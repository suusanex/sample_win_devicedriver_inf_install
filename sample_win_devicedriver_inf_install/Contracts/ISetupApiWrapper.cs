using System;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Contracts;

/// <summary>
/// SetupAPI操作の抽象化インターフェース
/// UnitTest時にスタブ化するために使用
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
        System.Text.StringBuilder lpBuffer,
        uint nSize,
        IntPtr arguments);
}