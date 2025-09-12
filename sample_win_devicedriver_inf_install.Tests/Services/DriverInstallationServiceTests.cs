using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Services;
using sample_win_devicedriver_inf_install.Tests.Stubs;
using sample_win_devicedriver_inf_install.Enums;
using System.Globalization;

namespace sample_win_devicedriver_inf_install.Tests.Services;

/// <summary>
/// DriverInstallationServiceのUnitTest
/// 実際のOS環境に影響を与えずにテストを実行
/// </summary>
public class DriverInstallationServiceTests : IDisposable
{
    private readonly ILogger<DriverInstallationService> _logger;
    private readonly ILogger<InstallationLogger> _installationLoggerLogger;
    private readonly InstallationLogger _installationLogger;
    private readonly LocalizationService _localizationService;
    private readonly SetupApiStub _setupApiStub;
    private readonly WindowsApiErrorHandler _errorHandler;
    private readonly DriverInstallationService _driverInstallationService;
    private readonly string _testInfPath;
    private readonly string _tempDirectory;

    public DriverInstallationServiceTests()
    {
        // テスト用のロガーを作成
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<DriverInstallationService>();
        _installationLoggerLogger = loggerFactory.CreateLogger<InstallationLogger>();

        // テスト用のサービスを作成
        _installationLogger = new InstallationLogger(_installationLoggerLogger);
        _localizationService = new LocalizationService(CultureInfo.GetCultureInfo("ja-JP"));
        _setupApiStub = new SetupApiStub();
        _errorHandler = new WindowsApiErrorHandler(_localizationService, _setupApiStub);
        _driverInstallationService = new DriverInstallationService(_logger, _installationLogger, _errorHandler, _setupApiStub);

        // テスト用の一時ディレクトリとファイルを作成
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DriverInstallationServiceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _testInfPath = Path.Combine(_tempDirectory, "test_driver.inf");
        
        // テスト用のINFファイル内容を作成
        var infContent = @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=TestFiles

[DefaultInstall.Services]
AddService=TestService,0x00000002,TestService.Install

[TestFiles]
test.sys

[TestService.Install]
ServiceType=1
StartType=3
ErrorControl=1
ServiceBinary=%12%\test.sys
";
        File.WriteAllText(_testInfPath, infContent);

        // SetupApiStubにINF内容を設定
        var sections = new Dictionary<string, List<string>>
        {
            ["Version"] = new() { "Signature=\"$WINDOWS NT$\"", "Class=Sample" },
            ["DefaultInstall"] = new() { "CopyFiles=TestFiles" },
            ["DefaultInstall.Services"] = new() { "AddService=TestService,0x00000002,TestService.Install" },
            ["TestFiles"] = new() { "test.sys" },
            ["TestService.Install"] = new() { "ServiceType=1", "StartType=3" }
        };
        _setupApiStub.SetupInfContent(_testInfPath, sections);
    }

    /// <summary>
    /// 正常なINFファイルからのインストールテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithValidInf_ShouldSucceed()
    {
        // Arrange
        _setupApiStub.Reset();
        _setupApiStub.SetupInfContent(_testInfPath, new Dictionary<string, List<string>>
        {
            ["Version"] = new() { "Signature=\"$WINDOWS NT$\"" },
            ["DefaultInstall"] = new() { "CopyFiles=TestFiles" }
        });

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(_testInfPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SessionId);
        Assert.Equal(InstallationStatus.Completed, result.Status);
        Assert.Equal(_testInfPath, result.InfPath);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.LogEntries);

        // ログエントリの検証
        var logEntries = result.LogEntries.ToList();
        Assert.Contains(logEntries, entry => entry.Message.Contains("Started declarative installation"));
        Assert.Contains(logEntries, entry => entry.Message.Contains("Installing from section"));
        Assert.Contains(logEntries, entry => entry.Message.Contains("Completed declarative installation"));
    }

    /// <summary>
    /// 存在しないINFファイルのテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithNonExistentFile_ShouldFail()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.inf");

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(nonExistentPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Failed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorInfo);
        Assert.Contains("not found", result.ErrorInfo?.SystemMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 無効なINFファイル（[Version]セクションなし）のテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithInvalidInf_ShouldFail()
    {
        // Arrange
        var invalidInfPath = Path.Combine(_tempDirectory, "invalid.inf");
        File.WriteAllText(invalidInfPath, "[SomeSection]\nkey=value");

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(invalidInfPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Failed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorInfo);
        Assert.Contains("Version", result.ErrorInfo?.SystemMessage ?? "");
    }

    /// <summary>
    /// セクションが見つからない場合のテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithMissingSection_ShouldFail()
    {
        // Arrange
        _setupApiStub.Reset();
        _setupApiStub.SetupInfContent(_testInfPath, new Dictionary<string, List<string>>
        {
            ["Version"] = new() { "Signature=\"$WINDOWS NT$\"" }
            // DefaultInstallセクションを意図的に削除
        });

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(_testInfPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Failed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorInfo);
    }

    /// <summary>
    /// カスタムセクション名でのインストールテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithCustomSection_ShouldSucceed()
    {
        // Arrange
        var customSectionName = "CustomInstall";
        _setupApiStub.Reset();
        _setupApiStub.SetupInfContent(_testInfPath, new Dictionary<string, List<string>>
        {
            ["Version"] = new() { "Signature=\"$WINDOWS NT$\"" },
            [customSectionName] = new() { "CopyFiles=TestFiles" }
        });

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(_testInfPath, customSectionName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Completed, result.Status);
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Servicesセクションがある場合のテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithServicesSection_ShouldInstallServices()
    {
        // Arrange
        _setupApiStub.Reset();
        _setupApiStub.SetupInfContent(_testInfPath, new Dictionary<string, List<string>>
        {
            ["Version"] = new() { "Signature=\"$WINDOWS NT$\"" },
            ["DefaultInstall"] = new() { "CopyFiles=TestFiles" },
            ["DefaultInstall.Services"] = new() { "AddService=TestService" }
        });

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(_testInfPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Completed, result.Status);
        Assert.True(result.IsSuccess);

        var logEntries = result.LogEntries.ToList();
        Assert.Contains(logEntries, entry => entry.Message.Contains("Found Services section"));
        Assert.Contains(logEntries, entry => entry.Message.Contains("Successfully installed services"));
    }

    /// <summary>
    /// セッション管理のテスト
    /// </summary>
    [Fact]
    public void CreateSession_ShouldCreateValidSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = Guid.NewGuid().ToString(),
            InfPath = _testInfPath,
            Name = "TestDriver"
        };

        // Act
        var session = _driverInstallationService.CreateSession(driverPackage);

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal(driverPackage.Id, session.DriverPackage.Id);
        Assert.Equal(InstallationStatus.Pending, session.Status);

        // セッションが管理されていることを確認
        var retrievedSession = _driverInstallationService.GetSession(session.SessionId);
        Assert.NotNull(retrievedSession);
        Assert.Equal(session.SessionId, retrievedSession.SessionId);
    }

    /// <summary>
    /// アクティブセッション一覧取得のテスト
    /// </summary>
    [Fact]
    public void GetActiveSessions_ShouldReturnAllActiveSessions()
    {
        // Arrange
        var package1 = new DriverPackage { Id = "1", InfPath = "test1.inf", Name = "Driver1" };
        var package2 = new DriverPackage { Id = "2", InfPath = "test2.inf", Name = "Driver2" };

        // Act
        var session1 = _driverInstallationService.CreateSession(package1);
        var session2 = _driverInstallationService.CreateSession(package2);
        var activeSessions = _driverInstallationService.GetActiveSessions().ToList();

        // Assert
        Assert.Contains(activeSessions, s => s.SessionId == session1.SessionId);
        Assert.Contains(activeSessions, s => s.SessionId == session2.SessionId);
        Assert.True(activeSessions.Count >= 2);
    }

    /// <summary>
    /// 存在しないセッション取得のテスト
    /// </summary>
    [Fact]
    public void GetSession_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var session = _driverInstallationService.GetSession(nonExistentId);

        // Assert
        Assert.Null(session);
    }

    /// <summary>
    /// キャンセレーション処理のテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // 即座にキャンセル

        // Act
        var result = await _driverInstallationService.InstallFromInfAsync(_testInfPath, cancellationToken: cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InstallationStatus.Cancelled, result.Status);
        Assert.False(result.IsSuccess);
    }

    public void Dispose()
    {
        _installationLogger?.Dispose();
        
        // テスト用ディレクトリの削除
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // 削除に失敗してもテストは続行
            }
        }
    }
}