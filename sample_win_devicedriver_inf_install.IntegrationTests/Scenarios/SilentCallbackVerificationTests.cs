using FluentAssertions;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// サイレントコールバックの動作検証テスト
/// </summary>
public class SilentCallbackVerificationTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;

    public SilentCallbackVerificationTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
    }

    /// <summary>
    /// サイレントコールバックが呼び出されることを確認するテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_ShouldInvokeSilentCallback()
    {
        // Arrange
        var validInfContent = @"
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
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "callback_test_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.LogEntries.Should().NotBeEmpty();
        
        // デバッグログを出力
        Console.WriteLine("=== All Log Entries ===");
        foreach (var log in result.LogEntries)
        {
            Console.WriteLine($"{log.Level}: [{log.Category}] {log.Message}");
        }

        // サイレントコールバック関連のログが存在することを確認
        var silentCallbackLogs = result.LogEntries.Where(log =>
            log.Message.Contains("SilentFileQueueCallback", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Silent callback", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("callback invoked", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("File queue processing", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("Copying file", StringComparison.OrdinalIgnoreCase) ||
            log.Message.Contains("SetupApiWrapper implementation", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        Console.WriteLine($"\n=== Silent Callback Logs ({silentCallbackLogs.Count}) ===");
        foreach (var log in silentCallbackLogs)
        {
            Console.WriteLine($"{log.Level}: [{log.Category}] {log.Message}");
        }

        // 最重要: サイレントコールバックが実際に動作していることを確認
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Silent callback invoked", StringComparison.OrdinalIgnoreCase),
            "サイレントコールバックが実際に呼び出される必要があります");

        // ファイルコピー通知が記録されていること
        result.LogEntries.Should().Contain(log => 
            log.Message.Contains("Copying file", StringComparison.OrdinalIgnoreCase),
            "ファイルコピー通知がログに記録される必要があります");

        // 統合テストでは SetupApiStub が使用されるため、SetupApiWrapper実装ログの確認は削除
    }
}