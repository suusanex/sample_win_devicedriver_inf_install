using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Core.Contracts;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Enums;
using Xunit;
using Xunit.Abstractions;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// T003 ドライバインストール機能の統合テスト
/// </summary>
public class DriverInstallationIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly IDriverInstallationService _installationService;
    private readonly string _testInfFilePath;

    public DriverInstallationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // テスト用のホストを設定
        _host = CreateTestHost();
        _installationService = _host.Services.GetRequiredService<IDriverInstallationService>();
        
        // テスト用のINFファイルを作成
        _testInfFilePath = CreateTestInfFile();
    }

    [Fact]
    public void T003_CreateSession_機能テスト_セッション管理が正しく動作する()
    {
        // Arrange - T003要件: インストールセッション管理
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-session",
            InfPath = _testInfFilePath,
            Name = "Test Driver for Session Management",
            Version = "1.0.0",
            Provider = "Test Provider"
        };

        // Act
        var session = _installationService.CreateSession(driverPackage);

        // Assert - T003完了条件: インストールセッションが正しく管理される
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal(driverPackage, session.DriverPackage);
        Assert.Equal(InstallationStatus.Pending, session.Status);
        
        // セッション取得のテスト
        var retrievedSession = _installationService.GetSession(session.SessionId);
        Assert.Equal(session, retrievedSession);
        
        // アクティブセッション一覧のテスト
        var activeSessions = _installationService.GetActiveSessions();
        Assert.Contains(session, activeSessions);
        
        _output.WriteLine($"✓ T003機能テスト: セッション管理 - SessionID: {session.SessionId}");
    }

    [Fact]
    public async Task T003_GetDriverStatusAsync_機能テスト_状態確認が実行される()
    {
        // Arrange - T003要件: ドライバ状態確認サービス
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-status",
            InfPath = _testInfFilePath,
            Name = "Test Driver for Status Check",
            HardwareIds = new() { "TEST\\DEVICE001", "TEST\\DEVICE002" }
        };

        // Act - T003完了条件: デバイス状態が確認できる
        var devices = await _installationService.GetDriverStatusAsync(driverPackage);

        // Assert
        Assert.NotNull(devices);
        // 実際のハードウェアが接続されていない環境では空のリストが返される
        // これはテスト環境では正常な動作
        
        _output.WriteLine($"✓ T003機能テスト: 状態確認 - 検出デバイス数: {devices.Count}");
    }

    [Fact]
    public void T003_INFファイル検証_機能テスト_有効なINFファイルが検証される()
    {
        // Arrange - T003要件: INF ファイルの検証
        var validDriverPackage = new DriverPackage
        {
            Id = "test-valid-inf",
            InfPath = _testInfFilePath,
            Name = "Test Driver with Valid INF"
        };

        var invalidDriverPackage = new DriverPackage
        {
            Id = "test-invalid-inf",
            InfPath = "non-existent-file.inf",
            Name = "Test Driver with Invalid INF"
        };

        // Act & Assert - 有効なINFファイル
        Assert.True(validDriverPackage.ValidateInfFileExists());
        
        // Act & Assert - 無効なINFファイル
        Assert.False(invalidDriverPackage.ValidateInfFileExists());
        
        _output.WriteLine("✓ T003機能テスト: INFファイル検証");
    }

    [Fact]
    public async Task T003_InstallDriverAsync_エラーハンドリング機能テスト_適切なApiErrorInfoが生成される()
    {
        // Arrange - T003要件: エラーハンドリング
        var invalidDriverPackage = new DriverPackage
        {
            Id = "test-invalid-driver",
            InfPath = "non-existent-driver.inf",
            Name = "Invalid Test Driver"
        };

        // Act
        var result = await _installationService.InstallDriverAsync(invalidDriverPackage);

        // Assert - T003完了条件: エラー発生時に適切な ApiErrorInfo が生成される
        Assert.NotNull(result);
        Assert.False(result.IsSuccessful);
        Assert.Equal(InstallationStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorInfo);
        
        _output.WriteLine($"✓ T003機能テスト: エラーハンドリング - ErrorCode: {result.ErrorInfo?.ErrorCode}");
    }

    /// <summary>
    /// テスト用のホストを作成
    /// </summary>
    private IHost CreateTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // sample_win_devicedriver_inf_install の Program.cs と同じサービス登録
                services.AddSingleton<sample_win_devicedriver_inf_install.Core.Services.WindowsApiErrorHandler>();
                services.AddSingleton<sample_win_devicedriver_inf_install.Services.DriverStatusService>();
                services.AddSingleton<IDriverInstallationService, sample_win_devicedriver_inf_install.Services.DriverInstallationService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .Build();
    }

    /// <summary>
    /// テスト用のINFファイルを作成
    /// </summary>
    private string CreateTestInfFile()
    {
        var tempPath = Path.GetTempPath();
        var infPath = Path.Combine(tempPath, $"test_driver_{Guid.NewGuid():N}.inf");
        
        // 最小限の有効なINFファイル内容を作成
        var infContent = @"[Version]
Signature = ""$Windows NT$""
Class = Sample
ClassGUID = {12345678-1234-5678-9012-123456789012}
Provider = %ProviderName%
DriverVer = 01/01/2024,1.0.0.0

[Strings]
ProviderName = ""Test Provider""
";
        
        File.WriteAllText(infPath, infContent);
        return infPath;
    }

    public void Dispose()
    {
        // テストファイルのクリーンアップ
        if (File.Exists(_testInfFilePath))
        {
            try
            {
                File.Delete(_testInfFilePath);
            }
            catch
            {
                // テスト環境でのクリーンアップエラーは無視
            }
        }
        
        _host?.Dispose();
    }
}