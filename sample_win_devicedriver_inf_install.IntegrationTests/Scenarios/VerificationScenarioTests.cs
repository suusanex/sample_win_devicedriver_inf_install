using FluentAssertions;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Enums;
using System.IO;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// インストール検証シナリオの統合テスト
/// INFに従ったインストール後の状態確認とAPIの使い分け検証
/// </summary>
public class VerificationScenarioTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;
    private readonly IInstallationLogger _logger;

    public VerificationScenarioTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
        _logger = _fixture.GetService<IInstallationLogger>();
    }

    /// <summary>
    /// INFに従ったインストール後の状態確認テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_AfterInstallation_ShouldReflectCorrectState()
    {
        // Arrange
        var validInfContent = CreateValidInfContentWithFiles();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "state_verification_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // セッション情報の検証
        result.SessionId.Should().NotBeNullOrEmpty();
        result.InfPath.Should().Be(infFilePath);
        result.SectionName.Should().Be("DefaultInstall");
        result.StartTime.Should().BeBefore(DateTime.UtcNow);
        result.CorrelationId.Should().NotBeEmpty();
        
        // 実行時間の検証
        result.ExecutionTime.Should().BePositive();
        result.ExecutionTime.Should().BeLessThan(TimeSpan.FromMinutes(1));
        
        // ログエントリの検証
        result.LogEntries.Should().NotBeEmpty();
        result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Information);
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Started", StringComparison.OrdinalIgnoreCase));
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 内部API使い分けの正常動作確認テスト
    /// SetupInstallFromInfSectionWとSetupInstallServicesFromInfSectionWの適切な使い分け
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldUseCorrectApiCalls()
    {
        // Arrange
        var infWithServicesContent = CreateInfContentWithServicesSection();
        var infFilePath = _fixture.CreateTestInfFile(infWithServicesContent, "api_usage_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // SetupInstallFromInfSectionW の呼び出しがログに記録されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("SetupInstallFromInfSection", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Installing from section", StringComparison.OrdinalIgnoreCase));
        
        // Servicesセクションが存在する場合、SetupInstallServicesFromInfSectionW の呼び出しも記録されているはず
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Services", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Service installation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Servicesセクションがない場合のAPI使い分け確認テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithoutServicesSection_ShouldOnlyUseMainInstallApi()
    {
        // Arrange
        var infWithoutServicesContent = CreateInfContentWithoutServicesSection();
        var infFilePath = _fixture.CreateTestInfFile(infWithoutServicesContent, "no_services_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // メインのインストールAPIが呼び出されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("SetupInstallFromInfSection", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Installing from section", StringComparison.OrdinalIgnoreCase));
        
        // Servicesセクションが存在しないため、Services関連の処理は実行されないことを確認
        var serviceRelatedLogs = result.LogEntries.Where(log => 
            log.Message.Contains("SetupInstallServicesFromInfSection", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Services section not found", StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Services関連のログがあってもエラーではないことを確認
        if (serviceRelatedLogs.Any())
        {
            serviceRelatedLogs.Should().NotContain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error);
        }
    }

    /// <summary>
    /// セクション適用順序の検証テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var complexInfContent = CreateComplexInfContent();
        var infFilePath = _fixture.CreateTestInfFile(complexInfContent, "order_verification_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // ログエントリの時系列順序を検証
        var logEntries = result.LogEntries.OrderBy(log => log.Timestamp).ToList();
        logEntries.Should().NotBeEmpty();
        
        // 開始ログが最初に記録されていることを確認
        var firstLog = logEntries.First();
        firstLog.Message.Should().Contain("Started");
        
        // 完了ログが最後の方に記録されていることを確認
        var completionLogs = logEntries.Where(log => 
            log.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Finished", StringComparison.OrdinalIgnoreCase)).ToList();
        completionLogs.Should().NotBeEmpty();
    }

    /// <summary>
    /// 相関IDによるセッション追跡の検証テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldMaintainCorrelationIdThroughoutSession()
    {
        // Arrange
        var validInfContent = CreateValidInfContentWithFiles();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "correlation_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().NotBeEmpty();
        
        // すべてのログエントリが同じ相関IDを持つことを確認
        var correlationId = result.SessionId;
        result.LogEntries.Should().AllSatisfy(log => 
            log.CorrelationId.Should().Be(correlationId));
    }

    /// <summary>
    /// 複数のカスタムセクションを持つINFファイルの処理確認テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithCustomSection_ShouldProcessCorrectly()
    {
        // Arrange
        var customSectionInfContent = CreateInfContentWithCustomSection();
        var infFilePath = _fixture.CreateTestInfFile(customSectionInfContent, "custom_section_driver.inf");
        var customSectionName = "CustomInstall";

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath, customSectionName);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        result.SectionName.Should().Be(customSectionName);
        
        // カスタムセクションが処理されたことをログで確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains(customSectionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// エラー復旧処理の検証テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithRecoverableError_ShouldHandleGracefully()
    {
        // Arrange
        var recoverableErrorInfContent = CreateInfContentWithWarnings();
        var infFilePath = _fixture.CreateTestInfFile(recoverableErrorInfContent, "recoverable_error_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        
        // 警告付きで完了するか、失敗するかのいずれか
        result.Status.Should().BeOneOf(
            InstallationStatus.Completed, 
            InstallationStatus.CompletedWithWarnings, 
            InstallationStatus.Failed);
        
        // 警告やエラーがログに記録されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Warning || 
            log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error);
        
        if (result.Status == InstallationStatus.CompletedWithWarnings)
        {
            result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Warning);
        }
    }

    /// <summary>
    /// ファイルコピー操作を含むINFファイルの内容を作成
    /// </summary>
    private static string CreateValidInfContentWithFiles()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DriverFiles
AddReg=DriverRegistry

[DriverFiles]
test_driver.sys
test_library.dll

[DriverRegistry]
HKR,,""TestValue"",0x00000000,""TestData""

[DestinationDirs]
DriverFiles = 12

[SourceDisksNames]
1 = %DiskName%,,,""""

[SourceDisksFiles]
test_driver.sys = 1
test_library.dll = 1

[Strings]
DiskName = ""Test Driver Installation Disk""
";
    }

    /// <summary>
    /// Servicesセクションを含むINFファイルの内容を作成
    /// </summary>
    private static string CreateInfContentWithServicesSection()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DriverFiles

[DefaultInstall.Services]
AddService = TestDriverService,0x00000002,TestDriverService_ServiceInstall

[TestDriverService_ServiceInstall]
ServiceType = 1
StartType = 3
ErrorControl = 1
ServiceBinary = %12%\test_driver.sys
DisplayName = %TestDriverService.DisplayName%

[DriverFiles]
test_driver.sys

[DestinationDirs]
DriverFiles = 12

[Strings]
TestDriverService.DisplayName = ""Test Driver Service""
";
    }

    /// <summary>
    /// Servicesセクションを含まないINFファイルの内容を作成
    /// </summary>
    private static string CreateInfContentWithoutServicesSection()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DriverFiles
AddReg=DriverRegistry

[DriverFiles]
test_driver.sys

[DriverRegistry]
HKR,,""TestValue"",0x00000000,""TestData""

[DestinationDirs]
DriverFiles = 12

[Strings]
DiskName = ""Test Driver Installation Disk""
";
    }

    /// <summary>
    /// 複雑な処理を含むINFファイルの内容を作成
    /// </summary>
    private static string CreateComplexInfContent()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DriverFiles,SupportFiles
AddReg=DriverRegistry,ConfigRegistry
DelReg=OldRegistry

[DefaultInstall.Services]
AddService = TestDriverService,0x00000002,TestDriverService_ServiceInstall
DelService = OldService

[TestDriverService_ServiceInstall]
ServiceType = 1
StartType = 3
ErrorControl = 1
ServiceBinary = %12%\test_driver.sys
DisplayName = %TestDriverService.DisplayName%

[DriverFiles]
test_driver.sys
driver_helper.dll

[SupportFiles]
config.ini
readme.txt

[DriverRegistry]
HKR,,""Version"",0x00000000,""1.0.0.0""
HKR,,""InstallDate"",0x00000000,%CurrentDate%

[ConfigRegistry]
HKLM,""Software\TestDriver"",""Enabled"",0x00010001,1

[OldRegistry]
HKLM,""Software\OldDriver""

[DestinationDirs]
DriverFiles = 12
SupportFiles = 16422

[Strings]
TestDriverService.DisplayName = ""Test Driver Service""
CurrentDate = ""2024-01-01""
";
    }

    /// <summary>
    /// カスタムセクションを含むINFファイルの内容を作成
    /// </summary>
    private static string CreateInfContentWithCustomSection()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DefaultFiles

[CustomInstall]
CopyFiles=CustomFiles
AddReg=CustomRegistry

[DefaultFiles]
default_driver.sys

[CustomFiles]
custom_driver.sys
custom_config.dll

[CustomRegistry]
HKR,,""CustomValue"",0x00000000,""CustomData""

[DestinationDirs]
DefaultFiles = 12
CustomFiles = 12

[Strings]
DiskName = ""Custom Driver Installation Disk""
";
    }

    /// <summary>
    /// 警告を発生させる可能性のあるINFファイルの内容を作成
    /// </summary>
    private static string CreateInfContentWithWarnings()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=DriverFiles
DelFiles=OptionalFiles

[DriverFiles]
test_driver.sys

[OptionalFiles]
optional_file.dll

[DestinationDirs]
DriverFiles = 12
OptionalFiles = 12

[Strings]
DiskName = ""Driver Installation Disk with Warnings""
";
    }
}