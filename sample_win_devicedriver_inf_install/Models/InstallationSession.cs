using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;

namespace sample_win_devicedriver_inf_install.Models;

/// <summary>
/// インストールセッション情報を表すモデル（宣言的インストール専用）
/// </summary>
public class InstallationSession
{
    /// <summary>
    /// セッションID（相関ID）
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// ドライバパッケージ
    /// </summary>
    public required DriverPackage DriverPackage { get; set; }

    /// <summary>
    /// 現在のステータス
    /// </summary>
    public InstallationStatus Status { get; set; } = InstallationStatus.Pending;

    /// <summary>
    /// 開始日時
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 終了日時
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 進捗率（0-100）
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// 現在のステップ説明
    /// </summary>
    public string? CurrentStep { get; set; }

    /// <summary>
    /// エラー情報（失敗時）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// キャンセレーショントークン
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// セッションの経過時間を取得します
    /// </summary>
    public TimeSpan ElapsedTime => CompletedAt?.Subtract(StartedAt) ?? DateTime.UtcNow.Subtract(StartedAt);

    /// <summary>
    /// セッションが完了状態かどうかを判定します
    /// </summary>
    public bool IsCompleted => Status is InstallationStatus.Completed or InstallationStatus.Failed or InstallationStatus.Cancelled or InstallationStatus.TimedOut;

    /// <summary>
    /// セッションが成功状態かどうかを判定します
    /// </summary>
    public bool IsSuccessful => Status == InstallationStatus.Completed;

    /// <summary>
    /// 新しいインストールセッションを作成します
    /// </summary>
    /// <param name="driverPackage">ドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>新しい InstallationSession インスタンス</returns>
    public static InstallationSession Create(DriverPackage driverPackage, CancellationToken cancellationToken = default)
    {
        return new InstallationSession
        {
            SessionId = Guid.NewGuid().ToString(),
            DriverPackage = driverPackage,
            CancellationToken = cancellationToken
        };
    }

    /// <summary>
    /// 進捗を更新します
    /// </summary>
    /// <param name="percentage">進捗率（0-100）</param>
    /// <param name="step">現在のステップ</param>
    public void UpdateProgress(int percentage, string? step = null)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        CurrentStep = step;
    }

    /// <summary>
    /// セッションを完了状態にします
    /// </summary>
    /// <param name="status">完了ステータス</param>
    /// <param name="errorMessage">エラーメッセージ（失敗時）</param>
    public void Complete(InstallationStatus status, string? errorMessage = null)
    {
        Status = status;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        
        if (status == InstallationStatus.Completed)
        {
            ProgressPercentage = 100;
        }
    }
}