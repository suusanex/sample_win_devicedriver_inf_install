using FluentAssertions;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Enums;
using System.IO;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// 基本的な統合テストの動作確認
/// </summary>
public class BasicIntegrationTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;
    private readonly IDriverInstallationService _installationService;

    public BasicIntegrationTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
        _installationService = _fixture.GetService<IDriverInstallationService>();
    }

    /// <summary>
    /// 基本的なサービス注入テスト
    /// </summary>
    [Fact]
    public void ServiceInjection_ShouldWork()
    {
        // Arrange & Act & Assert
        _installationService.Should().NotBeNull();
        _fixture.Host.Should().NotBeNull();
        _fixture.TempTestDirectory.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// テストファイル作成テスト
    /// </summary>
    [Fact]
    public void CreateTestInfFile_ShouldCreateFile()
    {
        // Arrange
        var testContent = "[Version]\nSignature=\"$WINDOWS NT$\"";
        var fileName = "basic_test.inf";

        // Act
        var filePath = _fixture.CreateTestInfFile(testContent, fileName);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Contain("$WINDOWS NT$");
        Path.GetFileName(filePath).Should().Be(fileName);
    }

    /// <summary>
    /// 基本的なインストールサービス呼び出しテスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithBasicInf_ShouldReturnResult()
    {
        // Arrange
        var basicInfContent = @"
[Version]
Signature=""$WINDOWS NT$""
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
; No actual operations - just basic structure
";
        var infFilePath = _fixture.CreateTestInfFile(basicInfContent, "basic_driver.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(infFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.Status.Should().BeOneOf(
            InstallationStatus.Completed, 
            InstallationStatus.CompletedWithWarnings, 
            InstallationStatus.Failed);
        result.InfPath.Should().Be(infFilePath);
        result.LogEntries.Should().NotBeEmpty();
    }

    /// <summary>
    /// 存在しないファイルでのエラーハンドリング基本テスト
    /// </summary>
    [Fact]
    public async Task InstallFromInfAsync_WithNonExistentFile_ShouldFail()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.TempTestDirectory, "nonexistent.inf");

        // Act
        var result = await _installationService.InstallFromInfAsync(nonExistentPath);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(InstallationStatus.Failed);
        result.ErrorInfo.Should().NotBeNull();
    }
}