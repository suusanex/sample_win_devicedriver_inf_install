using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Core.Contracts;
using sample_win_devicedriver_inf_install.Core.Services;
using sample_win_devicedriver_inf_install.Services;
using sample_win_devicedriver_inf_install.UI.Services;

namespace sample_win_devicedriver_inf_install;

/// <summary>
/// メインプログラムクラス（UI層 - FR-014準拠日本語インターフェース）
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
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
            return 0;
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
    /// <returns>設定済み HostBuilder</returns>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // コア層サービス（FR-014により英語のみ）
                services.AddSingleton<WindowsApiErrorHandler>();
                services.AddSingleton<DriverStatusService>();
                services.AddSingleton<IDriverInstallationService, DriverInstallationService>();
                
                // UI層サービス（FR-014により日本語インターフェース）
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<ErrorMessageProvider>();
                
                services.AddHostedService<ConsoleApplicationService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
#if DEBUG
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
#endif
            });
}

/// <summary>
/// コンソールアプリケーションサービス（UI層 - FR-014準拠日本語インターフェース）
/// ユーザーインターフェース関連とローカライズされたメッセージング を処理します
/// </summary>
public class ConsoleApplicationService : BackgroundService
{
    private readonly ILogger<ConsoleApplicationService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly WindowsApiErrorHandler _coreErrorHandler;
    private readonly ErrorMessageProvider _uiErrorProvider;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="appLifetime">アプリケーションライフタイム</param>
    /// <param name="coreErrorHandler">コアエラーハンドラー</param>
    /// <param name="uiErrorProvider">UIエラープロバイダー</param>
    public ConsoleApplicationService(
        ILogger<ConsoleApplicationService> logger,
        IHostApplicationLifetime appLifetime,
        WindowsApiErrorHandler coreErrorHandler,
        ErrorMessageProvider uiErrorProvider)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _coreErrorHandler = coreErrorHandler;
        _uiErrorProvider = uiErrorProvider;
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
            Console.WriteLine("Windows ドライバインストーラが開始されました");
            _logger.LogInformation("Windows Driver Installer started"); // コアログは英語

            // FR-014準拠実証: コア層は英語、UI層は日本語
            await DemonstrateLayerSeparation();

            Console.WriteLine("処理が完了しました");
            _logger.LogInformation("Processing completed"); // コアログは英語
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
    /// FR-014に従った適切な層分離を実証します
    /// コア層: 英語のみの技術データ
    /// UI層: 日本語ユーザーメッセージ
    /// </summary>
    /// <returns>非同期タスク</returns>
    private async Task DemonstrateLayerSeparation()
    {
        Console.WriteLine("\n=== FR-014 レイヤー分離のデモンストレーション ===");

        // コア層が技術的エラー情報を生成（英語のみ）
        var coreErrorInfo = _coreErrorHandler.CreateApiErrorInfo(5, "InstallDriver", "Test scenario");
        
        // コア層が技術情報を英語でログ出力（FR-014要件）
        _logger.LogError("Core Error: {LogMessage}", coreErrorInfo.GetLogMessage());
        
        // UI層がコアエラー情報から日本語ユーザーメッセージを生成
        var japaneseMessage = _uiErrorProvider.GetLocalizedErrorMessage(coreErrorInfo);
        
        // UI層がユーザーに日本語メッセージを表示
        Console.WriteLine($"ユーザー向けエラーメッセージ: {japaneseMessage}");
        
        // 日本語でトラブルシューティングガイダンスを表示
        var guidance = _uiErrorProvider.GetTroubleshootingGuidance(coreErrorInfo);
        Console.WriteLine($"\nトラブルシューティング:\n{guidance}");

        Console.WriteLine("\n=== レイヤー分離確認 ===");
        Console.WriteLine($"Core層技術情報 (英語): {coreErrorInfo.TechnicalDetails}");
        Console.WriteLine($"UI層ユーザーメッセージ (日本語): {japaneseMessage}");
        
        await Task.Delay(1000);
    }
}
