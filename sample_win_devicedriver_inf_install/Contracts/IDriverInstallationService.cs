using sample_win_devicedriver_inf_install.Models;

namespace sample_win_devicedriver_inf_install.Contracts;

/// <summary>
/// ドライバインストールサービスインターフェース（宣言的インストール専用）
/// </summary>
public interface IDriverInstallationService
{
    /// <summary>
    /// INFファイルに従った包括的な宣言的インストールを実行します
    /// </summary>
    /// <param name="infPath">INFファイルパス</param>
    /// <param name="sectionName">インストールセクション名（デフォルト: "DefaultInstall"）</param>
    /// <param name="flags">インストールフラグ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>インストール結果</returns>
    Task<InstallationResult> InstallFromInfAsync(
        string infPath,
        string sectionName = "DefaultInstall",
        uint flags = 0,
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