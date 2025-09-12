using System;
using System.Text;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// SetupAPIの実際の実装
/// 本番環境で使用される
/// </summary>
public class SetupApiWrapper : ISetupApiWrapper
{
    /// <summary>
    /// INFファイルを開きます
    /// </summary>
    public IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine)
    {
        return SetupApi.SetupOpenInfFile(fileName, infClass, infStyle, out errorLine);
    }

    /// <summary>
    /// INFセクションからインストールを実行します
    /// </summary>
    public bool SetupInstallFromInfSection(
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
        IntPtr deviceInfoData)
    {
        return SetupApi.SetupInstallFromInfSection(
            owner,
            infHandle,
            sectionName,
            flags,
            relativeKeyRoot,
            sourceRootPath,
            copyFlags,
            msgHandler,
            context,
            deviceInfoSet,
            deviceInfoData);
    }

    /// <summary>
    /// Services セクションからサービスをインストールします
    /// </summary>
    public bool SetupInstallServicesFromInfSection(IntPtr infHandle, string sectionName, uint flags)
    {
        return SetupApi.SetupInstallServicesFromInfSection(infHandle, sectionName, flags);
    }

    /// <summary>
    /// INFファイルハンドルを閉じます
    /// </summary>
    public void SetupCloseInfFile(IntPtr infHandle)
    {
        SetupApi.SetupCloseInfFile(infHandle);
    }

    /// <summary>
    /// INFファイル内で指定されたセクションの最初の行を検索します
    /// </summary>
    public bool SetupFindFirstLine(IntPtr infHandle, string section, string? key, out SetupApi.INFCONTEXT context)
    {
        return SetupApi.SetupFindFirstLine(infHandle, section, key, out context);
    }

    /// <summary>
    /// 最後のエラーコードを取得します
    /// </summary>
    public uint GetLastError()
    {
        return SetupApi.GetLastError();
    }

    /// <summary>
    /// エラーコードをメッセージに変換します
    /// </summary>
    public uint FormatMessage(
        uint dwFlags,
        IntPtr lpSource,
        uint dwMessageId,
        uint dwLanguageId,
        StringBuilder lpBuffer,
        uint nSize,
        IntPtr arguments)
    {
        return SetupApi.FormatMessage(dwFlags, lpSource, dwMessageId, dwLanguageId, lpBuffer, nSize, arguments);
    }
}