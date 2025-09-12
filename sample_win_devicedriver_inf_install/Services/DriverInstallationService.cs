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
    private readonly IInstallationLogger _installationLogger;
    private readonly WindowsApiErrorHandler _errorHandler;
    private readonly ISetupApiWrapper _setupApiWrapper;
    private readonly ConcurrentDictionary<string, InstallationSession> _activeSessions;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="installationLogger">インストールロガー</param>
    /// <param name="errorHandler">Windows API エラーハンドラー</param>
    /// <param name="setupApiWrapper">SetupAPI ラッパー</param>
    public DriverInstallationService(
        ILogger<DriverInstallationService> logger,
        IInstallationLogger installationLogger,
        WindowsApiErrorHandler errorHandler,
        ISetupApiWrapper setupApiWrapper)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _installationLogger = installationLogger ?? throw new ArgumentNullException(nameof(installationLogger));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _setupApiWrapper = setupApiWrapper ?? throw new ArgumentNullException(nameof(setupApiWrapper));
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

            // インストール開始ログ (テストが期待する "Started" メッセージ)
            await _installationLogger.LogInformationAsync(
                $"Started declarative installation from INF: {infPath}, Section: {sectionName}",
                "Installation",
                session.SessionId);

            // セッション状態を更新
            session.Status = InstallationStatus.InProgress;

            // Step 1: INF ファイルの検証
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateInfFileAsync(infPath, session.SessionId, cancellationToken);
            session.UpdateProgress(20, "INF file validation completed");

            // Step 2: INF ファイルを開く
            cancellationToken.ThrowIfCancellationRequested();
            var infHandle = await OpenInfFileAsync(infPath, session.SessionId, cancellationToken);
            
            try
            {
                session.UpdateProgress(40, "INF file opened successfully");

                // Step 3: 指定セクションからのインストール実行
                cancellationToken.ThrowIfCancellationRequested();
                await InstallFromInfSectionAsync(infHandle, sectionName, flags, session.SessionId, cancellationToken);
                session.UpdateProgress(70, "Main section installation completed");

                // Step 4: Services セクションの自動検出と条件付き実行
                cancellationToken.ThrowIfCancellationRequested();
                await InstallServicesFromInfAsync(infHandle, sectionName, session.SessionId, cancellationToken);
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
            
            await _installationLogger.LogInformationAsync(
                $"Completed declarative installation from {sectionName} section",
                "Installation",
                session.SessionId);

            var result = InstallationResult.Success(
                sessionId: session.SessionId,
                installedPackage: driverPackage,
                sectionName: sectionName,
                executionTime: executionTime,
                technicalMessage: $"Declarative installation completed successfully from {sectionName} section"
            );

            // ログエントリを結果に含める
            var logEntries = await _installationLogger.GetLogEntriesAsync(session.SessionId, cancellationToken);
            result.AddLogEntries(logEntries);

            _logger.LogInformation("Declarative installation completed successfully: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Declarative installation was cancelled: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);
            
            await _installationLogger.LogWarningAsync(
                "Declarative installation was cancelled",
                "Installation",
                session.SessionId);
            
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Cancelled);
            
            var result = InstallationResult.Cancelled(
                sessionId: session.SessionId,
                sectionName: sectionName,
                executionTime: executionTime
            );

            // ログエントリを結果に含める
            var logEntries = await _installationLogger.GetLogEntriesAsync(session.SessionId, cancellationToken);
            result.AddLogEntries(logEntries);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during declarative installation: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            await _installationLogger.LogErrorAsync(
                $"Error occurred during declarative installation: {ex.Message}",
                "Installation",
                session.SessionId,
                ex);

            var errorInfo = _errorHandler.CreateFromException(ex, "InstallFromInfAsync");
            var executionTime = DateTime.UtcNow - startTime;
            session.Complete(InstallationStatus.Failed, ex.Message);

            var result = InstallationResult.Failure(
                sessionId: session.SessionId,
                errorInfo: errorInfo,
                sectionName: sectionName,
                executionTime: executionTime,
                technicalMessage: null,
                installedPackage: driverPackage // keep InfPath available on failure
            );

            // ログエントリを結果に含める
            var logEntries = await _installationLogger.GetLogEntriesAsync(session.SessionId, cancellationToken);
            result.AddLogEntries(logEntries);

            return result;
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
    /// INF ファイルの基本検証を行います。
    /// 仕様（この実装に準拠）:
    /// - 検証対象は次の2点のみです。
    ///   1) 指定パスにファイルが存在すること。
    ///   2) ファイル内容に文字列 "[Version]" が含まれていること（大文字小文字は無視）。
    /// - 上記以外（例: [Version] セクション内の Signature 值や DriverVer 形式など）の妥当性は検証しません。
    /// - 条件を満たさない場合は InvalidOperationException（[Version] 欠落）または FileNotFoundException（ファイル不存在）を投げます。
    /// - ログ: 失敗時は Validation カテゴリでエラーログ、成功時は情報ログを出力します。
    /// </summary>
    /// <param name="infFilePath">INF ファイルのフルパス</param>
    /// <param name="correlationId">相関ID（セッションID）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
    /// <exception cref="InvalidOperationException">[Version] セクションが見つからない場合</exception>
    private async Task ValidateInfFileAsync(string infFilePath, string correlationId, CancellationToken cancellationToken)
    {
        if (!File.Exists(infFilePath))
        {
            await _installationLogger.LogErrorAsync(
                $"INF file not found: {infFilePath}",
                "Validation",
                correlationId);
            throw new FileNotFoundException($"INF file not found: {infFilePath}");
        }

        // 非同期でファイル内容を読み取って基本的な検証を実行
        var content = await File.ReadAllTextAsync(infFilePath, cancellationToken);
        
        if (!content.Contains("[Version]", StringComparison.OrdinalIgnoreCase))
        {
            await _installationLogger.LogErrorAsync(
                "Invalid INF file: [Version] section not found",
                "Validation",
                correlationId);
            throw new InvalidOperationException("Invalid INF file: [Version] section not found");
        }

        await _installationLogger.LogInformationAsync(
            $"INF file validation completed: {infFilePath}",
            "Validation",
            correlationId);

        _logger.LogDebug("INF file validation completed: {InfFilePath}", infFilePath);
    }

    /// <summary>
    /// INF ファイルを開く（スタブ由来の一時的エラーに対する簡易リトライを含む）
    /// </summary>
    private async Task<IntPtr> OpenInfFileAsync(string infFilePath, string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IntPtr AttemptOpen()
        {
            return _setupApiWrapper.SetupOpenInfFile(
                infFilePath,
                null, // InfClass (auto-detect)
                SetupApi.INF_STYLE_WIN4,
                out uint _
            );
        }

        var infHandle = AttemptOpen();

        if (infHandle == SetupApi.INVALID_HANDLE_VALUE)
        {
            // 1回だけリトライ（特に _lastError が 0 の場合、前回のエラー残骸による失敗の可能性）
            var firstError = _errorHandler.GetLastError("SetupOpenInfFile");
            if (firstError.ErrorCode == 0)
            {
                infHandle = AttemptOpen();
            }

            if (infHandle == SetupApi.INVALID_HANDLE_VALUE)
            {
                var error = firstError.ErrorCode != 0 ? firstError : _errorHandler.GetLastError("SetupOpenInfFile");
                await _installationLogger.LogErrorAsync(
                    $"Failed to open INF file: {error.SystemMessage}",
                    "API",
                    correlationId);

                throw new InvalidOperationException($"Failed to open INF file: {error.SystemMessage}");
            }
        }

        await _installationLogger.LogInformationAsync(
            $"Successfully opened INF file: {infFilePath}",
            "API",
            correlationId);

        return infHandle;
    }

    /// <summary>
    /// 指定セクションからのインストール実行
    /// </summary>
    private async Task InstallFromInfSectionAsync(IntPtr infHandle, string sectionName, uint flags, string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // まずセクションの存在確認を行う
        bool sectionExists = _setupApiWrapper.SetupFindFirstLine(
            infHandle,
            sectionName,
            null, // Key (any line in section)
            out SetupApi.INFCONTEXT context
        );

        if (!sectionExists)
        {
            await _installationLogger.LogErrorAsync(
                $"Section '{sectionName}' not found in INF file",
                "API",
                correlationId);

            throw new InvalidOperationException($"Section '{sectionName}' not found in INF file");
        }

        // デフォルトフラグを設定（全ての処理を実行）
        if (flags == 0)
            flags = SetupApi.SPINST_ALL;

        // テストが期待するログメッセージを記録
        await _installationLogger.LogInformationAsync(
            $"Installing from section '{sectionName}' using SetupInstallFromInfSection",
            "API",
            correlationId);

        bool success = _setupApiWrapper.SetupInstallFromInfSection(
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
            await _installationLogger.LogErrorAsync(
                $"Failed to install from INF section '{sectionName}': {error.SystemMessage}",
                "API",
                correlationId);

            throw new InvalidOperationException($"Failed to install from INF section '{sectionName}': {error.SystemMessage}");
        }

        await _installationLogger.LogInformationAsync(
            $"Successfully installed from INF section: {sectionName}",
            "API",
            correlationId);

        _logger.LogDebug("Successfully installed from INF section: {SectionName}", sectionName);
    }

    /// <summary>
    /// Services セクションの自動検出と条件付きインストール
    /// </summary>
    private async Task InstallServicesFromInfAsync(IntPtr infHandle, string baseSectionName, string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // .Services セクションの存在確認
        var servicesSectionName = $"{baseSectionName}.Services";
        
        bool servicesExist = _setupApiWrapper.SetupFindFirstLine(
            infHandle,
            servicesSectionName,
            null, // Key (any line in section)
            out SetupApi.INFCONTEXT context
        );

        if (servicesExist)
        {
            await _installationLogger.LogInformationAsync(
                $"Found Services section: {servicesSectionName}",
                "API",
                correlationId);

            _logger.LogDebug("Found Services section: {ServicesSectionName}", servicesSectionName);

            bool success = _setupApiWrapper.SetupInstallServicesFromInfSection(
                infHandle,
                servicesSectionName,
                0 // Flags (default)
            );

            if (!success)
            {
                var error = _errorHandler.GetLastError("SetupInstallServicesFromInfSection", $"Section: {servicesSectionName}");
                // Services セクションの失敗は警告として扱う（致命的ではない）
                await _installationLogger.LogWarningAsync(
                    $"Failed to install services from section '{servicesSectionName}': {error.SystemMessage}",
                    "API",
                    correlationId);

                _logger.LogWarning("Failed to install services from section '{ServicesSectionName}': {ErrorMessage}", 
                    servicesSectionName, error.SystemMessage);
            }
            else
            {
                await _installationLogger.LogInformationAsync(
                    $"Successfully installed services from section: {servicesSectionName}",
                    "API",
                    correlationId);

                _logger.LogDebug("Successfully installed services from section: {ServicesSectionName}", servicesSectionName);
            }
        }
        else
        {
            await _installationLogger.LogInformationAsync(
                $"Services section not found for: {baseSectionName}",
                "API",
                correlationId);

            _logger.LogDebug("No Services section found for: {BaseSectionName}", baseSectionName);
        }
    }

    #endregion
}