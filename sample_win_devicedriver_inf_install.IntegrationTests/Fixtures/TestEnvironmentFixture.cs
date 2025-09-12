using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Services;
using sample_win_devicedriver_inf_install.Tests.Stubs;
using System.IO;
using System.Reflection;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;

/// <summary>
/// 統合テスト環境のセットアップとクリーンアップを管理するフィクスチャ
/// 注意: 実際のOS環境に影響を与えないよう、SetupAPIのスタブ実装を使用します
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
    /// 統合テスト用にスタブ実装を使用して実OS環境への影響を回避
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
                // SetupAPIスタブを使用（実OS環境に影響を与えない）
                var setupApiStub = new SetupApiStub();
                services.AddSingleton<ISetupApiWrapper>(setupApiStub);
                
                // 統合テスト時にSetupApiStubにテスト用INF構造を設定するためのアクセスを提供
                services.AddSingleton(setupApiStub);

                // コアサービスの登録
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<WindowsApiErrorHandler>();
                
                // アプリケーションサービスの登録
                services.AddSingleton<IDriverInstallationService, DriverInstallationService>();
                services.AddSingleton<IInstallationLogger, InstallationLogger>();
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
    /// リソースクラスからテストINFファイルを作成し、SetupApiStubに登録
    /// </summary>
    /// <param name="content">INFファイルの内容</param>
    /// <param name="fileName">ファイル名</param>
    /// <returns>作成されたファイルのパス</returns>
    public string CreateTestInfFile(string content, string fileName = "test_driver.inf")
    {
        var filePath = Path.Combine(TempTestDirectory, fileName);
        File.WriteAllText(filePath, content);
        
        // SetupApiStubにINF内容を登録（実際のファイル読み込みを回避）
        var setupApiStub = GetService<SetupApiStub>();
        var sections = ParseInfSections(content);
        setupApiStub.SetupInfContent(filePath, sections);
        
        return filePath;
    }
    
    /// <summary>
    /// INFファイルの内容からセクション構造を解析
    /// </summary>
    /// <param name="infContent">INFファイルの内容</param>
    /// <returns>セクション構造</returns>
    private static Dictionary<string, List<string>> ParseInfSections(string infContent)
    {
        var sections = new Dictionary<string, List<string>>();
        var currentSection = string.Empty;
        var lines = infContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';'))
                continue;
                
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                // セクション開始
                currentSection = trimmedLine[1..^1];
                sections[currentSection] = new List<string>();
            }
            else if (!string.IsNullOrEmpty(currentSection) && sections.ContainsKey(currentSection))
            {
                // セクション内のライン
                sections[currentSection].Add(trimmedLine);
            }
        }
        
        return sections;
    }
    
    /// <summary>
    /// 管理者権限が必要なテストかどうかを確認
    /// 注意: この統合テストではスタブを使用するため、実際には管理者権限は不要です
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