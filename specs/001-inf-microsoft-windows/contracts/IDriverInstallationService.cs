using System.Threading;
using System.Threading.Tasks;

namespace WindowsDriverInstaller.Contracts
{
    /// <summary>
    /// Windows デバイスドライバの宣言的インストールサービス
    /// INF ファイルに従って包括的なインストール処理を実行します
    /// </summary>
    public interface IDriverInstallationService
    {
        /// <summary>
        /// INF ファイルに従ってドライバを宣言的にインストールします。
        /// 指定されたセクション（CopyFiles, AddReg 等）と関連する .Services セクション（存在する場合）を
        /// 適切な順序で自動的に適用し、完全なインストール処理を実行します。
        /// </summary>
        /// <param name="infPath">INF ファイルの絶対パス</param>
        /// <param name="sectionName">適用するセクション名（既定: "DefaultInstall"）</param>
        /// <param name="flags">必要に応じたフラグ（省略可）</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>包括的なインストール結果</returns>
        Task<InstallationResult> InstallFromInfAsync(
            string infPath,
            string sectionName = "DefaultInstall",
            uint flags = 0,
            CancellationToken cancellationToken = default);
    }
}