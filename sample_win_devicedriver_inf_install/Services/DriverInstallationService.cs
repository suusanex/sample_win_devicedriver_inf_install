using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// ドライバインストールサービスの実装（宣言的インストール専用）
/// </summary>
public class DriverInstallationService : IDriverInstallationService
{
    private readonly ILogger<DriverInstallationService> _logger;
    private readonly WindowsApiErrorHandler _errorHandler;
    private readonly ConcurrentDictionary<string, InstallationSession> _activeSessions;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="errorHandler">Windows API エラーハンドラー</param>
    public DriverInstallationService(
        ILogger<DriverInstallationService> logger,
        WindowsApiErrorHandler errorHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _activeSessions = new ConcurrentDictionary<string, InstallationSession>();
    }

    /// <summary>
    /// INFファイルに従った包括的な宣言的インストールを実行します
    /// </summary>
    /// <param name="infPath">INFファイルパス</param>
    /// <param name="sectionName">インストールセクション名（デフォルト: "DefaultInstall"）</param>
    /// <param name="flags">インストールフラグ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>インストール結果</returns>
    public async Task<InstallationResult> InstallFromInfAsync(
        string infPath,
        string sectionName = "DefaultInstall",
        uint flags = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(infPath))
            throw new ArgumentException("INF file path cannot be null or empty", nameof(infPath));

        // ドライバパッケージを作成
        var driverPackage = new DriverPackage
        {
            Id = Guid.NewGuid().ToString(),
            InfPath = infPath,
            Name = Path.GetFileNameWithoutExtension(infPath)
        };

        var session = CreateSession(driverPackage, cancellationToken);
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting declarative installation from INF: {InfPath}, Section: {SectionName} (SessionId: {SessionId})",
                infPath, sectionName, session.SessionId);

            // セッション状態を更新
            session.Status = InstallationStatus.InProgress;

            // Step 1: INF ファイルの検証
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateInfFileAsync(infPath, cancellationToken);
            session.UpdateProgress(20, "INF file validation completed");

            // Step 2: INF ファイルを開く
            cancellationToken.ThrowIfCancellationRequested();
            var infHandle = await OpenInfFileAsync(infPath, cancellationToken);
            
            try
            {
                session.UpdateProgress(40, "INF file opened successfully");

                // Step 3: 指定セクションからのインストール実行
                cancellationToken.ThrowIfCancellationRequested();
                await InstallFromInfSectionAsync(infHandle, sectionName, flags, cancellationToken);
                session.UpdateProgress(70, "Main section installation completed");

                // Step 4: Services セクションの自動検出と条件付き実行
                cancellationToken.ThrowIfCancellationRequested();
                await InstallServicesFromInfAsync(infHandle, sectionName, cancellationToken);
                session.UpdateProgress(90, "Services section processing completed");

                session.UpdateProgress(100, "Declarative installation completed");
            }
            finally
            {
                // INF ファイルハンドルを確実に閉じる
                SetupApi.SetupCloseInfFile(infHandle);
            }

            // 成功結果の作成
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Completed);
            
            var result = InstallationResult.Success(
                sessionId: session.SessionId,
                installedPackage: driverPackage,
                executionTime: executionTime,
                technicalMessage: $"Declarative installation completed successfully from {sectionName} section"
            );

            _logger.LogInformation("Declarative installation completed successfully: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Declarative installation was cancelled: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);
            
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Cancelled);
            
            return InstallationResult.Cancelled(
                sessionId: session.SessionId,
                executionTime: executionTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during declarative installation: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            var errorInfo = _errorHandler.CreateFromException(ex, "InstallFromInfAsync");
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
                await Task.Delay(TimeSpan.FromHours(1), CancellationToken.None);
                _activeSessions.TryRemove(session.SessionId, out _);
            });
        }
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
        
        _logger.LogDebug("Created new installation session: {SessionId} for {DriverName}",
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
    /// INF ファイルの基本検証
    /// </summary>
    private async Task ValidateInfFileAsync(string infFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(infFilePath))
            throw new FileNotFoundException($"INF file not found: {infFilePath}");

        // 非同期でファイル内容を読み取って基本的な検証を実行
        var content = await File.ReadAllTextAsync(infFilePath, cancellationToken);
        
        if (!content.Contains("[Version]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid INF file: [Version] section not found");

        _logger.LogDebug("INF file validation completed: {InfFilePath}", infFilePath);
    }

    /// <summary>
    /// INF ファイルを開く
    /// </summary>
    private async Task<IntPtr> OpenInfFileAsync(string infFilePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var infHandle = SetupApi.SetupOpenInfFile(
                infFilePath,
                null, // InfClass (auto-detect)
                SetupApi.INF_STYLE_WIN4,
                out uint errorLine
            );

            if (infHandle == SetupApi.INVALID_HANDLE_VALUE)
            {
                var error = _errorHandler.GetLastError("SetupOpenInfFile", $"Error line: {errorLine}");
                throw new InvalidOperationException($"Failed to open INF file: {error.SystemMessage}");
            }

            return infHandle;
        }, cancellationToken);
    }

    /// <summary>
    /// 指定セクションからのインストール実行
    /// </summary>
    private async Task InstallFromInfSectionAsync(IntPtr infHandle, string sectionName, uint flags, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // デフォルトフラグを設定（全ての処理を実行）
            if (flags == 0)
                flags = SetupApi.SPINST_ALL;

            bool success = SetupApi.SetupInstallFromInfSection(
                IntPtr.Zero, // Owner (no parent window)
                infHandle,
                sectionName,
                flags,
                IntPtr.Zero, // RelativeKeyRoot (default)
                null, // SourceRootPath (use INF directory)
                0, // CopyFlags
                IntPtr.Zero, // MsgHandler
                IntPtr.Zero, // Context
                IntPtr.Zero, // DeviceInfoSet
                IntPtr.Zero  // DeviceInfoData
            );

            if (!success)
            {
                var error = _errorHandler.GetLastError("SetupInstallFromInfSection", $"Section: {sectionName}");
                throw new InvalidOperationException($"Failed to install from INF section '{sectionName}': {error.SystemMessage}");
            }

            _logger.LogDebug("Successfully installed from INF section: {SectionName}", sectionName);
        }, cancellationToken);
    }

    /// <summary>
    /// Services セクションの自動検出と条件付きインストール
    /// </summary>
    private async Task InstallServicesFromInfAsync(IntPtr infHandle, string baseSectionName, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // .Services セクションの存在確認
            var servicesSectionName = $"{baseSectionName}.Services";
            
            bool servicesExist = SetupApi.SetupFindFirstLine(
                infHandle,
                servicesSectionName,
                null, // Key (any line in section)
                out SetupApi.INFCONTEXT context
            );

            if (servicesExist)
            {
                _logger.LogDebug("Found Services section: {ServicesSectionName}", servicesSectionName);

                bool success = SetupApi.SetupInstallServicesFromInfSection(
                    infHandle,
                    servicesSectionName,
                    0 // Flags (default)
                );

                if (!success)
                {
                    var error = _errorHandler.GetLastError("SetupInstallServicesFromInfSection", $"Section: {servicesSectionName}");
                    // Services セクションの失敗は警告として扱う（致命的ではない）
                    _logger.LogWarning("Failed to install services from section '{ServicesSectionName}': {ErrorMessage}", 
                        servicesSectionName, error.SystemMessage);
                }
                else
                {
                    _logger.LogDebug("Successfully installed services from section: {ServicesSectionName}", servicesSectionName);
                }
            }
            else
            {
                _logger.LogDebug("No Services section found for: {BaseSectionName}", baseSectionName);
            }
        }, cancellationToken);
    }

    #endregion
}