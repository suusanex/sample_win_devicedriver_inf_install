using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Services;
using LogLevel = sample_win_devicedriver_inf_install.Enums.LogLevel;

namespace sample_win_devicedriver_inf_install.Tests.Services;

/// <summary>
/// InstallationLoggerのテストクラス
/// T004のタスク要件に基づく機能テストを実装
/// </summary>
public class InstallationLoggerTests : IDisposable
{
    private readonly ILogger<InstallationLogger> _mockLogger;
    private readonly InstallationLogger _installationLogger;
    private readonly string _testCorrelationId;
    private readonly string _testCategory;

    public InstallationLoggerTests()
    {
        // Moqを使わずシンプルなテスト用ロガーを使用
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
        _mockLogger = loggerFactory.CreateLogger<InstallationLogger>();
        _installationLogger = new InstallationLogger(_mockLogger);
        _testCorrelationId = Guid.NewGuid().ToString();
        _testCategory = "TestCategory";
    }

    /// <summary>
    /// 基本的なログ記録機能のテスト
    /// </summary>
    [Fact]
    public async Task LogAsync_ShouldRecordLogEntry_WhenValidEntryProvided()
    {
        // Arrange
        var logEntry = LogEntry.Information("Test message", _testCategory, _testCorrelationId);

        // Act
        await _installationLogger.LogAsync(logEntry);

        // Assert
        var retrievedEntries = await _installationLogger.GetLogEntriesAsync(_testCorrelationId);
        Assert.Single(retrievedEntries);
        
        var retrievedEntry = retrievedEntries.First();
        Assert.Equal("Test message", retrievedEntry.Message);
        Assert.Equal(_testCategory, retrievedEntry.Category);
        Assert.Equal(_testCorrelationId, retrievedEntry.CorrelationId);
        Assert.Equal(LogLevel.Information, retrievedEntry.Level);
    }

    /// <summary>
    /// 複数のログエントリの記録テスト
    /// </summary>
    [Fact]
    public async Task LogAsync_ShouldRecordMultipleEntries_WhenMultipleEntriesProvided()
    {
        // Arrange
        var entries = new[]
        {
            LogEntry.Information("Info message", _testCategory, _testCorrelationId),
            LogEntry.Warning("Warning message", _testCategory, _testCorrelationId),
            LogEntry.Error("Error message", _testCategory, _testCorrelationId, new Exception("Test exception"))
        };

        // Act
        await _installationLogger.LogAsync(entries);

        // Assert
        var retrievedEntries = await _installationLogger.GetLogEntriesAsync(_testCorrelationId);
        Assert.Equal(3, retrievedEntries.Count());
        
        var entriesByLevel = retrievedEntries.GroupBy(e => e.Level).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(1, entriesByLevel[LogLevel.Information]);
        Assert.Equal(1, entriesByLevel[LogLevel.Warning]);
        Assert.Equal(1, entriesByLevel[LogLevel.Error]);
    }

    /// <summary>
    /// 情報ログの記録テスト
    /// </summary>
    [Fact]
    public async Task LogInformationAsync_ShouldRecordInformationEntry()
    {
        // Arrange
        var message = "Information test message";
        var properties = new Dictionary<string, object> { ["TestProperty"] = "TestValue" };

        // Act
        await _installationLogger.LogInformationAsync(message, _testCategory, _testCorrelationId, properties);

        // Assert
        var retrievedEntries = await _installationLogger.GetLogEntriesAsync(_testCorrelationId);
        Assert.Single(retrievedEntries);
        
        var entry = retrievedEntries.First();
        Assert.Equal(message, entry.Message);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.NotNull(entry.Properties);
        Assert.Equal("TestValue", entry.Properties["TestProperty"]);
    }

    /// <summary>
    /// エラーログの記録テスト（例外情報含む）
    /// </summary>
    [Fact]
    public async Task LogErrorAsync_ShouldRecordErrorEntryWithException()
    {
        // Arrange
        var message = "Error test message";
        var exception = new InvalidOperationException("Test exception");
        var properties = new Dictionary<string, object> { ["ErrorCode"] = "0x800" };

        // Act
        await _installationLogger.LogErrorAsync(message, _testCategory, _testCorrelationId, exception, properties);

        // Assert
        var retrievedEntries = await _installationLogger.GetLogEntriesAsync(_testCorrelationId);
        Assert.Single(retrievedEntries);
        
        var entry = retrievedEntries.First();
        Assert.Equal(message, entry.Message);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Equal("Test exception", entry.Exception.Message);
        Assert.NotNull(entry.Properties);
        Assert.Equal("0x800", entry.Properties["ErrorCode"]);
    }

    /// <summary>
    /// 警告ログの記録テスト
    /// </summary>
    [Fact]
    public async Task LogWarningAsync_ShouldRecordWarningEntry()
    {
        // Arrange
        var message = "Warning test message";
        var properties = new Dictionary<string, object> { ["Section"] = "DefaultInstall.Services" };

        // Act
        await _installationLogger.LogWarningAsync(message, _testCategory, _testCorrelationId, properties);

        // Assert
        var retrievedEntries = await _installationLogger.GetLogEntriesAsync(_testCorrelationId);
        Assert.Single(retrievedEntries);
        
        var entry = retrievedEntries.First();
        Assert.Equal(message, entry.Message);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Properties);
        Assert.Equal("DefaultInstall.Services", entry.Properties["Section"]);
    }

    /// <summary>
    /// 相関IDによるログエントリ取得テスト
    /// </summary>
    [Fact]
    public async Task GetLogEntriesAsync_ShouldReturnCorrectEntries_WhenFilteredByCorrelationId()
    {
        // Arrange
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();
        
        await _installationLogger.LogInformationAsync("Message 1", _testCategory, correlationId1);
        await _installationLogger.LogInformationAsync("Message 2", _testCategory, correlationId2);
        await _installationLogger.LogInformationAsync("Message 3", _testCategory, correlationId1);

        // Act
        var entriesForId1 = await _installationLogger.GetLogEntriesAsync(correlationId1);
        var entriesForId2 = await _installationLogger.GetLogEntriesAsync(correlationId2);

        // Assert
        Assert.Equal(2, entriesForId1.Count());
        Assert.Single(entriesForId2);
        
        Assert.All(entriesForId1, entry => Assert.Equal(correlationId1, entry.CorrelationId));
        Assert.Equal(correlationId2, entriesForId2.First().CorrelationId);
    }

    /// <summary>
    /// JSONエクスポート機能のテスト
    /// </summary>
    [Fact]
    public async Task ExportLogsAsync_ShouldExportToJsonFile_WhenJsonFormatSpecified()
    {
        // Arrange
        await _installationLogger.LogInformationAsync("Export test message", _testCategory, _testCorrelationId);
        var exportPath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.json");

        try
        {
            // Act
            await _installationLogger.ExportLogsAsync(_testCorrelationId, exportPath, "json");

            // Assert
            Assert.True(File.Exists(exportPath));
            var content = await File.ReadAllTextAsync(exportPath);
            Assert.Contains("Export test message", content);
            Assert.Contains(_testCorrelationId, content);
            Assert.Contains("\"level\":", content); // JSON構造の確認
        }
        finally
        {
            // Cleanup
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    /// <summary>
    /// XMLエクスポート機能のテスト
    /// </summary>
    [Fact]
    public async Task ExportLogsAsync_ShouldExportToXmlFile_WhenXmlFormatSpecified()
    {
        // Arrange
        await _installationLogger.LogInformationAsync("XML export test", _testCategory, _testCorrelationId);
        var exportPath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.xml");

        try
        {
            // Act
            await _installationLogger.ExportLogsAsync(_testCorrelationId, exportPath, "xml");

            // Assert
            Assert.True(File.Exists(exportPath));
            var content = await File.ReadAllTextAsync(exportPath);
            Assert.Contains("XML export test", content);
            Assert.Contains(_testCorrelationId, content);
            Assert.Contains("<LogEntries>", content); // XML構造の確認
        }
        finally
        {
            // Cleanup
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    /// <summary>
    /// CSVエクスポート機能のテスト
    /// </summary>
    [Fact]
    public async Task ExportLogsAsync_ShouldExportToCsvFile_WhenCsvFormatSpecified()
    {
        // Arrange
        await _installationLogger.LogInformationAsync("CSV export test", _testCategory, _testCorrelationId);
        var exportPath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.csv");

        try
        {
            // Act
            await _installationLogger.ExportLogsAsync(_testCorrelationId, exportPath, "csv");

            // Assert
            Assert.True(File.Exists(exportPath));
            var content = await File.ReadAllTextAsync(exportPath);
            Assert.Contains("CSV export test", content);
            Assert.Contains(_testCorrelationId, content);
            Assert.Contains("Timestamp,Level,CorrelationId", content); // CSVヘッダーの確認
        }
        finally
        {
            // Cleanup
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    /// <summary>
    /// 不正なエクスポート形式の例外テスト
    /// </summary>
    [Fact]
    public async Task ExportLogsAsync_ShouldThrowArgumentException_WhenUnsupportedFormatSpecified()
    {
        // Arrange
        await _installationLogger.LogInformationAsync("Test message", _testCategory, _testCorrelationId);
        var exportPath = Path.Combine(Path.GetTempPath(), "test_export.txt");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _installationLogger.ExportLogsAsync(_testCorrelationId, exportPath, "unsupported"));
    }

    /// <summary>
    /// 古いログエントリのクリーンアップテスト
    /// </summary>
    [Fact]
    public async Task CleanupOldLogsAsync_ShouldRemoveOldEntries_WhenOlderThanSpecifiedDate()
    {
        // Arrange
        var oldCorrelationId = Guid.NewGuid().ToString();
        var recentCorrelationId = Guid.NewGuid().ToString();
        
        // 古いエントリを作成（手動でタイムスタンプを設定）
        var oldEntry = new LogEntry(
            LogLevel.Information,
            "Old message",
            _testCategory,
            DateTime.UtcNow.AddDays(-10),
            oldCorrelationId);
        
        await _installationLogger.LogAsync(oldEntry);
        await _installationLogger.LogInformationAsync("Recent message", _testCategory, recentCorrelationId);

        // Act
        await _installationLogger.CleanupOldLogsAsync(DateTime.UtcNow.AddDays(-5));

        // Assert
        var oldEntries = await _installationLogger.GetLogEntriesAsync(oldCorrelationId);
        var recentEntries = await _installationLogger.GetLogEntriesAsync(recentCorrelationId);
        
        Assert.Empty(oldEntries); // 古いエントリは削除されている
        Assert.Single(recentEntries); // 新しいエントリは残っている
    }

    /// <summary>
    /// 存在しない相関IDでのログエントリ取得テスト
    /// </summary>
    [Fact]
    public async Task GetLogEntriesAsync_ShouldReturnEmpty_WhenCorrelationIdNotFound()
    {
        // Arrange
        var nonExistentCorrelationId = Guid.NewGuid().ToString();

        // Act
        var entries = await _installationLogger.GetLogEntriesAsync(nonExistentCorrelationId);

        // Assert
        Assert.Empty(entries);
    }

    /// <summary>
    /// リソースの適切な解放テスト
    /// </summary>
    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange & Act & Assert
        _installationLogger.Dispose();
        _installationLogger.Dispose(); // 2回呼び出しても例外が発生しないことを確認
    }

    /// <summary>
    /// 破棄後のメソッド呼び出しテスト
    /// </summary>
    [Fact]
    public async Task LogAsync_ShouldThrowObjectDisposedException_WhenCalledAfterDispose()
    {
        // Arrange
        _installationLogger.Dispose();
        var logEntry = LogEntry.Information("Test", _testCategory, _testCorrelationId);

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _installationLogger.LogAsync(logEntry));
    }

    public void Dispose()
    {
        _installationLogger?.Dispose();
    }
}