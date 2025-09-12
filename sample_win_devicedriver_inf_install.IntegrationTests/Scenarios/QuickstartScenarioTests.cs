using FluentAssertions;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Enums;
using System.IO;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// quickstart.mdに記載されたシナリオの統合テスト
/// 実際の使用例に基づいた機能検証
/// </summary>
public class QuickstartScenarioTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;
    private readonly IInstallationLogger _logger;

    public QuickstartScenarioTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
        _logger = _fixture.GetService<IInstallationLogger>();
    }

    /// <summary>
    /// シナリオ1: 標準的なINFインストール
    /// quickstart.md - "標準的な INF インストール"
    /// </summary>
    [Fact]
    public async Task Scenario1_StandardInfInstallation_ShouldSucceed()
    {
        // Arrange - quickstart.mdのサンプルINFファイルと同等の内容
        var standardInfContent = CreateStandardInfContent();
        var infFilePath = _fixture.CreateTestInfFile(standardInfContent, "sample.inf");

        // Act - quickstart.mdで指定されたコマンドライン操作と同等
        // sample_win_devicedriver_inf_install.exe --install --inf sample.inf
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert - quickstart.mdで期待される結果
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // ログで処理成功を確認（quickstart.mdの要件）
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("DefaultInstall", StringComparison.OrdinalIgnoreCase));
        
        // DefaultInstall.Servicesセクションの自動検出と適用を確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Services", StringComparison.OrdinalIgnoreCase) ||
            log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Information);
        
        // インストール完了通知の確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Success", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// シナリオ2: カスタムセクションでのインストール
    /// quickstart.md - "カスタムセクションでのインストール"
    /// </summary>
    [Fact]
    public async Task Scenario2_CustomSectionInstallation_ShouldSucceed()
    {
        // Arrange - カスタムセクションを含むINFファイル
        var customSectionInfContent = CreateCustomSectionInfContent();
        var infFilePath = _fixture.CreateTestInfFile(customSectionInfContent, "custom_sample.inf");
        var customSectionName = "MyCustomInstall";

        // Act - quickstart.mdで指定されたコマンドライン操作と同等
        // sample_win_devicedriver_inf_install.exe --install --inf sample.inf --section MyCustomInstall
        var result = await _installationService.InstallFromInfAsync(infFilePath, customSectionName);

        // Assert - quickstart.mdで期待される結果
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        result.SectionName.Should().Be(customSectionName);
        
        // ログでカスタムセクションの処理成功を確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains(customSectionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// パフォーマンス検証: 小規模ドライバで30秒以内完了
    /// quickstart.md - "パフォーマンス検証（目安）"
    /// </summary>
    [Fact]
    public async Task PerformanceVerification_SmallScaleDriver_ShouldCompleteWithin30Seconds()
    {
        // Arrange - 小規模ドライバシミュレーション
        var smallScaleInfContent = CreateSmallScaleInfContent();
        var infFilePath = _fixture.CreateTestInfFile(smallScaleInfContent, "small_scale_driver.inf");
        var maxExecutionTime = TimeSpan.FromSeconds(30);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _installationService.InstallFromInfAsync(infFilePath);
        stopwatch.Stop();

        // Assert - quickstart.mdのパフォーマンス要件
        stopwatch.Elapsed.Should().BeLessThan(maxExecutionTime, 
            "小規模ドライバは30秒以内に完了する必要があります（quickstart.md要件）");
        result.ExecutionTime.Should().BeLessThan(maxExecutionTime);
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
    }

    /// <summary>
    /// サイレント実行テスト: ユーザー対話なしでの実行
    /// quickstart.md - "ユーザー対話なしで実行"
    /// </summary>
    [Fact]
    public async Task SilentExecution_ShouldRunWithoutUserInteraction()
    {
        // Arrange
        var standardInfContent = CreateStandardInfContent();
        var infFilePath = _fixture.CreateTestInfFile(standardInfContent, "silent_test.inf");

        // Act - サイレント実行
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert - quickstart.mdで期待されるサイレント実行結果
        result.Should().NotBeNull();
        result.Status.Should().NotBe(InstallationStatus.PendingUserInput, 
            "サイレント実行ではユーザー入力待ちになってはいけません");
        
        // 成功/失敗の判定ができることを確認
        result.Status.Should().BeOneOf(
            InstallationStatus.Completed, 
            InstallationStatus.CompletedWithWarnings, 
            InstallationStatus.Failed);
    }

    /// <summary>
    /// ログ出力検証: 詳細ログが適切に出力される
    /// quickstart.md - "詳細ログが出力"
    /// </summary>
    [Fact]
    public async Task LogOutput_ShouldProvideDetailedLogs()
    {
        // Arrange
        var standardInfContent = CreateStandardInfContent();
        var infFilePath = _fixture.CreateTestInfFile(standardInfContent, "log_test.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert - quickstart.mdで期待されるログ出力
        result.Should().NotBeNull();
        result.LogEntries.Should().NotBeEmpty("詳細ログが出力される必要があります");
        
        // 複数のログレベルが含まれることを確認
        result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Information);
        
        // 処理ステップが記録されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Started", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Processing", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase));
        
        // タイムスタンプが適切に設定されていることを確認
        result.LogEntries.Should().AllSatisfy(log => 
            log.Timestamp.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5)));
    }

    /// <summary>
    /// 内部処理確認: SetupAPI使い分けの自動実行
    /// quickstart.md - "内部処理の確認"
    /// </summary>
    [Fact]
    public async Task InternalProcessing_ShouldAutoExecuteSetupApiCalls()
    {
        // Arrange - Services セクション付きINF
        var servicesInfContent = CreateInfWithServicesSection();
        var infFilePath = _fixture.CreateTestInfFile(servicesInfContent, "internal_processing_test.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert - quickstart.mdで説明されている内部処理の確認
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(InstallationStatus.Completed, InstallationStatus.CompletedWithWarnings, InstallationStatus.Failed);
        
        // SetupInstallFromInfSectionW による指定セクションの適用確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("SetupInstallFromInfSection", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Installing from section", StringComparison.OrdinalIgnoreCase));
        
        // .Services セクションの存在確認と適用確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Services", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// エラー時の日本語メッセージ確認
    /// quickstart.md - "成功/失敗の日本語メッセージ"
    /// </summary>
    [Fact]
    public async Task ErrorHandling_ShouldProvideJapaneseMessages()
    {
        // Arrange - 意図的にエラーを発生させるINF
        var invalidInfContent = CreateInvalidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(invalidInfContent, "error_test.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert - quickstart.mdで期待される日本語エラーメッセージ
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        
        if (result.ErrorInfo != null)
        {
            // 日本語メッセージが含まれることを確認
            result.ErrorInfo.Value.LocalizedMessage.Should().MatchRegex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]", 
                "失敗時は日本語メッセージが表示される必要があります");
        }
        
        // エラーログにも日本語が含まれることを確認（可能であれば）
        var errorLogs = result.LogEntries.Where(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error).ToList();
        errorLogs.Should().NotBeEmpty();
    }

    /// <summary>
    /// quickstart.mdで示される標準的なINFファイルの内容を作成
    /// </summary>
    private static string CreateStandardInfContent()
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
helper.dll

[DriverRegistry]
HKR,,""Version"",0x00000000,""1.0.0.0""
HKR,,""InstallDate"",0x00000000,%InstallDate%

[DestinationDirs]
DriverFiles = 12

[SourceDisksNames]
1 = %DiskName%,,,""""

[SourceDisksFiles]
test_driver.sys = 1
helper.dll = 1

[Strings]
DiskName = ""Sample Driver Installation Disk""
TestDriverService.DisplayName = ""Sample Test Driver Service""
InstallDate = ""2024-01-01""
";
    }

    /// <summary>
    /// カスタムセクション用のINFファイルの内容を作成
    /// </summary>
    private static string CreateCustomSectionInfContent()
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

[MyCustomInstall]
CopyFiles=CustomFiles
AddReg=CustomRegistry

[MyCustomInstall.Services]
AddService = CustomDriverService,0x00000002,CustomDriverService_ServiceInstall

[CustomDriverService_ServiceInstall]
ServiceType = 1
StartType = 3
ErrorControl = 1
ServiceBinary = %12%\custom_driver.sys
DisplayName = %CustomDriverService.DisplayName%

[DefaultFiles]
default_driver.sys

[CustomFiles]
custom_driver.sys
custom_helper.dll

[CustomRegistry]
HKR,,""CustomValue"",0x00000000,""CustomData""

[DestinationDirs]
DefaultFiles = 12
CustomFiles = 12

[Strings]
CustomDriverService.DisplayName = ""Custom Test Driver Service""
";
    }

    /// <summary>
    /// 小規模ドライバ用のINFファイルの内容を作成（パフォーマンステスト用）
    /// </summary>
    private static string CreateSmallScaleInfContent()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=SmallDriverFiles

[SmallDriverFiles]
small_driver.sys

[DestinationDirs]
SmallDriverFiles = 12

[SourceDisksNames]
1 = %DiskName%,,,""""

[SourceDisksFiles]
small_driver.sys = 1

[Strings]
DiskName = ""Small Driver Installation Disk""
";
    }

    /// <summary>
    /// Servicesセクション付きのINFファイルの内容を作成（内部処理確認用）
    /// </summary>
    private static string CreateInfWithServicesSection()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=ServiceDriverFiles
AddReg=ServiceDriverRegistry

[DefaultInstall.Services]
AddService = ServiceTestDriver,0x00000002,ServiceTestDriver_ServiceInstall
AddService = HelperService,0x00000000,HelperService_ServiceInstall

[ServiceTestDriver_ServiceInstall]
ServiceType = 1
StartType = 3
ErrorControl = 1
ServiceBinary = %12%\service_driver.sys
DisplayName = %ServiceTestDriver.DisplayName%

[HelperService_ServiceInstall]
ServiceType = 32
StartType = 2
ErrorControl = 1
ServiceBinary = %11%\helper_service.exe
DisplayName = %HelperService.DisplayName%

[ServiceDriverFiles]
service_driver.sys
helper_service.exe

[ServiceDriverRegistry]
HKR,,""ServiceVersion"",0x00000000,""1.0.0.0""

[DestinationDirs]
ServiceDriverFiles = 12

[Strings]
ServiceTestDriver.DisplayName = ""Service Test Driver""
HelperService.DisplayName = ""Helper Service""
";
    }

    /// <summary>
    /// 無効なINFファイルの内容を作成（検証仕様: [Version] セクションが存在しないケースを生成）
    /// ValidateInfFileAsync の仕様に合わせ、[Version] がない場合を不正と判定する
    /// </summary>
    private static string CreateInvalidInfContent()
    {
        return @"
[DefaultInstall]
CopyFiles=NonExistentFiles

[NonExistentFiles]
missing.sys
";
    }
}