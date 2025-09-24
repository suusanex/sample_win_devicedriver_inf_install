using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// ドライバインストールサービスの実装（宣言的インストール専用・3段階実行対応）
/// </summary>
public class DriverInstallationService : IDriverInstallationService
{
    private readonly ILogger<DriverInstallationService> _logger;
    private readonly IInstallationLogger _installationLogger;
    private readonly WindowsApiErrorHandler _errorHandler;
    private readonly ISetupApiWrapper _setupApiWrapper;
    private readonly ConcurrentDictionary<string, InstallationSession> _activeSessions;

    // SetupAPI呼び出しのデフォルトタイムアウト（NFR-003準拠・各段階）
    private static readonly TimeSpan DefaultSetupApiTimeout = TimeSpan.FromSeconds(30);

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
    /// INFファイルに従った包括的な宣言的インストールを実行します（3段階実行）
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
            _logger.LogInformation("Starting 3-phase declarative installation from INF: {InfPath}, Section: {SectionName} (SessionId: {SessionId})",
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
            session.UpdateProgress(15, "INF file validation completed");

            // Step 2: INF ファイルを開く
            cancellationToken.ThrowIfCancellationRequested();
            var infHandle = await OpenInfFileAsync(infPath, session.SessionId, cancellationToken);
            
            try
            {
                session.UpdateProgress(25, "INF file opened successfully");

                // Step 3: 3段階実行による完全なインストール
                cancellationToken.ThrowIfCancellationRequested();
                
                // 段階1: ファイル操作（CopyFiles）
                await InstallFilesFromInfAsync(infHandle, sectionName, session.SessionId, infPath, cancellationToken);
                session.UpdateProgress(45, "File operations (CopyFiles) completed");

                // 段階2: レジストリ操作（AddReg等）
                cancellationToken.ThrowIfCancellationRequested();
                await InstallRegistryFromInfAsync(infHandle, sectionName, session.SessionId, cancellationToken);
                session.UpdateProgress(70, "Registry operations (AddReg) completed");

                // 段階3: サービス登録（Services セクション）
                cancellationToken.ThrowIfCancellationRequested();
                await InstallServicesFromInfAsync(infHandle, sectionName, session.SessionId, cancellationToken);
                session.UpdateProgress(90, "Services registration completed");

                session.UpdateProgress(100, "3-phase declarative installation completed");
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
                $"Completed 3-phase declarative installation from {sectionName} section",
                "Installation",
                session.SessionId);

            var result = InstallationResult.Success(
                sessionId: session.SessionId,
                installedPackage: driverPackage,
                sectionName: sectionName,
                executionTime: executionTime,
                technicalMessage: $"3-phase declarative installation completed successfully from {sectionName} section"
            );

            // ログエントリを結果に含める
            var logEntries = await _installationLogger.GetLogEntriesAsync(session.SessionId, cancellationToken);
            result.AddLogEntries(logEntries);

            _logger.LogInformation("3-phase declarative installation completed successfully: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("3-phase declarative installation was cancelled: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);
            
            await _installationLogger.LogWarningAsync(
                "3-phase declarative installation was cancelled",
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
            _logger.LogError(ex, "Error occurred during 3-phase declarative installation: {InfPath} (SessionId: {SessionId})",
                infPath, session.SessionId);

            await _installationLogger.LogErrorAsync(
                $"Error occurred during 3-phase declarative installation: {ex.Message}",
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
    /// 段階1: ファイル操作（CopyFiles等）を実行します（タイムアウト制御付き・サイレントモード対応）
    /// </summary>
    private async Task InstallFilesFromInfAsync(IntPtr infHandle, string sectionName, string correlationId, string infPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // セクションの存在確認を行う
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
                "FileOperation",
                correlationId);
            throw new InvalidOperationException($"Section '{sectionName}' not found in INF file");
        }

        await _installationLogger.LogInformationAsync(
            $"Starting file operations (CopyFiles) from section '{sectionName}'",
            "FileOperation",
            correlationId);

        // INFファイルのディレクトリを取得してソースルートパスとして設定
        var infDirectory = Path.GetDirectoryName(infPath) ?? throw new InvalidOperationException($"Could not determine directory for INF file: {infPath}");
        
        await _installationLogger.LogInformationAsync(
            $"Setting source root path to INF directory: {infDirectory}",
            "FileOperation",
            correlationId);

        // ファイルキューを開く
        IntPtr fileQueue = IntPtr.Zero;
        try
        {
            var fileQueueTask = Task.Run(() => _setupApiWrapper.SetupOpenFileQueue());
            fileQueue = await fileQueueTask.WaitAsync(DefaultSetupApiTimeout, cancellationToken);

            if (fileQueue == IntPtr.Zero)
            {
                var error = _errorHandler.GetLastError("SetupOpenFileQueue");
                await _installationLogger.LogErrorAsync(
                    $"Failed to open file queue: {error.SystemMessage}",
                    "FileOperation",
                    correlationId);
                throw new InvalidOperationException($"Failed to open file queue: {error.SystemMessage}");
            }

            // ファイル操作をキューに追加（INFディレクトリを明示的にソースルートパスとして指定）
            bool filesQueued;
            try
            {
                var queueFilesTask = Task.Run(() => _setupApiWrapper.SetupInstallFilesFromInfSection(
                    infHandle,
                    IntPtr.Zero, // LayoutInfHandle (same as main INF)
                    fileQueue,
                    sectionName,
                    infDirectory, // SourceRootPath (INFと同じディレクトリを明示指定)
                    0 // CopyStyle (default)
                ));
                filesQueued = await queueFilesTask.WaitAsync(DefaultSetupApiTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                await _installationLogger.LogErrorAsync(
                    $"SetupInstallFilesFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds for section '{sectionName}'",
                    "FileOperation",
                    correlationId);
                throw new OperationCanceledException($"SetupInstallFilesFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds");
            }

            if (!filesQueued)
            {
                var error = _errorHandler.GetLastError("SetupInstallFilesFromInfSection", $"Section: {sectionName}");
                await _installationLogger.LogErrorAsync(
                    $"Failed to queue files from section '{sectionName}': {error.SystemMessage}",
                    "FileOperation",
                    correlationId);
                throw new InvalidOperationException($"Failed to queue files from section '{sectionName}': {error.SystemMessage}");
            }

            await _installationLogger.LogInformationAsync(
                $"Successfully queued files from section '{sectionName}' with source root: {infDirectory}",
                "FileOperation",
                correlationId);

            // ファイルキューをサイレントコールバックでコミット（FR-012準拠）
            bool commitSuccess;
            try
            {
                var commitTask = Task.Run(() =>
                {
                    // サイレントコールバック対応のメソッドを使用（インターフェース経由）
                    var silentCallback = new SilentFileQueueCallback(_logger, _installationLogger, correlationId, infDirectory);
                    
                    // 詳細: どの実装が使用されているかログに記録
                    var wrapperType = _setupApiWrapper.GetType();
                    var wrapperTypeName = wrapperType.Name;
                    var wrapperAssembly = wrapperType.Assembly.GetName().Name;
                    
                    _logger.LogInformation("Using SetupApiWrapper implementation: {WrapperType} from {Assembly}", 
                        wrapperTypeName, wrapperAssembly);
                    
                    // SetupApiStub使用時の特別な処理
                    if (wrapperTypeName == "SetupApiStub")
                    {
                        _logger.LogInformation("Running in test mode with SetupApiStub - actual file operations will be mocked");
                    }
                    else
                    {
                        _logger.LogInformation("Running in production mode with real SetupAPI - actual file operations will be performed");
                        _logger.LogInformation("File queue handle: {FileQueue}, Silent callback initialized: {CallbackInitialized}, Source root: {SourceRoot}",
                            fileQueue, silentCallback != null, infDirectory);
                    }
                    
                    try
                    {
                        var result = _setupApiWrapper.SetupCommitFileQueueWithSilentCallback(
                            IntPtr.Zero, // Owner (no parent window)
                            fileQueue,
                            silentCallback
                        );
                        
                        _logger.LogInformation("SetupCommitFileQueueWithSilentCallback returned: {Result} (wrapper: {WrapperType})", 
                            result, wrapperTypeName);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in SetupCommitFileQueueWithSilentCallback (wrapper: {WrapperType})", wrapperTypeName);
                        
                        // フォールバック: 従来の方法を試行
                        _logger.LogWarning("Falling back to default commit method (wrapper: {WrapperType})", wrapperTypeName);
                        
                        try
                        {
                            var fallbackResult = _setupApiWrapper.SetupCommitFileQueue(
                                IntPtr.Zero, // Owner (no parent window)
                                fileQueue,
                                IntPtr.Zero, // MsgHandler
                                IntPtr.Zero  // Context
                            );
                            
                            _logger.LogInformation("Fallback SetupCommitFileQueue returned: {Result} (wrapper: {WrapperType})",
                                fallbackResult, wrapperTypeName);
                            
                            return fallbackResult;
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogError(fallbackEx, "Fallback commit method also failed (wrapper: {WrapperType})", wrapperTypeName);
                            throw;
                        }
                    }
                });
                commitSuccess = await commitTask.WaitAsync(DefaultSetupApiTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                await _installationLogger.LogErrorAsync(
                    $"SetupCommitFileQueue timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds for section '{sectionName}'",
                    "FileOperation",
                    correlationId);
                throw new OperationCanceledException($"SetupCommitFileQueue timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds");
            }

            if (!commitSuccess)
            {
                var error = _errorHandler.GetLastError("SetupCommitFileQueue", $"Section: {sectionName}");
                await _installationLogger.LogErrorAsync(
                    $"Failed to commit file queue for section '{sectionName}': {error.SystemMessage}",
                    "FileOperation",
                    correlationId);
                throw new InvalidOperationException($"Failed to commit file queue for section '{sectionName}': {error.SystemMessage}");
            }

            await _installationLogger.LogInformationAsync(
                $"Successfully completed file operations (CopyFiles) from section: {sectionName}",
                "FileOperation",
                correlationId);

            _logger.LogDebug("Successfully completed file operations from section: {SectionName}", sectionName);
        }
        finally
        {
            // ファイルキューを確実に閉じる
            if (fileQueue != IntPtr.Zero)
            {
                _setupApiWrapper.SetupCloseFileQueue(fileQueue);
            }
        }
    }

    /// <summary>
    /// 段階2: レジストリ操作（AddReg等）を実行します（タイムアウト制御付き）
    /// </summary>
    private async Task InstallRegistryFromInfAsync(IntPtr infHandle, string sectionName, string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // セクションの存在確認を行う
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
                "RegistryOperation",
                correlationId);
            throw new InvalidOperationException($"Section '{sectionName}' not found in INF file");
        }

        // レジストリ操作のみのフラグを設定（ファイル操作は除く）
        uint registryFlags = SetupApi.SPINST_REGISTRY | SetupApi.SPINST_INIFILES | SetupApi.SPINST_INI2REG | SetupApi.SPINST_BITREG;

        await _installationLogger.LogInformationAsync(
            $"Starting registry operations (AddReg) from section '{sectionName}' using SetupInstallFromInfSection",
            "RegistryOperation",
            correlationId);

        // SetupAPI呼び出しをタイムアウト制御可能な非同期実行でラップ（NFR-003準拠）
        // Task.WaitAsyncを使用してタイムアウト制御を実現
        bool success;
        try
        {
            var setupApiTask = Task.Run(() => _setupApiWrapper.SetupInstallFromInfSection(
                IntPtr.Zero, // Owner (no parent window)
                infHandle,
                sectionName,
                registryFlags,
                IntPtr.Zero, // RelativeKeyRoot (default)
                null, // SourceRootPath (use INF directory)
                0, // CopyFlags
                IntPtr.Zero, // MsgHandler
                IntPtr.Zero, // Context
                IntPtr.Zero, // DeviceInfoSet
                IntPtr.Zero  // DeviceInfoData
            ));

            success = await setupApiTask.WaitAsync(DefaultSetupApiTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            // タイムアウトが発生した場合
            await _installationLogger.LogErrorAsync(
                $"SetupInstallFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds for section '{sectionName}'",
                "RegistryOperation",
                correlationId);

            throw new OperationCanceledException($"SetupInstallFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds");
        }

        if (!success)
        {
            var error = _errorHandler.GetLastError("SetupInstallFromInfSection", $"Section: {sectionName}");
            await _installationLogger.LogErrorAsync(
                $"Failed to perform registry operations from section '{sectionName}': {error.SystemMessage}",
                "RegistryOperation",
                correlationId);

            throw new InvalidOperationException($"Failed to perform registry operations from section '{sectionName}': {error.SystemMessage}");
        }

        await _installationLogger.LogInformationAsync(
            $"Successfully completed registry operations (AddReg) from section: {sectionName}",
            "RegistryOperation",
            correlationId);

        _logger.LogDebug("Successfully completed registry operations from section: {SectionName}", sectionName);
    }

    /// <summary>
    /// 段階3: Services セクションの自動検出と条件付きインストール（タイムアウト制御付き）
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
                "ServiceOperation",
                correlationId);

            _logger.LogDebug("Found Services section: {ServicesSectionName}", servicesSectionName);

            // SetupAPI呼び出しをタイムアウト制御可能な非同期実行でラップ（NFR-003準拠）
            // Task.WaitAsyncを使用してタイムアウト制御を実現
            bool success;
            try
            {
                var setupApiTask = Task.Run(() => _setupApiWrapper.SetupInstallServicesFromInfSection(
                    infHandle,
                    servicesSectionName,
                    0 // Flags (default)
                ));

                success = await setupApiTask.WaitAsync(DefaultSetupApiTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                // タイムアウトが発生した場合（Services セクションも致命的エラーとして扱う）
                await _installationLogger.LogErrorAsync(
                    $"SetupInstallServicesFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds for section '{servicesSectionName}'",
                    "ServiceOperation",
                    correlationId);

                throw new OperationCanceledException($"SetupInstallServicesFromInfSection timed out after {DefaultSetupApiTimeout.TotalSeconds} seconds");
            }

            if (!success)
            {
                var error = _errorHandler.GetLastError("SetupInstallServicesFromInfSection", $"Section: {servicesSectionName}");
                // Services セクションの失敗も致命的エラーとして扱う
                await _installationLogger.LogErrorAsync(
                    $"Failed to install services from section '{servicesSectionName}': {error.SystemMessage}",
                    "ServiceOperation",
                    correlationId);

                throw new InvalidOperationException($"Failed to install services from section '{servicesSectionName}': {error.SystemMessage}");
            }
            else
            {
                await _installationLogger.LogInformationAsync(
                    $"Successfully installed services from section: {servicesSectionName}",
                    "ServiceOperation",
                    correlationId);

                _logger.LogDebug("Successfully installed services from section: {ServicesSectionName}", servicesSectionName);
            }
        }
        else
        {
            await _installationLogger.LogInformationAsync(
                $"Services section not found for: {baseSectionName}",
                "ServiceOperation",
                correlationId);

            _logger.LogDebug("No Services section found for: {BaseSectionName}", baseSectionName);
        }
    }

    #endregion
}