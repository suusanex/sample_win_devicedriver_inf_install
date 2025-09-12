using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Services;
using System.IO;
using System.Reflection;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;

/// <summary>
/// 統合テスト環境のセットアップとクリーンアップを管理するフィクスチャ
/// </summary>
public class TestEnvironmentFixture : IDisposable
{
    public IHost Host { get; private set; } = null!;
    public string TestDataDirectory { get; private set; } = null!;
    public string TempTestDirectory { get; private set; } = null!;
    
    /// <summary>
    /// テスト環境の初期化
    /// </summary>
    public TestEnvironmentFixture()
    {
        SetupTestDirectories();
        SetupHost();
        CopyTestData();
    }
    
    /// <summary>
    /// サービスプロバイダーからサービスを取得
    /// </summary>
    /// <typeparam name="T">取得するサービスの型</typeparam>
    /// <returns>サービスインスタンス</returns>
    public T GetService<T>() where T : notnull
    {
        return Host.Services.GetRequiredService<T>();
    }
    
    /// <summary>
    /// テストディレクトリのセットアップ
    /// </summary>
    private void SetupTestDirectories()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var testProjectDirectory = Path.GetDirectoryName(assemblyLocation)!;
        
        // テストデータディレクトリの特定
        TestDataDirectory = Path.Combine(testProjectDirectory, "TestData");
        
        // 一時テストディレクトリの作成
        TempTestDirectory = Path.Combine(Path.GetTempPath(), $"DeviceDriverInstaller_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempTestDirectory);
    }
    
    /// <summary>
    /// DIコンテナとホストのセットアップ
    /// </summary>
    private void SetupHost()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.FormatterName = ConsoleFormatterNames.Simple;
                });
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((context, services) =>
            {
                // サービスの登録
                services.AddSingleton<IDriverInstallationService, DriverInstallationService>();
                services.AddSingleton<IInstallationLogger, InstallationLogger>();
                services.AddSingleton<WindowsApiErrorHandler>();
            });
            
        Host = hostBuilder.Build();
    }
    
    /// <summary>
    /// テストデータのコピー
    /// </summary>
    private void CopyTestData()
    {
        var sampleDriversSource = Path.Combine("..", "..", "..", "..", "TestData");
        var sampleDriversTarget = Path.Combine(TempTestDirectory, "SampleDrivers");
        
        if (Directory.Exists(sampleDriversSource))
        {
            CopyDirectory(sampleDriversSource, sampleDriversTarget);
        }
    }
    
    /// <summary>
    /// ディレクトリの再帰的コピー
    /// </summary>
    /// <param name="sourceDir">コピー元ディレクトリ</param>
    /// <param name="targetDir">コピー先ディレクトリ</param>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(dir, targetSubDir);
        }
    }
    
    /// <summary>
    /// リソースクラスからテストINFファイルを作成
    /// </summary>
    /// <param name="content">INFファイルの内容</param>
    /// <param name="fileName">ファイル名</param>
    /// <returns>作成されたファイルのパス</returns>
    public string CreateTestInfFile(string content, string fileName = "test_driver.inf")
    {
        var filePath = Path.Combine(TempTestDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// 管理者権限が必要なテストかどうかを確認
    /// </summary>
    /// <returns>管理者権限で実行されている場合はtrue</returns>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// リソースの解放
    /// </summary>
    public void Dispose()
    {
        Host?.Dispose();
        
        if (Directory.Exists(TempTestDirectory))
        {
            try
            {
                Directory.Delete(TempTestDirectory, true);
            }
            catch
            {
                // テスト後のクリーンアップ失敗は無視
            }
        }
        
        GC.SuppressFinalize(this);
    }
}