using sample_win_devicedriver_inf_install.Models.ValueObjects;

namespace sample_win_devicedriver_inf_install.Core.Contracts;

/// <summary>
/// インストールログサービスインターフェース（コア層 - FR-014準拠）
/// 技術的ログ記録機能を提供します（英語ログ出力）
/// </summary>
public interface IInstallationLogger
{
    /// <summary>
    /// ログエントリを記録します
    /// </summary>
    /// <param name="logEntry">ログエントリ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task LogAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 複数のログエントリを記録します
    /// </summary>
    /// <param name="logEntries">ログエントリ一覧</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task LogAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default);

    /// <summary>
    /// 情報レベルのログを記録します（英語メッセージ）
    /// </summary>
    /// <param name="message">メッセージ（英語）</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="correlationId">相関ID</param>
    /// <param name="properties">追加プロパティ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task LogInformationAsync(
        string message,
        string category,
        string correlationId,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// エラーレベルのログを記録します（英語メッセージ）
    /// </summary>
    /// <param name="message">メッセージ（英語）</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="correlationId">相関ID</param>
    /// <param name="exception">例外</param>
    /// <param name="properties">追加プロパティ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task LogErrorAsync(
        string message,
        string category,
        string correlationId,
        Exception? exception = null,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 警告レベルのログを記録します（英語メッセージ）
    /// </summary>
    /// <param name="message">メッセージ（英語）</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="correlationId">相関ID</param>
    /// <param name="properties">追加プロパティ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task LogWarningAsync(
        string message,
        string category,
        string correlationId,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定された相関IDのログエントリを取得します
    /// </summary>
    /// <param name="correlationId">相関ID</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>ログエントリ一覧</returns>
    Task<IEnumerable<LogEntry>> GetLogEntriesAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ログをファイル出力します
    /// </summary>
    /// <param name="correlationId">相関ID</param>
    /// <param name="filePath">出力ファイルパス</param>
    /// <param name="format">出力形式（json, xml, csv）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task ExportLogsAsync(
        string correlationId,
        string filePath,
        string format = "json",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 古いログエントリを削除します
    /// </summary>
    /// <param name="olderThan">この日時より古いエントリを削除</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task CleanupOldLogsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}