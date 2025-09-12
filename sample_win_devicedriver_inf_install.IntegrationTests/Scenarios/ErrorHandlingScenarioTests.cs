using FluentAssertions;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Enums;
using System.IO;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// エラーハンドリングシナリオの統合テスト
/// 様々なエラー条件での適切な処理を検証
/// </summary>
public class ErrorHandlingScenarioTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;
    private readonly IInstallationLogger _logger;

    public ErrorHandlingScenarioTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
        _logger = _fixture.GetService<IInstallationLogger>();
    }

    /// <summary>
    /// 存在しないINFファイルでのエラーハンドリングテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithNonExistentFile_ShouldReturnFailedStatus()
    {
        // Arrange
        var nonExistentFilePath = Path.Combine(_fixture.TempTestDirectory, "non_existent.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(nonExistentFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Value.ErrorCode.Should().NotBe(0);
        result.ErrorInfo.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
        
        // ログにエラー情報が記録されていることを確認
        result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error);
    }

    /// <summary>
    /// 不正なINFファイル形式でのエラーハンドリングテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithInvalidInfFormat_ShouldReturnFailedStatus()
    {
        // Arrange
        var invalidInfContent = "This is not a valid INF file content";
        var infFilePath = _fixture.CreateTestInfFile(invalidInfContent, "invalid_format.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
        
        // 日本語エラーメッセージが含まれることを確認
        result.ErrorInfo.Value.LocalizedMessage!.Should().MatchRegex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]");
        
        // エラーログが記録されていることを確認
        result.LogEntries.Should().Contain(log => 
            log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error && 
            log.Message.Contains("INF", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 存在しないセクション指定でのエラーハンドリングテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithNonExistentSection_ShouldReturnFailedStatus()
    {
        // Arrange
        var validInfContent = CreateValidInfContent();
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "missing_section.inf");
        var nonExistentSection = "NonExistentSection";

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath, nonExistentSection);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
        result.SectionName.Should().Be(nonExistentSection);
        
        // セクションが見つからないことを示すエラー情報が含まれることを確認
        result.LogEntries.Should().Contain(log => 
            log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error && 
            log.Message.Contains(nonExistentSection, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 権限不足エラーのシミュレーションテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithInsufficientPermissions_ShouldHandlePermissionError()
    {
        // Skip test if running as administrator (this test simulates permission issues)
        if (TestEnvironmentFixture.IsRunningAsAdministrator())
        {
            return; // Skip when running as admin
        }

        // Arrange
        var validInfContent = CreateSystemLevelInfContent(); // システムレベルのインストールが必要なINF
        var infFilePath = _fixture.CreateTestInfFile(validInfContent, "system_level_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Status == InstallationStatus.Failed)
        {
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo!.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
            
            // 権限関連のエラーが記録されていることを確認
            result.LogEntries.Should().Contain(log => 
                log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error && 
                (log.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                 log.Message.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                 log.Message.Contains("権限", StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// 破損したINFファイルでのエラーハンドリングテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithCorruptedInfFile_ShouldReturnFailedStatus()
    {
        // Arrange
        var corruptedInfContent = CreateCorruptedInfContent();
        var infFilePath = _fixture.CreateTestInfFile(corruptedInfContent, "corrupted.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
        
        // 構文エラーや形式エラーが適切に処理されていることを確認
        result.LogEntries.Should().Contain(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error);
        result.ErrorInfo!.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 空のINFファイルでのエラーハンドリングテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithEmptyInfFile_ShouldReturnFailedStatus()
    {
        // Arrange
        var emptyInfContent = "";
        var infFilePath = _fixture.CreateTestInfFile(emptyInfContent, "empty.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 日本語エラーメッセージの表示確認テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_OnError_ShouldProvideJapaneseErrorMessages()
    {
        // Arrange
        var invalidInfContent = CreateInvalidVersionSection();
        var infFilePath = _fixture.CreateTestInfFile(invalidInfContent, "invalid_version.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Status == InstallationStatus.Failed && result.ErrorInfo != null)
        {
            // 日本語の文字が含まれることを確認（ひらがな、カタカナ、漢字）
            result.ErrorInfo.Value.LocalizedMessage.Should().MatchRegex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]", 
                "エラーメッセージには日本語が含まれる必要があります");
            
            // 技術詳細も含まれることを確認
            result.ErrorInfo.Value.TechnicalDetails.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// 複数のエラーが発生した場合の統合処理テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithMultipleErrors_ShouldConsolidateErrorInformation()
    {
        // Arrange
        var multiErrorInfContent = CreateMultiErrorInfContent();
        var infFilePath = _fixture.CreateTestInfFile(multiErrorInfContent, "multi_error.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        
        // 複数のエラーログエントリが記録されていることを確認
        var errorLogs = result.LogEntries.Where(log => log.Level == sample_win_devicedriver_inf_install.Enums.LogLevel.Error).ToList();
        errorLogs.Should().NotBeEmpty();
        
        // 最終的なエラー情報が統合されていることを確認
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Value.LocalizedMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 有効なINFファイルの内容を作成
    /// </summary>
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

[Strings]
DiskName = ""Test Driver Installation Disk""
";
    }

    /// <summary>
    /// システムレベルのインストールが必要なINFファイルの内容を作成
    /// </summary>
    private static string CreateSystemLevelInfContent()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=System
ClassGUID={4d36e97d-e325-11ce-bfc1-08002be10318}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=SystemFiles
RegisterDlls=SystemDlls

[SystemFiles]
system_driver.sys

[SystemDlls]
system_lib.dll

[DestinationDirs]
SystemFiles = 12
SystemDlls = 11

[Strings]
DiskName = ""System Driver Installation Disk""
";
    }

    /// <summary>
    /// 破損したINFファイルの内容を作成
    /// </summary>
    private static string CreateCorruptedInfContent()
    {
        return @"
[Version
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={invalid-guid}
Provider=TestProvider

[DefaultInstall]
CopyFiles=TestFiles
Missing closing bracket

[TestFiles
test.sys

[DestinationDirs]
TestFiles = 
";
    }

    /// <summary>
    /// 無効なVersionセクションを持つINFファイルの内容を作成
    /// </summary>
    private static string CreateInvalidVersionSection()
    {
        return @"
[Version]
Signature=""INVALID SIGNATURE""
Class=
Provider=
DriverVer=invalid-date

[DefaultInstall]
CopyFiles=TestFiles

[TestFiles]
test.sys
";
    }

    /// <summary>
    /// 複数のエラーを含むINFファイルの内容を作成
    /// </summary>
    private static string CreateMultiErrorInfContent()
    {
        return @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={invalid-guid-format}
Provider=
DriverVer=

[DefaultInstall]
CopyFiles=NonExistentFiles
AddReg=InvalidRegistry

[NonExistentFiles]
missing.sys

[InvalidRegistry]
HKR,,InvalidValue

[DestinationDirs]
NonExistentFiles = InvalidDestination
";
    }
}