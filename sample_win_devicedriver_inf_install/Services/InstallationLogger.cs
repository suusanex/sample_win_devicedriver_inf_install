using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using LogLevel = sample_win_devicedriver_inf_install.Enums.LogLevel;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// インストールログサービス実装（宣言的インストール専用）
/// Microsoft.Extensions.Loggingを使用した構造化ログ機能を提供
/// </summary>
public class InstallationLogger : IInstallationLogger, IDisposable
{
    private readonly ILogger<InstallationLogger> _msLogger;
    private readonly ConcurrentDictionary<string, List<LogEntry>> _logCache;
    private readonly string _logDirectory;
    private readonly object _fileLock = new();
    private bool _disposed = false;

    // ログファイルの設定
    private const long MaxLogFileSize = 10 * 1024 * 1024; // 10MB
    private const int MaxLogFiles = 5;

    public InstallationLogger(ILogger<InstallationLogger> logger)
    {
        _msLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logCache = new ConcurrentDictionary<string, List<LogEntry>>();
        
        // ログディレクトリの設定（アプリケーションディレクトリ下のlogs）
        _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// ログエントリを記録します
    /// </summary>
    public async Task LogAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Microsoft.Extensions.Loggingへの記録
        var msLogLevel = ConvertToMsLogLevel(logEntry.Level);
        using var scope = _msLogger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = logEntry.CorrelationId,
            ["Category"] = logEntry.Category,
            ["Timestamp"] = logEntry.Timestamp
        });

        if (logEntry.Exception != null)
        {
            _msLogger.Log(msLogLevel, logEntry.Exception, logEntry.Message);
        }
        else
        {
            _msLogger.Log(msLogLevel, logEntry.Message);
        }

        // 内部キャッシュへの記録
        _logCache.AddOrUpdate(
            logEntry.CorrelationId,
            [logEntry],
            (key, existingList) =>
            {
                lock (existingList)
                {
                    existingList.Add(logEntry);
                }
                return existingList;
            });

        // ファイルへの記録
        await WriteToFileAsync(logEntry, cancellationToken);
    }

    /// <summary>
    /// 複数のログエントリを記録します
    /// </summary>
    public async Task LogAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var tasks = logEntries.Select(entry => LogAsync(entry, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 情報レベルのログを記録します（英語メッセージ）
    /// </summary>
    public async Task LogInformationAsync(
        string message,
        string category,
        string correlationId,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var logEntry = LogEntry.Information(message, category, correlationId, properties);
        await LogAsync(logEntry, cancellationToken);
    }

    /// <summary>
    /// エラーレベルのログを記録します（英語メッセージ）
    /// </summary>
    public async Task LogErrorAsync(
        string message,
        string category,
        string correlationId,
        Exception? exception = null,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var logEntry = LogEntry.Error(message, category, correlationId, exception, properties);
        await LogAsync(logEntry, cancellationToken);
    }

    /// <summary>
    /// 警告レベルのログを記録します（英語メッセージ）
    /// </summary>
    public async Task LogWarningAsync(
        string message,
        string category,
        string correlationId,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var logEntry = LogEntry.Warning(message, category, correlationId, properties);
        await LogAsync(logEntry, cancellationToken);
    }

    /// <summary>
    /// 指定された相関IDのログエントリを取得します
    /// </summary>
    public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_logCache.TryGetValue(correlationId, out var logList))
        {
            lock (logList)
            {
                return Task.FromResult<IEnumerable<LogEntry>>(logList.ToList());
            }
        }

        return Task.FromResult<IEnumerable<LogEntry>>([]);
    }

    /// <summary>
    /// ログをファイル出力します
    /// </summary>
    public async Task ExportLogsAsync(
        string correlationId,
        string filePath,
        string format = "json",
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var logEntries = await GetLogEntriesAsync(correlationId, cancellationToken);
        if (!logEntries.Any())
        {
            await _msLogger.LogWarningAsync($"No log entries found for correlation ID: {correlationId}");
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (format.ToLowerInvariant())
        {
            case "json":
                await ExportAsJsonAsync(logEntries, filePath, cancellationToken);
                break;
            case "xml":
                await ExportAsXmlAsync(logEntries, filePath, cancellationToken);
                break;
            case "csv":
                await ExportAsCsvAsync(logEntries, filePath, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }

        await _msLogger.LogInformationAsync($"Exported {logEntries.Count()} log entries to {filePath} in {format} format");
    }

    /// <summary>
    /// 古いログエントリを削除します
    /// </summary>
    public async Task CleanupOldLogsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        int removedCount = 0;

        // メモリキャッシュのクリーンアップ
        foreach (var kvp in _logCache.ToList())
        {
            var logList = kvp.Value;
            lock (logList)
            {
                int originalCount = logList.Count;
                logList.RemoveAll(entry => entry.Timestamp < olderThan);
                removedCount += originalCount - logList.Count;

                // 空になったエントリを削除
                if (!logList.Any())
                {
                    _logCache.TryRemove(kvp.Key, out _);
                }
            }
        }

        // ログファイルのクリーンアップ
        await CleanupOldLogFilesAsync(olderThan, cancellationToken);

        await _msLogger.LogInformationAsync($"Cleaned up {removedCount} old log entries older than {olderThan:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// ファイルにログエントリを書き込みます
    /// </summary>
    private async Task WriteToFileAsync(LogEntry logEntry, CancellationToken cancellationToken)
    {
        var fileName = $"installation_{DateTime.UtcNow:yyyy-MM-dd}.log";
        var filePath = Path.Combine(_logDirectory, fileName);

        // ファイルローテーション実行
        await RotateLogFileIfNeededAsync(filePath, cancellationToken);

        var logLine = FormatLogLine(logEntry);

        lock (_fileLock)
        {
            File.AppendAllText(filePath, logLine + Environment.NewLine);
        }
    }

    /// <summary>
    /// ログファイルのローテーションを実行します
    /// </summary>
    private async Task RotateLogFileIfNeededAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < MaxLogFileSize)
            return;

        // ファイルローテーション実行
        lock (_fileLock)
        {
            for (int i = MaxLogFiles - 1; i >= 1; i--)
            {
                var oldFile = $"{filePath}.{i}";
                var newFile = $"{filePath}.{i + 1}";

                if (File.Exists(oldFile))
                {
                    if (i == MaxLogFiles - 1)
                    {
                        File.Delete(oldFile); // 最古のファイルを削除
                    }
                    else
                    {
                        File.Move(oldFile, newFile);
                    }
                }
            }

            // 現在のファイルを .1 にリネーム
            if (File.Exists(filePath))
            {
                File.Move(filePath, $"{filePath}.1");
            }
        }

        await _msLogger.LogInformationAsync($"Rotated log file: {filePath}");
    }

    /// <summary>
    /// 古いログファイルを削除します
    /// </summary>
    private async Task CleanupOldLogFilesAsync(DateTime olderThan, CancellationToken cancellationToken)
    {
        var logFiles = Directory.GetFiles(_logDirectory, "*.log*");
        int deletedCount = 0;

        foreach (var file in logFiles)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.LastWriteTime < olderThan)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    await _msLogger.LogWarningAsync($"Failed to delete old log file {file}: {ex.Message}");
                }
            }
        }

        if (deletedCount > 0)
        {
            await _msLogger.LogInformationAsync($"Deleted {deletedCount} old log files");
        }
    }

    /// <summary>
    /// ログエントリをフォーマットします
    /// </summary>
    private static string FormatLogLine(LogEntry logEntry)
    {
        var timestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = logEntry.Level.ToString().ToUpperInvariant();
        var correlationId = logEntry.CorrelationId[..Math.Min(8, logEntry.CorrelationId.Length)];
        
        var line = $"[{timestamp}] [{level}] [{correlationId}] [{logEntry.Category}] {logEntry.Message}";
        
        if (logEntry.Exception != null)
        {
            line += $" | Exception: {logEntry.Exception}";
        }

        if (logEntry.Properties?.Any() == true)
        {
            var properties = string.Join(", ", logEntry.Properties.Select(p => $"{p.Key}={p.Value}"));
            line += $" | Properties: {properties}";
        }

        return line;
    }

    /// <summary>
    /// JSON形式でエクスポートします
    /// </summary>
    private static async Task ExportAsJsonAsync(IEnumerable<LogEntry> logEntries, string filePath, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(logEntries, options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// XML形式でエクスポートします
    /// </summary>
    private static async Task ExportAsXmlAsync(IEnumerable<LogEntry> logEntries, string filePath, CancellationToken cancellationToken)
    {
        var root = new XElement("LogEntries");

        foreach (var entry in logEntries)
        {
            var entryElement = new XElement("LogEntry",
                new XAttribute("Level", entry.Level),
                new XAttribute("Timestamp", entry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                new XAttribute("CorrelationId", entry.CorrelationId),
                new XAttribute("Category", entry.Category),
                new XElement("Message", entry.Message));

            if (entry.Exception != null)
            {
                entryElement.Add(new XElement("Exception", entry.Exception.ToString()));
            }

            if (entry.Properties?.Any() == true)
            {
                var propertiesElement = new XElement("Properties");
                foreach (var prop in entry.Properties)
                {
                    propertiesElement.Add(new XElement("Property",
                        new XAttribute("Key", prop.Key),
                        new XAttribute("Value", prop.Value?.ToString() ?? string.Empty)));
                }
                entryElement.Add(propertiesElement);
            }

            root.Add(entryElement);
        }

        var document = new XDocument(root);
        await File.WriteAllTextAsync(filePath, document.ToString(), cancellationToken);
    }

    /// <summary>
    /// CSV形式でエクスポートします
    /// </summary>
    private static async Task ExportAsCsvAsync(IEnumerable<LogEntry> logEntries, string filePath, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(filePath);
        
        // ヘッダー行
        await writer.WriteLineAsync("Timestamp,Level,CorrelationId,Category,Message,Exception,Properties");

        foreach (var entry in logEntries)
        {
            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var message = EscapeCsvField(entry.Message);
            var exception = EscapeCsvField(entry.Exception?.ToString() ?? string.Empty);
            var properties = EscapeCsvField(entry.Properties?.Any() == true 
                ? string.Join("; ", entry.Properties.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty);

            var line = $"{timestamp},{entry.Level},{entry.CorrelationId},{entry.Category},{message},{exception},{properties}";
            await writer.WriteLineAsync(line);
        }
    }

    /// <summary>
    /// CSVフィールドをエスケープします
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// カスタムLogLevelをMicrosoft.Extensions.LoggingのLogLevelに変換します
    /// </summary>
    private static Microsoft.Extensions.Logging.LogLevel ConvertToMsLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }

    /// <summary>
    /// オブジェクトが破棄されているかどうかをチェックします
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InstallationLogger));
        }
    }

    /// <summary>
    /// リソースを解放します
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _logCache.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// ILoggerの拡張メソッド
/// </summary>
internal static class LoggerExtensions
{
    public static async Task LogInformationAsync(this ILogger logger, string message)
    {
        await Task.Run(() => logger.LogInformation(message));
    }

    public static async Task LogWarningAsync(this ILogger logger, string message)
    {
        await Task.Run(() => logger.LogWarning(message));
    }

    public static async Task LogErrorAsync(this ILogger logger, string message)
    {
        await Task.Run(() => logger.LogError(message));
    }
}