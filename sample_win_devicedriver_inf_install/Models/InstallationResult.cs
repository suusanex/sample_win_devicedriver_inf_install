using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Core.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Models;

namespace sample_win_devicedriver_inf_install.Core.Models;

/// <summary>
/// インストール結果モデル（コア層 - FR-014準拠）
/// ローカライズされたメッセージを含まない技術的インストール結果データを格納します
/// </summary>
public class InstallationResult
{
    /// <summary>
    /// セッションID
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// インストールステータス
    /// </summary>
    public InstallationStatus Status { get; set; }

    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool IsSuccessful => Status == InstallationStatus.Completed;

    /// <summary>
    /// エラー情報（失敗時）
    /// </summary>
    public ApiErrorInfo? ErrorInfo { get; set; }

    /// <summary>
    /// インストールされたドライバパッケージ
    /// </summary>
    public DriverPackage? InstalledPackage { get; set; }

    /// <summary>
    /// 影響を受けたデバイス一覧
    /// </summary>
    public List<DeviceInstance> AffectedDevices { get; set; } = new();

    /// <summary>
    /// 実行時間
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// インストール開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// インストール終了時刻
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 技術的メッセージ（FR-014によりログ用英語のみ）
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
    /// <param name="affectedDevices">影響を受けたデバイス</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <returns>成功した InstallationResult</returns>
    public static InstallationResult Success(
        string sessionId,
        DriverPackage installedPackage,
        List<DeviceInstance>? affectedDevices = null,
        TimeSpan? executionTime = null,
        string? technicalMessage = null)
    {
        return new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Completed,
            InstalledPackage = installedPackage,
            AffectedDevices = affectedDevices ?? new(),
            ExecutionTime = executionTime ?? TimeSpan.Zero,
            StartTime = DateTime.UtcNow.Subtract(executionTime ?? TimeSpan.Zero),
            EndTime = DateTime.UtcNow,
            TechnicalMessage = technicalMessage ?? "Driver installation completed successfully"
        };
    }

    /// <summary>
    /// 失敗したインストール結果を作成します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <param name="errorInfo">エラー情報</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <returns>失敗した InstallationResult</returns>
    public static InstallationResult Failure(
        string sessionId,
        ApiErrorInfo errorInfo,
        TimeSpan? executionTime = null,
        string? technicalMessage = null)
    {
        return new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Failed,
            ErrorInfo = errorInfo,
            ExecutionTime = executionTime ?? TimeSpan.Zero,
            StartTime = DateTime.UtcNow.Subtract(executionTime ?? TimeSpan.Zero),
            EndTime = DateTime.UtcNow,
            TechnicalMessage = technicalMessage ?? errorInfo.SystemMessage
        };
    }

    /// <summary>
    /// キャンセルされたインストール結果を作成します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <param name="executionTime">実行時間</param>
    /// <param name="technicalMessage">技術的メッセージ</param>
    /// <returns>キャンセルされた InstallationResult</returns>
    public static InstallationResult Cancelled(
        string sessionId,
        TimeSpan? executionTime = null,
        string? technicalMessage = null)
    {
        return new InstallationResult
        {
            SessionId = sessionId,
            Status = InstallationStatus.Cancelled,
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