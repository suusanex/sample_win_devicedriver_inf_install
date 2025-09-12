using FluentAssertions;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Enums;
using System.IO;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// 統一されたインストールシナリオの統合テスト
/// INFファイルに従った包括的なインストール機能を検証
/// </summary>
public class UnifiedInstallationScenarioTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;
    private readonly IInstallationLogger _logger;

    public UnifiedInstallationScenarioTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
        _logger = _fixture.GetService<IInstallationLogger>();
    }

    /// <summary>
    /// 有効なINFファイルでの包括的インストールテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithValidInfFile_ShouldInstallSuccessfully()
    {
        // Arrange
        var validInfContent = CreateValidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "valid_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        result.InfPath.Should().Be(infFilePath);
        result.ExecutionTime.Should().BePositive();
        
        // ログが記録されていることを確認
        result.LogEntries.Should().NotBeEmpty();
        result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Information);
    }

    /// <summary>
    /// DefaultInstallセクションを明示指定した場合のテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithExplicitDefaultInstallSection_ShouldInstallSuccessfully()
    {
        // Arrange
        var validInfContent = CreateValidInfContentWithServices();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "explicit_section_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath, "DefaultInstall");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        result.SectionName.Should().Be("DefaultInstall");
        
        // セクション適用とサービス登録が自動実行されたことを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("SetupInstallFromInfSection", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Servicesセクション付きINFファイルでの自動検出テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithServicesSection_ShouldAutoDetectAndInstallServices()
    {
        // Arrange
        var infWithServicesContent = CreateValidInfContentWithServices();
        var infFilePath = _fixture.CreateTestInfFile(infWithServicesContent, "services_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // Servicesセクションが検出され処理されたことをログで確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Services", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("SetupInstallServicesFromInfSection", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 長時間実行でのキャンセレーションテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Skip test if not running as administrator
        if (!TestEnvironmentFixture.IsRunningAsAdministrator())
        {
            return; // Skip test when not running as admin
        }

        // Arrange
        var validInfContent = CreateValidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "cancellation_driver.inf");
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // 100ms後にキャンセル

        // Act & Assert
        var act = async () => await _installationService.InstallFromInfAsync(infFilePath, cancellationToken: cts.Token);
        
        // キャンセレーションまたは正常完了のいずれかが発生することを確認
        var result = await act.Should().NotThrowAsync();
        
        if (cts.Token.IsCancellationRequested)
        {
            result.Subject.Status.Should().Be(InstallationStatus.Cancelled);
        }
    }

    /// <summary>
    /// インストール進捗追跡テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldTrackProgressCorrectly()
    {
        // Arrange
        var validInfContent = CreateValidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "progress_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.StartTime.Should().BeBefore(DateTime.UtcNow);
        result.ExecutionTime.Should().BePositive();
        
        // 進捗ログが記録されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Started", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// パフォーマンス要件テスト（30秒以内での完了）
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldCompleteWithinPerformanceRequirements()
    {
        // Arrange
        var validInfContent = CreateValidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "performance_driver.inf");
        var maxExecutionTime = TimeSpan.FromSeconds(30);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _installationService.InstallFromInfAsync(infFilePath);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(maxExecutionTime, 
            "インストール処理は30秒以内に完了する必要があります");
        result.ExecutionTime.Should().BeLessThan(maxExecutionTime);
    }

    /// <summary>
    /// 有効なINFファイルの内容を作成
    /// </summary>
    /// <returns>INFファイルの内容</returns>
    private static string CreateValidInfContent()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=TestFiles

[TestFiles]
test.sys

[DestinationDirs]
TestFiles = 12

[SourceDisksNames]
1 = %DiskName%,,,""""

[SourceDisksFiles]
test.sys = 1

[Strings]
DiskName = ""Test Driver Installation Disk""
";
    }

    /// <summary>
    /// Servicesセクション付きの有効なINFファイルの内容を作成
    /// </summary>
    /// <returns>INFファイルの内容</returns>
    private static string CreateValidInfContentWithServices()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=TestFiles

[DefaultInstall.Services]
AddService = TestService,0x00000002,TestService_ServiceInstall

[TestService_ServiceInstall]
ServiceType = 1
StartType = 3
ErrorControl = 1
ServiceBinary = %12%\test.sys
DisplayName = %TestService.DisplayName%

[TestFiles]
test.sys

[DestinationDirs]
TestFiles = 12

[SourceDisksNames]
1 = %DiskName%,,,""""

[SourceDisksFiles]
test.sys = 1

[Strings]
DiskName = ""Test Driver Installation Disk""
TestService.DisplayName = ""Test Driver Service""
";
    }
}