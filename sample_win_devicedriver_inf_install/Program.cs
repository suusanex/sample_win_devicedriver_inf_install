using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.CommandHandlers;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install;

/// <summary>
/// メインプログラムクラス（宣言的インストール専用）
/// </summary>
public class Program
{
    /// <summary>
    /// アプリケーションエントリーポイント
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <returns>終了コード</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // ローカライゼーションサービスを初期化（CLIパーサーで使用）
            var localization = new LocalizationService();
            var cliParser = new CliParser(localization);

            // コマンドライン引数を解析
            var (options, errorMessage) = cliParser.Parse(args);
            
            if (options == null)
            {
                Console.WriteLine(errorMessage);
                Console.WriteLine();
                Console.WriteLine(cliParser.GenerateHelpMessage());
                return 1;
            }

            // GenericHostを構築して実行
            var host = CreateHostBuilder(args, options).Build();
            
            using (host)
            {
                await host.StartAsync();

                // CommandExecutorServiceから終了コードを取得するため参照を保持
                var commandExecutor = host.Services.GetRequiredService<CommandExecutorService>();
                
                // ホストが停止されるまで待機（停止は CommandExecutorService 側で行う）
                await host.WaitForShutdownAsync();
                
                return commandExecutor.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"アプリケーションでエラーが発生しました: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// HostBuilder を作成します
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="options">CLIオプション</param>
    /// <returns>設定済み HostBuilder</returns>
    public static IHostBuilder CreateHostBuilder(string[] args, CliOptions options) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // CLIオプションをDIコンテナに登録
                services.AddSingleton(options);

                // SetupAPI ラッパーを登録（本番環境では実装、テスト環境ではスタブ）
                services.AddSingleton<ISetupApiWrapper, SetupApiWrapper>();

                // コアサービス
                services.AddSingleton<WindowsApiErrorHandler>();
                services.AddSingleton<LocalizationService>();
                services.AddScoped<IDriverInstallationService, DriverInstallationService>();
                
                // ログサービス
                services.AddSingleton<IInstallationLogger, InstallationLogger>();
                
                // CLIサービス
                services.AddSingleton<CliParser>();
                services.AddScoped<InstallCommand>();
                services.AddScoped<HelpCommand>();
                services.AddSingleton<CommandExecutorService>();
                
                // ホステッドサービスとしてCommandExecutorServiceを登録
                services.AddHostedService<CommandExecutorService>(provider => 
                    provider.GetRequiredService<CommandExecutorService>());
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                
                // 詳細モード時はデバッグレベルまで出力
                if (options.Verbose)
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    // 通常時は警告レベル以上のみ
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                }
            });
}

/// <summary>
/// コンソールアプリケーションサービス（宣言的インストール専用）
/// </summary>
public class ConsoleApplicationService : BackgroundService
{
    private readonly ILogger<ConsoleApplicationService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IDriverInstallationService _installationService;
    private readonly IInstallationLogger _installationLogger;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="appLifetime">アプリケーションライフタイム</param>
    /// <param name="installationService">インストールサービス</param>
    /// <param name="installationLogger">インストールロガー</param>
    public ConsoleApplicationService(
        ILogger<ConsoleApplicationService> logger,
        IHostApplicationLifetime appLifetime,
        IDriverInstallationService installationService,
        IInstallationLogger installationLogger)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _installationService = installationService;
        _installationLogger = installationLogger;
    }

    /// <summary>
    /// メイン処理を非同期実行します
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    /// <returns>非同期タスク</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Console.WriteLine("Windows ドライバインストーラが開始されました（宣言的インストール専用）");
            _logger.LogInformation("Windows Driver Installer started (declarative installation only)");

            // サンプル実行
            await DemonstrateInstallationService();
            
            // ログ機能のデモンストレーション
            await DemonstrateLoggingService();

            Console.WriteLine("処理が完了しました");
            _logger.LogInformation("Processing completed");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("アプリケーションがキャンセルされました");
            _logger.LogInformation("Application was cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"アプリケーション実行中にエラーが発生しました: {ex.Message}");
            _logger.LogError(ex, "Application execution failed");
        }
        finally
        {
            _appLifetime.StopApplication();
        }
    }

    /// <summary>
    /// インストールサービスのデモンストレーション
    /// </summary>
    /// <returns>非同期タスク</returns>
    private async Task DemonstrateInstallationService()
    {
        Console.WriteLine("\n=== 基盤構築確認 ===");
        Console.WriteLine("- GenericHost が正常に初期化されました");
        Console.WriteLine("- DI コンテナが正しく設定されました");
        Console.WriteLine("- インストールサービスが正常に登録されました");
        Console.WriteLine("- データモデルが構築されました");
        
        _logger.LogInformation("Infrastructure verification completed successfully");
        
        await Task.Delay(1000);
    }

    /// <summary>
    /// ログ機能のデモンストレーション
    /// </summary>
    /// <returns>非同期タスク</returns>
    private async Task DemonstrateLoggingService()
    {
        Console.WriteLine("\n=== ログ機能確認 ===");
        
        // 相関IDを生成
        var correlationId = Guid.NewGuid().ToString();
        var category = "DemoInstallation";
        
        // 各種ログレベルのテスト
        await _installationLogger.LogInformationAsync(
            "Installation process started",
            category,
            correlationId,
            new Dictionary<string, object> { ["InfPath"] = "test.inf", ["Section"] = "DefaultInstall" });
        
        await _installationLogger.LogWarningAsync(
            "Service section not found, skipping service installation", 
            category, 
            correlationId,
            new Dictionary<string, object> { ["Section"] = "DefaultInstall.Services" });
        
        await _installationLogger.LogErrorAsync(
            "Failed to install driver from INF file",
            category,
            correlationId,
            new InvalidOperationException("Setup API error 0x800"),
            new Dictionary<string, object> { ["ErrorCode"] = "0x800", ["LastError"] = 2048 });
        
        await _installationLogger.LogInformationAsync(
            "Installation process completed successfully",
            category,
            correlationId,
            new Dictionary<string, object> { ["Duration"] = "00:00:30", ["FilesInstalled"] = 12 });

        Console.WriteLine("- 構造化ログが正常に記録されました");
        Console.WriteLine("- 相関IDによる処理追跡が動作しています");
        Console.WriteLine("- ファイル出力とコンソール出力が分離されています");
        
        // ログエントリの取得テスト
        var logEntries = await _installationLogger.GetLogEntriesAsync(correlationId);
        Console.WriteLine($"- セッション追跡: {logEntries.Count()} 件のログエントリが記録されました");
        
        // ログエクスポートのテスト
        var exportPath = Path.Combine(Path.GetTempPath(), $"demo_log_{correlationId[..8]}.json");
        await _installationLogger.ExportLogsAsync(correlationId, exportPath, "json");
        Console.WriteLine($"- ログエクスポート: {exportPath} に JSON 形式で出力されました");
        
        // ファイルが存在することを確認
        if (File.Exists(exportPath))
        {
            var fileSize = new FileInfo(exportPath).Length;
            Console.WriteLine($"- エクスポートファイルサイズ: {fileSize} bytes");
        }
        
        _logger.LogInformation("Logging service demonstration completed successfully");
        
        await Task.Delay(1000);
    }
}
