using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Models;

namespace sample_win_devicedriver_inf_install.Models;

/// <summary>
/// インストール結果モデル（宣言的インストール専用）
/// </summary>
public class InstallationResult
{
    /// <summary>
    /// セッションID
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// 相関ID（CLIで使用）
    /// </summary>
    public string CorrelationId => SessionId;

    /// <summary>
    /// インストールステータス
    /// </summary>
    public InstallationStatus Status { get; set; }

    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool IsSuccessful => Status == InstallationStatus.Completed;

    /// <summary>
    /// 成功フラグ（テスト用プロパティ）
    /// </summary>
    public bool IsSuccess => IsSuccessful;

    /// <summary>
    /// エラー情報（失敗時）
    /// </summary>
    public ApiErrorInfo? ErrorInfo { get; set; }

    /// <summary>
    /// エラー一覧（CLI用）
    /// </summary>
    public IEnumerable<ApiErrorInfo> Errors => ErrorInfo.HasValue ? new[] { ErrorInfo.Value } : Array.Empty<ApiErrorInfo>();

    /// <summary>
    /// インストールされたドライバパッケージ
    /// </summary>
    public DriverPackage? InstalledPackage { get; set; }

    /// <summary>
    /// INFファイルのパス（CLI用）
    /// </summary>
    public string? InfPath => InstalledPackage?.InfPath;

    /// <summary>
    /// セクション名（CLI用）
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// 実行時間
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// 実行時間（CLI用）
    /// </summary>
    public TimeSpan Duration => ExecutionTime;

    /// <summary>
    /// インストール開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// インストール終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 技術的メッセージ（ログ用英語のみ）
    /// </summary>
    public string? TechnicalMessage { get; set; }

    /// <summary>
    /// ログエントリ一覧
    /// </summary>
    public List<LogEntry> LogEntries { get; set; } = new();

    /// <summary>
    /// 成功したインストール結果を作成します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <param name="installedPackage">インストールされたパッケージ</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <returns>成功した InstallationResult</returns>
    public static InstallationResult Success(
        string sessionId,
        DriverPackage installedPackage,
        string? sectionName = null,
        TimeSpan? executionTime = null,
        string? technicalMessage = null)
    {
        var actualExecutionTime = executionTime ?? TimeSpan.Zero;
        var result = new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Completed,
            InstalledPackage = installedPackage,
            SectionName = sectionName ?? "DefaultInstall",
            ExecutionTime = actualExecutionTime,
            StartTime = DateTime.UtcNow.Subtract(actualExecutionTime),
            EndTime = DateTime.UtcNow,
            TechnicalMessage = technicalMessage ?? "Driver installation completed successfully"
        };
        
        return result;
    }

    /// <summary>
    /// 失敗したインストール結果を作成します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <param name="errorInfo">エラー情報</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <param name="installedPackage">関連するドライバパッケージ（失敗時でもパス等の参照に使用）</param>
    /// <returns>失敗した InstallationResult</returns>
    public static InstallationResult Failure(
        string sessionId,
        ApiErrorInfo errorInfo,
        string? sectionName = null,
        TimeSpan? executionTime = null,
        string? technicalMessage = null,
        DriverPackage? installedPackage = null)
    {
        return new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Failed,
            ErrorInfo = errorInfo,
            SectionName = sectionName,
            ExecutionTime = executionTime ?? TimeSpan.Zero,
            StartTime = DateTime.UtcNow.Subtract(executionTime ?? TimeSpan.Zero),
            EndTime = DateTime.UtcNow,
            TechnicalMessage = technicalMessage ?? errorInfo.SystemMessage,
            InstalledPackage = installedPackage
        };
    }

    /// <summary>
    /// キャンセルされたインストール結果を作成します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <returns>キャンセルされた InstallationResult</returns>
    public static InstallationResult Cancelled(
        string sessionId,
        string? sectionName = null,
        TimeSpan? executionTime = null,
        string? technicalMessage = null)
    {
        return new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Cancelled,
            SectionName = sectionName,
            ExecutionTime = executionTime ?? TimeSpan.Zero,
            StartTime = DateTime.UtcNow.Subtract(executionTime ?? TimeSpan.Zero),
            EndTime = DateTime.UtcNow,
            TechnicalMessage = technicalMessage ?? "Installation was cancelled"
        };
    }

    /// <summary>
    /// ログエントリを追加します
    /// </summary>
    /// <param name="logEntry">ログエントリ</param>
    public void AddLogEntry(LogEntry logEntry)
    {
        LogEntries.Add(logEntry);
    }

    /// <summary>
    /// 複数のログエントリを追加します
    /// </summary>
    /// <param name="logEntries">ログエントリ一覧</param>
    public void AddLogEntries(IEnumerable<LogEntry> logEntries)
    {
        LogEntries.AddRange(logEntries);
    }

    /// <summary>
    /// ログ用の技術的サマリを取得します
    /// </summary>
    /// <returns>技術的サマリ</returns>
    public string GetTechnicalSummary()
    {
        var summary = $"Session: {SessionId}, Status: {Status}, Duration: {ExecutionTime:c}";
        
        if (ErrorInfo.HasValue)
        {
            summary += $", Error: {ErrorInfo.Value.GetLogMessage()}";
        }
        
        if (InstalledPackage != null)
        {
            summary += $", Package: {InstalledPackage.InfPath}";
        }
        
        return summary;
    }
}