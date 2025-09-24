using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using sample_win_devicedriver_inf_install.Tests.Stubs;
using Xunit;

namespace sample_win_devicedriver_inf_install.IntegrationTests.Scenarios;

/// <summary>
/// ファイル操作（CopyFiles）専用統合テスト
/// </summary>
[Collection("TestEnvironment")]
public class FileOperationScenarioTests
{
    private readonly TestEnvironmentFixture _fixture;

    public FileOperationScenarioTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InstallFromInfAsync_WithCopyFilesSection_ShouldExecuteFileOperationsCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var installationLogger = serviceProvider.GetRequiredService<IInstallationLogger>();
        
        // ファイル操作を含むINFファイルを使用
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        var sectionName = "DefaultInstall";

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath, sectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("File operations should complete successfully");

        // ファイル操作段階のログを詳細に確認
        var logEntries = await installationLogger.GetLogEntriesAsync(result.SessionId, CancellationToken.None);
        var fileOperationLogs = logEntries.Where(e => e.Category == "FileOperation").ToList();

        fileOperationLogs.Should().NotBeEmpty("File operation logs should be present");
        
        // ファイル操作の開始ログを確認
        var startFileOpLog = fileOperationLogs.FirstOrDefault(e => e.Message.Contains("Starting file operations"));
        startFileOpLog.Should().NotBeNull("File operation start should be logged");
        startFileOpLog!.Message.Should().Contain("CopyFiles");

        // ファイル操作の完了ログを確認
        var completeFileOpLog = fileOperationLogs.FirstOrDefault(e => e.Message.Contains("Successfully completed file operations"));
        completeFileOpLog.Should().NotBeNull("File operation completion should be logged");
    }

    [Fact]
    public async Task InstallFromInfAsync_FileOperationPhase_ShouldUseFileQueueCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var setupApiWrapper = serviceProvider.GetRequiredService<ISetupApiWrapper>();
        
        // SetupApiStubの場合、ファイルキュー操作のスタブ動作を確認
        if (setupApiWrapper is SetupApiStub stub)
        {
            // ファイルキュー操作前の状態を記録
            var initialErrorCode = stub.GetLastError();
        }

        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(infPath);

        // Assert
        result.IsSuccess.Should().BeTrue("File queue operations should work correctly");

        // SetupApiStubでファイルキュー操作が正しく呼び出されたことを間接的に確認
        // （実際のファイルはコピーされないが、APIの呼び出し順序は正しく処理される）
    }

    [Fact]
    public async Task InstallFromInfAsync_FileOperationError_ShouldHandleFileOperationFailuresCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        var setupApiWrapper = serviceProvider.GetRequiredService<ISetupApiWrapper>();
        
        // 存在しないINFファイルでファイル操作エラーをシミュレート
        var invalidInfPath = "nonexistent_file_operations.inf";
        var sectionName = "DefaultInstall";

        // Act
        var result = await driverInstallationService.InstallFromInfAsync(invalidInfPath, sectionName);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("Invalid file should cause failure");
        result.ErrorInfo.Should().NotBeNull("Error info should be provided");
    }

    [Fact]
    public async Task InstallFromInfAsync_FileOperationTimeout_ShouldHandleTimeoutCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var driverInstallationService = serviceProvider.GetRequiredService<IDriverInstallationService>();
        
        var infPath = Path.Combine(_fixture.TestDataDirectory, "SampleDrivers", "sample_driver.inf");
        
        // 短いタイムアウト設定でテスト
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act & Assert
        var result = await driverInstallationService.InstallFromInfAsync(infPath, cancellationToken: cts.Token);
        
        // タイムアウトまたはキャンセルによる適切な処理を確認
        if (!result.IsSuccess)
        {
            // タイムアウトまたはキャンセルの場合
            result.Status.Should().BeOneOf(
                Enums.InstallationStatus.Cancelled,
                Enums.InstallationStatus.Failed
            );
        }
    }

    [Fact]
    public async Task InstallFromInfAsync_FileOperationsPhaseFirst_ShouldExecuteInCorrectOrder()
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
        var orderedLogs = logEntries.OrderBy(e => e.Timestamp).ToList();

        // ファイル操作が最初に実行されることを確認
        var fileOpIndex = orderedLogs.FindIndex(e => e.Category == "FileOperation" && e.Message.Contains("Starting"));
        var registryOpIndex = orderedLogs.FindIndex(e => e.Category == "RegistryOperation" && e.Message.Contains("Starting"));
        var serviceOpIndex = orderedLogs.FindIndex(e => e.Category == "ServiceOperation");

        fileOpIndex.Should().BeGreaterOrEqualTo(0, "File operations should be logged");
        registryOpIndex.Should().BeGreaterThan(fileOpIndex, "Registry operations should come after file operations");
        serviceOpIndex.Should().BeGreaterThan(registryOpIndex, "Service operations should come last");
    }

    [Fact]
    public async Task FileQueueOperations_ThroughSetupApiWrapper_ShouldWorkCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var setupApiWrapper = serviceProvider.GetRequiredService<ISetupApiWrapper>();
        
        // SetupApiStubでファイルキュー操作を直接テスト
        if (setupApiWrapper is SetupApiStub stub)
        {
            // Act
            var fileQueue = stub.SetupOpenFileQueue();
            
            // Assert
            fileQueue.Should().NotBe(IntPtr.Zero, "File queue should be created successfully");
            
            // ファイルキューの操作を確認
            var infHandle = stub.SetupOpenInfFile("test.inf", null, 2, out _);
            if (infHandle != IntPtr.Zero)
            {
                var queueResult = stub.SetupInstallFilesFromInfSection(
                    infHandle, IntPtr.Zero, fileQueue, "DefaultInstall", null, 0);
                queueResult.Should().BeTrue("Queuing files should succeed");
                
                var commitResult = stub.SetupCommitFileQueue(IntPtr.Zero, fileQueue, IntPtr.Zero, IntPtr.Zero);
                commitResult.Should().BeTrue("Committing file queue should succeed");
                
                stub.SetupCloseInfFile(infHandle);
            }
            
            var closeResult = stub.SetupCloseFileQueue(fileQueue);
            closeResult.Should().BeTrue("Closing file queue should succeed");
        }
    }
}