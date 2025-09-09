using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Models;

namespace sample_win_devicedriver_inf_install.Core.Contracts;

/// <summary>
/// ドライバインストールサービスインターフェース（コア層 - FR-014準拠）
/// UI関連の処理を含まない、コアなドライバインストール操作を提供します
/// </summary>
public interface IDriverInstallationService
{
    /// <summary>
    /// ドライバを非同期でインストールします
    /// </summary>
    /// <param name="driverPackage">インストールするドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>インストール結果</returns>
    Task<InstallationResult> InstallDriverAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ドライバを非同期でアンインストールします
    /// </summary>
    /// <param name="driverPackage">アンインストールするドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>アンインストール結果</returns>
    Task<InstallationResult> UninstallDriverAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ドライバの状態を非同期で取得します
    /// </summary>
    /// <param name="driverPackage">確認するドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>ドライバに関連するデバイス一覧</returns>
    Task<List<DeviceInstance>> GetDriverStatusAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// インストールセッションを作成します
    /// </summary>
    /// <param name="driverPackage">ドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>作成されたセッション</returns>
    InstallationSession CreateSession(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// アクティブなセッション一覧を取得します
    /// </summary>
    /// <returns>アクティブなセッション一覧</returns>
    IEnumerable<InstallationSession> GetActiveSessions();

    /// <summary>
    /// 指定されたセッションを取得します
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    /// <returns>セッション（見つからない場合はnull）</returns>
    InstallationSession? GetSession(string sessionId);
}