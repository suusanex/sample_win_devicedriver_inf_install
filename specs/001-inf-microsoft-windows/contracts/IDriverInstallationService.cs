using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsDriverInstaller.Contracts
{
    /// <summary>
    /// Windows デバイスドライバインストールサービスのコントラクト
    /// </summary>
    public interface IDriverInstallationService
    {
        /// <summary>
        /// ドライバパッケージをインストールします
        /// </summary>
        /// <param name="driverPackage">インストールするドライバパッケージ</param>
        /// <param name="options">インストールオプション</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>インストール結果</returns>
        Task<InstallationResult> InstallDriverAsync(
            DriverPackage driverPackage, 
            InstallationOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ドライバパッケージをアンインストールします
        /// </summary>
        /// <param name="driverPackage">アンインストールするドライバパッケージ</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>アンインストール結果</returns>
        Task<InstallationResult> UninstallDriverAsync(
            DriverPackage driverPackage,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// インストール済みドライバの状態を確認します
        /// </summary>
        /// <param name="hardwareId">ハードウェア識別子</param>
        /// <returns>ドライバ状態</returns>
        Task<DriverStatus> GetDriverStatusAsync(string hardwareId);

        /// <summary>
        /// インストール可能なドライバを検索します
        /// </summary>
        /// <param name="hardwareId">ハードウェア識別子</param>
        /// <returns>利用可能なドライバリスト</returns>
        Task<IEnumerable<DriverPackage>> FindCompatibleDriversAsync(string hardwareId);

        /// <summary>
        /// インストールセッションを開始します
        /// </summary>
        /// <param name="driverPackage">対象ドライバパッケージ</param>
        /// <returns>セッション識別子</returns>
        Guid StartInstallationSession(DriverPackage driverPackage);

        /// <summary>
        /// インストールセッションの進捗を取得します
        /// </summary>
        /// <param name="sessionId">セッション識別子</param>
        /// <returns>進捗情報</returns>
        InstallationProgress GetInstallationProgress(Guid sessionId);

        /// <summary>
        /// インストールセッションをキャンセルします
        /// </summary>
        /// <param name="sessionId">セッション識別子</param>
        /// <returns>キャンセル成功フラグ</returns>
        Task<bool> CancelInstallationAsync(Guid sessionId);
    }
}