using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Services;
using sample_win_devicedriver_inf_install.Tests.Stubs;
using Xunit;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// 基本統合テスト（3段階実行対応）
/// </summary>
[Collection("TestEnvironment")]
public class BasicIntegrationTests
{
    private readonly TestEnvironmentFixture _fixture;

    public BasicIntegrationTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InstallFromInfAsync_WithValidInf_ShouldComplete3PhaseInstallationSuccessfully()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var installationLogger = serviceProvider.GetRequiredService<IInstallationLogger>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        var sectionName = "DefaultInstall";

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath, sectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.ExecutionTime.Should().BePositive();
        result.SectionName.Should().Be(sectionName);
        result.InstalledPackage.Should().NotBeNull();
        result.InstalledPackage!.InfPath.Should().Be(infPath);

        // 3段階実行のログ確認
        var logEntries = await installationLogger.GetLogEntriesAsync(result.SessionId, CancellationToken.None);
        logEntries.Should().NotBeEmpty();

        // 各段階のログが記録されていることを確認
        var fileOperationLogs = logEntries.Where(e => e.Category == "FileOperation").ToList();
        var registryOperationLogs = logEntries.Where(e => e.Category == "RegistryOperation").ToList();
        var serviceOperationLogs = logEntries.Where(e => e.Category == "ServiceOperation").ToList();

        fileOperationLogs.Should().NotBeEmpty("File operations (CopyFiles) should be logged");
        registryOperationLogs.Should().NotBeEmpty("Registry operations (AddReg) should be logged");
        serviceOperationLogs.Should().NotBeEmpty("Service operations should be logged");

        // 開始ログの確認
        var startLog = logEntries.FirstOrDefault(e => e.Message.Contains("Started declarative installation"));
        startLog.Should().NotBeNull();

        // 完了ログの確認
        var completionLog = logEntries.FirstOrDefault(e => e.Message.Contains("3-phase declarative installation"));
        completionLog.Should().NotBeNull();
    }

    [Fact]
    public async Task InstallFromInfAsync_WithInvalidInfPath_ShouldFailWithAppropriateError()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        
        var invalidInfPath = "nonexistent_file.inf";
        var sectionName = "DefaultInstall";

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(invalidInfPath, sectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo.Should().NotBeNull();
        
        var errorInfo = result.ErrorInfo.Value;
        errorInfo.ErrorCode.Should().NotBe(0u);
        errorInfo.SystemMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InstallFromInfAsync_WithInvalidSection_ShouldFailWithSectionNotFoundError()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        var invalidSectionName = "InvalidSection";

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath, invalidSectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo.Should().NotBeNull();
        
        var errorInfo = result.ErrorInfo.Value;
        errorInfo.SystemMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task InstallFromInfAsync_ShouldCreateAndManageSessionCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath);

        // Assert
        result.SessionId.Should().NotBeNullOrEmpty();
        
        // セッションが作成されていることを確認
        var session = driverInstallationService.GetSession(result.SessionId);
        session.Should().NotBeNull();
        session!.SessionId.Should().Be(result.SessionId);
        session.Status.Should().Be(InstallationStatus.Completed);
        
        // アクティブセッション一覧に含まれることを確認
        var activeSessions = driverInstallationService.GetActiveSessions();
        activeSessions.Should().Contain(s => s.SessionId == result.SessionId);
    }

    [Fact]
    public async Task InstallFromInfAsync_WithCancellation_ShouldHandleCancellationCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        // 即座にキャンセルしてキャンセレーション処理を確認
        cts.Cancel();
        
        var result = await driverInstallationService.InstallFromInfAsync(infPath, cancellationToken: cts.Token);
        
        // キャンセルの場合、結果はキャンセル状態になる
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Cancelled);
    }

    [Fact]
    public async Task InstallFromInfAsync_WithServicesSection_ShouldProcessServicesCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var installationLogger = serviceProvider.GetRequiredService<IInstallationLogger>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        var sectionName = "DefaultInstall"; // この実装では .Services セクションも自動検出される

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath, sectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        // Services 段階のログが記録されていることを確認
        var logEntries = await installationLogger.GetLogEntriesAsync(result.SessionId, CancellationToken.None);
        var servicesLogs = logEntries.Where(e => e.Category == "ServiceOperation").ToList();
        servicesLogs.Should().NotBeEmpty();

        // Services セクション処理のログメッセージを確認
        var servicesFoundLog = servicesLogs.FirstOrDefault(e => e.Message.Contains("Services section"));
        servicesFoundLog.Should().NotBeNull();
    }

    [Fact]
    public async Task InstallFromInfAsync_ShouldLog3PhaseExecutionSteps()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var installationLogger = serviceProvider.GetRequiredService<IInstallationLogger>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var logEntries = await installationLogger.GetLogEntriesAsync(result.SessionId, CancellationToken.None);
        
        // 各段階のログが正しい順序で記録されていることを確認
        var orderedLogs = logEntries.OrderBy(e => e.Timestamp).ToList();
        
        // ファイル操作段階のログを探す
        var fileOpStartIndex = orderedLogs.FindIndex(e => e.Message.Contains("file operations") && e.Message.Contains("Starting"));
        var fileOpCompleteIndex = orderedLogs.FindIndex(e => e.Message.Contains("file operations") && e.Message.Contains("completed"));
        
        // レジストリ操作段階のログを探す
        var registryOpStartIndex = orderedLogs.FindIndex(e => e.Message.Contains("registry operations") && e.Message.Contains("Starting"));
        var registryOpCompleteIndex = orderedLogs.FindIndex(e => e.Message.Contains("registry operations") && e.Message.Contains("completed"));
        
        // サービス操作段階のログを探す
        var serviceOpIndex = orderedLogs.FindIndex(e => e.Category == "ServiceOperation");

        // 3段階が正しい順序で実行されていることを確認
        fileOpStartIndex.Should().BeGreaterOrEqualTo(0, "File operation start should be logged");
        fileOpCompleteIndex.Should().BeGreaterThan(fileOpStartIndex, "File operation completion should come after start");
        registryOpStartIndex.Should().BeGreaterThan(fileOpCompleteIndex, "Registry operations should come after file operations");
        registryOpCompleteIndex.Should().BeGreaterThan(registryOpStartIndex, "Registry operation completion should come after start");
        serviceOpIndex.Should().BeGreaterThan(registryOpCompleteIndex, "Service operations should come last");
    }
}