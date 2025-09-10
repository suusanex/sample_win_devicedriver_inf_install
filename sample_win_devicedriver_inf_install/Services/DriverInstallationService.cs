using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Core.Contracts;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Core.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Core.Services;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Native;
using AppLogLevel = sample_win_devicedriver_inf_install.Enums.LogLevel;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// ドライバインストールサービスの実装（コア層 - FR-014準拠）
/// SetupAPI を使用したドライバインストール機能を提供します
/// </summary>
public class DriverInstallationService : IDriverInstallationService
{
    private readonly ILogger<DriverInstallationService> _logger;
    private readonly WindowsApiErrorHandler _errorHandler;
    private readonly DriverStatusService _statusService;
    private readonly ConcurrentDictionary<string, InstallationSession> _activeSessions;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="errorHandler">Windows API エラーハンドラー</param>
    /// <param name="statusService">ドライバ状態確認サービス</param>
    public DriverInstallationService(
        ILogger<DriverInstallationService> logger,
        WindowsApiErrorHandler errorHandler,
        DriverStatusService statusService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _activeSessions = new ConcurrentDictionary<string, InstallationSession>();
    }

    /// <summary>
    /// ドライバを非同期でインストールします
    /// </summary>
    /// <param name="driverPackage">インストールするドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>インストール結果</returns>
    public async Task<InstallationResult> InstallDriverAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default)
    {
        if (driverPackage == null)
            throw new ArgumentNullException(nameof(driverPackage));

        var session = CreateSession(driverPackage, cancellationToken);
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("ドライバインストールを開始します: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            // セッション状態を更新
            session.Status = InstallationStatus.InProgress;

            // Step 1: INF ファイルの検証
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateInfFileAsync(driverPackage.InfPath, cancellationToken);
            session.UpdateProgress(20, "INF ファイル検証完了");

            // Step 2: OEM INF のコピー
            cancellationToken.ThrowIfCancellationRequested();
            var copiedInfPath = await CopyOEMInfAsync(driverPackage.InfPath, cancellationToken);
            session.UpdateProgress(40, "INF ファイルコピー完了");
            session.LogMessages.Add($"INF ファイルをシステムディレクトリにコピーしました: {copiedInfPath}");

            // Step 3: デバイス情報セットの作成
            cancellationToken.ThrowIfCancellationRequested();
            using var deviceInfoSet = CreateDeviceInfoSet();
            session.UpdateProgress(60, "デバイス情報セット作成完了");

            // Step 4: ドライバのインストール実行
            cancellationToken.ThrowIfCancellationRequested();
            await InstallDriverInternalAsync(deviceInfoSet, driverPackage, session, cancellationToken);
            session.UpdateProgress(80, "ドライバインストール実行完了");

            // Step 5: インストール後の検証
            cancellationToken.ThrowIfCancellationRequested();
            var deviceInstances = await _statusService.GetDriverStatusAsync(driverPackage, cancellationToken);
            session.UpdateProgress(100, "インストール検証完了");

            // 成功結果の作成
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Completed);
            
            var result = InstallationResult.Success(
                sessionId: session.SessionId,
                installedPackage: driverPackage,
                affectedDevices: deviceInstances,
                executionTime: executionTime,
                technicalMessage: "Driver installation completed successfully"
            );

            _logger.LogInformation("ドライバインストールが完了しました: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ドライバインストールがキャンセルされました: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);
            
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Cancelled);
            
            return InstallationResult.Cancelled(
                sessionId: session.SessionId,
                executionTime: executionTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ドライバインストールでエラーが発生しました: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            var errorInfo = _errorHandler.CreateFromException(ex, "InstallDriverAsync");
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Failed, ex.Message);

            return InstallationResult.Failure(
                sessionId: session.SessionId,
                errorInfo: errorInfo,
                executionTime: executionTime
            );
        }
        finally
        {
            // セッションをアクティブリストから削除（1時間後）
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromHours(1));
                _activeSessions.TryRemove(session.SessionId, out _);
            });
        }
    }

    /// <summary>
    /// ドライバを非同期でアンインストールします
    /// </summary>
    /// <param name="driverPackage">アンインストールするドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>アンインストール結果</returns>
    public async Task<InstallationResult> UninstallDriverAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default)
    {
        if (driverPackage == null)
            throw new ArgumentNullException(nameof(driverPackage));

        var session = CreateSession(driverPackage, cancellationToken);
        var startTime = DateTime.UtcNow;
        session.Status = InstallationStatus.InProgress;

        try
        {
            _logger.LogInformation("ドライバアンインストールを開始します: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            // 関連デバイスの取得
            var devices = await _statusService.GetDriverStatusAsync(driverPackage, cancellationToken);
            session.UpdateProgress(30, "対象デバイス検出完了");

            // 各デバイスのアンインストール
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UninstallDeviceAsync(device, session, cancellationToken);
            }

            session.UpdateProgress(100, "アンインストール完了");
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Completed);

            _logger.LogInformation("ドライバアンインストールが完了しました: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            return InstallationResult.Success(
                sessionId: session.SessionId,
                installedPackage: driverPackage,
                affectedDevices: new List<DeviceInstance>(),
                executionTime: executionTime,
                technicalMessage: "Driver uninstallation completed successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ドライバアンインストールでエラーが発生しました: {DriverName} (SessionId: {SessionId})",
                driverPackage.Name, session.SessionId);

            var errorInfo = _errorHandler.CreateFromException(ex, "UninstallDriverAsync");
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Failed, ex.Message);

            return InstallationResult.Failure(
                sessionId: session.SessionId,
                errorInfo: errorInfo,
                executionTime: executionTime
            );
        }
    }

    /// <summary>
    /// ドライバの状態を非同期で取得します
    /// </summary>
    /// <param name="driverPackage">確認するドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>ドライバに関連するデバイス一覧</returns>
    public async Task<List<DeviceInstance>> GetDriverStatusAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default)
    {
        return await _statusService.GetDriverStatusAsync(driverPackage, cancellationToken);
    }

    /// <summary>
    /// インストールセッションを作成します
    /// </summary>
    /// <param name="driverPackage">ドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>作成されたセッション</returns>
    public InstallationSession CreateSession(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default)
    {
        var session = InstallationSession.Create(driverPackage, cancellationToken);
        
        _activeSessions[session.SessionId] = session;
        
        _logger.LogDebug("新しいインストールセッションを作成しました: {SessionId} for {DriverName}",
            session.SessionId, driverPackage.Name);
        
        return session;
    }

    /// <summary>
    /// アクティブなセッション一覧を取得します
    /// </summary>
    /// <returns>アクティブなセッション一覧</returns>
    public IEnumerable<InstallationSession> GetActiveSessions()
    {
        return _activeSessions.Values.ToList();
    }

    /// <summary>
    /// 指定されたセッションを取得します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <returns>セッション（見つからない場合はnull）</returns>
    public InstallationSession? GetSession(string sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out var session);
        return session;
    }

    #region Private Helper Methods

    /// <summary>
    /// INF ファイルの検証
    /// </summary>
    private async Task ValidateInfFileAsync(string infFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(infFilePath))
            throw new ArgumentException("INF ファイルパスが指定されていません");

        if (!File.Exists(infFilePath))
            throw new FileNotFoundException($"INF ファイルが見つかりません: {infFilePath}");

        // 非同期でファイル内容を読み取って基本的な検証を実行
        var content = await File.ReadAllTextAsync(infFilePath, cancellationToken);
        
        if (!content.Contains("[Version]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("無効な INF ファイル: [Version] セクションが見つかりません");

        _logger.LogDebug("INF ファイルの検証が完了しました: {InfFilePath}", infFilePath);
    }

    /// <summary>
    /// OEM INF のコピー
    /// </summary>
    private async Task<string> CopyOEMInfAsync(string sourceInfPath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var destinationBuffer = new StringBuilder(SetupApi.MAX_PATH);
            var destinationComponentBuffer = new StringBuilder(SetupApi.MAX_PATH);
            
            bool success = SetupApi.SetupCopyOEMInf(
                sourceInfPath,
                null, // OEMSourceMediaLocation
                SetupApi.SPOST_PATH,
                SetupApi.SPOST_NONE,
                destinationBuffer,
                SetupApi.MAX_PATH,
                out uint requiredSize,
                out destinationComponentBuffer
            );

            if (!success)
            {
                var error = _errorHandler.GetLastError("SetupCopyOEMInf");
                throw new InvalidOperationException($"INF ファイルのコピーに失敗しました: {error.SystemMessage}");
            }

            return destinationBuffer.ToString();
        }, cancellationToken);
    }

    /// <summary>
    /// デバイス情報セットの作成
    /// </summary>
    private DeviceInfoSetHandle CreateDeviceInfoSet()
    {
        // すべてのクラスのデバイス情報セットを作成
        var classGuid = Guid.Empty;
        var deviceInfoSet = SetupApi.SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            SetupApi.DIGCF_ALLCLASSES
        );

        if (deviceInfoSet == SetupApi.INVALID_HANDLE_VALUE)
        {
            var error = _errorHandler.GetLastError("SetupDiGetClassDevs");
            throw new InvalidOperationException($"デバイス情報セットの作成に失敗しました: {error.SystemMessage}");
        }

        return new DeviceInfoSetHandle(deviceInfoSet);
    }

    /// <summary>
    /// ドライバインストールの内部実装
    /// </summary>
    private async Task InstallDriverInternalAsync(
        DeviceInfoSetHandle deviceInfoSet,
        DriverPackage driverPackage,
        InstallationSession session,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // デバイス情報の列挙とインストール処理
            var devInfoData = SetupApi.CreateDevInfoData();
            uint memberIndex = 0;

            // 関連デバイスを検索してインストール
            while (SetupApi.SetupDiEnumDeviceInfo(deviceInfoSet.Handle, memberIndex++, ref devInfoData))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // ハードウェアIDの取得と一致確認
                    var hardwareIds = GetDeviceHardwareIds(deviceInfoSet.Handle, ref devInfoData);
                    
                    if (IsMatchingDevice(hardwareIds, driverPackage.HardwareIds))
                    {
                        // デバイスドライバのインストール実行
                        bool installResult = SetupApi.SetupDiCallClassInstaller(
                            SetupApi.DIF_INSTALLDEVICE,
                            deviceInfoSet.Handle,
                            ref devInfoData
                        );

                        if (!installResult)
                        {
                            var error = _errorHandler.GetLastError("SetupDiCallClassInstaller");
                            session.LogMessages.Add($"デバイスインストールで警告: {error.SystemMessage}");
                        }
                        else
                        {
                            session.LogMessages.Add("デバイスドライバのインストールが完了しました");
                        }
                    }
                }
                catch (Exception ex)
                {
                    session.LogMessages.Add($"デバイス処理中にエラー: {ex.Message}");
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// デバイスのハードウェアIDを取得
    /// </summary>
    private string[] GetDeviceHardwareIds(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA devInfoData)
    {
        const uint bufferSize = 4096;
        var buffer = new byte[bufferSize];

        bool success = SetupApi.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref devInfoData,
            SetupApi.SPDRP_HARDWAREID,
            out uint propertyRegDataType,
            buffer,
            bufferSize,
            out uint requiredSize
        );

        if (!success)
        {
            return Array.Empty<string>();
        }

        // マルチ文字列を分割
        var str = Encoding.Unicode.GetString(buffer, 0, (int)requiredSize);
        return str.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// デバイスが対象ドライバと一致するかチェック
    /// </summary>
    private bool IsMatchingDevice(string[] deviceHardwareIds, List<string> driverHardwareIds)
    {
        return deviceHardwareIds.Any(deviceId =>
            driverHardwareIds.Any(driverId =>
                string.Equals(deviceId, driverId, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// デバイスのアンインストール
    /// </summary>
    private async Task UninstallDeviceAsync(DeviceInstance device, InstallationSession session, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // デバイス情報セットを取得
            using var deviceInfoSet = CreateDeviceInfoSet();
            var devInfoData = SetupApi.CreateDevInfoData();

            // デバイスの削除実行
            bool removeResult = SetupApi.SetupDiCallClassInstaller(
                SetupApi.DIF_REMOVE,
                deviceInfoSet.Handle,
                ref devInfoData
            );

            if (!removeResult)
            {
                var error = _errorHandler.GetLastError("SetupDiCallClassInstaller (Remove)");
                session.LogMessages.Add($"デバイス削除で警告: {error.SystemMessage}");
            }
            else
            {
                session.LogMessages.Add($"デバイスを削除しました: {device.DeviceName}");
            }
        }, cancellationToken);
    }

    #endregion

    /// <summary>
    /// デバイス情報セットのハンドルラッパー
    /// </summary>
    private class DeviceInfoSetHandle : IDisposable
    {
        public IntPtr Handle { get; }
        private bool _disposed = false;

        public DeviceInfoSetHandle(IntPtr handle)
        {
            Handle = handle;
        }

        public void Dispose()
        {
            if (!_disposed && Handle != IntPtr.Zero && Handle != SetupApi.INVALID_HANDLE_VALUE)
            {
                SetupApi.SetupDiDestroyDeviceInfoList(Handle);
                _disposed = true;
            }
        }
    }
}