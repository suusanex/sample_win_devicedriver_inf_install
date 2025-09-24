using sample_win_devicedriver_inf_install.Enums;

namespace sample_win_devicedriver_inf_install.Models.ValueObjects;

/// <summary>
/// ログエントリを表す値オブジェクト
/// </summary>
/// <param name="Level">ログレベル</param>
/// <param name="Message">メッセージ</param>
/// <param name="Category">カテゴリ</param>
/// <param name="Timestamp">タイムスタンプ</param>
/// <param name="CorrelationId">相関ID</param>
/// <param name="Exception">例外情報（オプション）</param>
/// <param name="Properties">追加プロパティ</param>
public readonly record struct LogEntry(
    LogLevel Level,
    string Message,
    string Category,
    DateTime Timestamp,
    string CorrelationId,
    Exception? Exception = null,
    Dictionary<string, object>? Properties = null
)
{
    /// <summary>
    /// 詳細情報（CLI用）
    /// </summary>
    public string? Details => Exception?.ToString() ?? Properties?.ToString();

    /// <summary>
    /// ログエントリを作成します
    /// </summary>
    /// <param name="level">ログレベル</param>
    /// <param name="message">メッセージ</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="correlationId">相関ID</param>
    /// <param name="exception">例外</param>
    /// <param name="properties">追加プロパティ</param>
    /// <returns>LogEntry インスタンス</returns>
    public static LogEntry Create(
        LogLevel level,
        string message,
        string category,
        string correlationId,
        Exception? exception = null,
        Dictionary<string, object>? properties = null)
        => new(level, message, category, DateTime.UtcNow, correlationId, exception, properties);

    /// <summary>
    /// 情報レベルのログエントリを作成します
    /// </summary>
    public static LogEntry Information(string message, string category, string correlationId, Dictionary<string, object>? properties = null)
        => Create(LogLevel.Information, message, category, correlationId, null, properties);

    /// <summary>
    /// エラーレベルのログエントリを作成します
    /// </summary>
    public static LogEntry Error(string message, string category, string correlationId, Exception? exception = null, Dictionary<string, object>? properties = null)
        => Create(LogLevel.Error, message, category, correlationId, exception, properties);

    /// <summary>
    /// 警告レベルのログエントリを作成します
    /// </summary>
    public static LogEntry Warning(string message, string category, string correlationId, Dictionary<string, object>? properties = null)
        => Create(LogLevel.Warning, message, category, correlationId, null, properties);
}