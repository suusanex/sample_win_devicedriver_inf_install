using System;
using System.Text;
using sample_win_devicedriver_inf_install.Native;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install.Contracts;

/// <summary>
/// SetupAPI操作の抽象化インターフェース
/// テスト時にはスタブ、本番時には実装を使用
/// </summary>
public interface ISetupApiWrapper
{
    /// <summary>
    /// INFファイルを開きます
    /// </summary>
    /// <param name="fileName">INFファイルパス</param>
    /// <param name="infClass">INFクラス</param>
    /// <param name="infStyle">INFスタイル</param>
    /// <param name="errorLine">エラー行番号（出力）</param>
    /// <returns>INFハンドル</returns>
    IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine);

    /// <summary>
    /// INFセクションからインストールを実行します
    /// </summary>
    /// <param name="owner">オーナーウィンドウ</param>
    /// <param name="infHandle">INFハンドル</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="flags">フラグ</param>
    /// <param name="relativeKeyRoot">相対キールート</param>
    /// <param name="sourceRootPath">ソースルートパス</param>
    /// <param name="copyFlags">コピーフラグ</param>
    /// <param name="msgHandler">メッセージハンドラー</param>
    /// <param name="context">コンテキスト</param>
    /// <param name="deviceInfoSet">デバイス情報セット</param>
    /// <param name="deviceInfoData">デバイス情報データ</param>
    /// <returns>成功の場合true</returns>
    bool SetupInstallFromInfSection(
        IntPtr owner,
        IntPtr infHandle,
        string sectionName,
        uint flags,
        IntPtr relativeKeyRoot,
        string? sourceRootPath,
        uint copyFlags,
        IntPtr msgHandler,
        IntPtr context,
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData);

    /// <summary>
    /// Services セクションからサービスをインストールします
    /// </summary>
    /// <param name="infHandle">INFハンドル</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="flags">フラグ</param>
    /// <returns>成功の場合true</returns>
    bool SetupInstallServicesFromInfSection(IntPtr infHandle, string sectionName, uint flags);

    /// <summary>
    /// INFファイルハンドルを閉じます
    /// </summary>
    /// <param name="infHandle">INFハンドル</param>
    void SetupCloseInfFile(IntPtr infHandle);

    /// <summary>
    /// INFファイル内で指定されたセクションの最初の行を検索します
    /// </summary>
    /// <param name="infHandle">INFハンドル</param>
    /// <param name="section">セクション名</param>
    /// <param name="key">キー名</param>
    /// <param name="context">コンテキスト（出力）</param>
    /// <returns>見つかった場合true</returns>
    bool SetupFindFirstLine(IntPtr infHandle, string section, string? key, out SetupApi.INFCONTEXT context);

    #region File Queue Operations for CopyFiles Support

    /// <summary>
    /// ファイルキューを開きます
    /// </summary>
    /// <returns>ファイルキューハンドル</returns>
    IntPtr SetupOpenFileQueue();

    /// <summary>
    /// INF セクションからファイル操作をキューに追加します
    /// </summary>
    /// <param name="infHandle">INFハンドル</param>
    /// <param name="layoutInfHandle">レイアウトINFハンドル</param>
    /// <param name="fileQueue">ファイルキューハンドル</param>
    /// <param name="sectionName">セクション名</param>
    /// <param name="sourceRootPath">ソースルートパス</param>
    /// <param name="copyStyle">コピースタイル</param>
    /// <returns>成功の場合true</returns>
    bool SetupInstallFilesFromInfSection(
        IntPtr infHandle,
        IntPtr layoutInfHandle,
        IntPtr fileQueue,
        string sectionName,
        string? sourceRootPath,
        uint copyStyle);

    /// <summary>
    /// ファイルキューをコミット（実際にファイル操作を実行）します
    /// </summary>
    /// <param name="owner">オーナーウィンドウ</param>
    /// <param name="queueHandle">キューハンドル</param>
    /// <param name="msgHandler">メッセージハンドラー</param>
    /// <param name="context">コンテキスト</param>
    /// <returns>成功の場合true</returns>
    bool SetupCommitFileQueue(
        IntPtr owner,
        IntPtr queueHandle,
        IntPtr msgHandler,
        IntPtr context);

    /// <summary>
    /// ファイルキューをサイレントコールバックでコミットします（FR-012準拠）
    /// </summary>
    /// <param name="owner">オーナーハンドル</param>
    /// <param name="queueHandle">キューハンドル</param>
    /// <param name="silentCallback">サイレント用コールバック</param>
    /// <returns>成功時true</returns>
    bool SetupCommitFileQueueWithSilentCallback(
        IntPtr owner,
        IntPtr queueHandle,
        SilentFileQueueCallback silentCallback);

    /// <summary>
    /// ファイルキューを閉じます
    /// </summary>
    /// <param name="queueHandle">キューハンドル</param>
    /// <returns>成功の場合true</returns>
    bool SetupCloseFileQueue(IntPtr queueHandle);

    #endregion

    /// <summary>
    /// 最後のエラーコードを取得します
    /// </summary>
    /// <returns>エラーコード</returns>
    uint GetLastError();

    /// <summary>
    /// エラーコードをメッセージに変換します
    /// </summary>
    /// <param name="dwFlags">フラグ</param>
    /// <param name="lpSource">ソース</param>
    /// <param name="dwMessageId">メッセージID</param>
    /// <param name="dwLanguageId">言語ID</param>
    /// <param name="lpBuffer">バッファ</param>
    /// <param name="nSize">サイズ</param>
    /// <param name="arguments">引数</param>
    /// <returns>取得した文字数</returns>
    uint FormatMessage(
        uint dwFlags,
        IntPtr lpSource,
        uint dwMessageId,
        uint dwLanguageId,
        StringBuilder lpBuffer,
        uint nSize,
        IntPtr arguments);
}