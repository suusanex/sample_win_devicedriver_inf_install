using System;
using System.Runtime.InteropServices;
using System.Text;

namespace sample_win_devicedriver_inf_install.Native;

/// <summary>
/// Windows Setup API の P/Invoke ラッパー（宣言的インストール専用）
/// </summary>
public static class SetupApi
{
    #region Constants

    public const int MAX_PATH = 260;
    
    // SetupInstallFromInfSection flags
    public const uint SPINST_LOGCONFIG = 0x00000001;
    public const uint SPINST_INIFILES = 0x00000002;
    public const uint SPINST_REGISTRY = 0x00000004;
    public const uint SPINST_INI2REG = 0x00000008;
    public const uint SPINST_FILES = 0x00000010;
    public const uint SPINST_BITREG = 0x00000020;
    public const uint SPINST_ALL = 0x000000FF;

    #endregion

    #region SetupAPI Functions for Declarative Installation

    /// <summary>
    /// SetupOpenInfFileW function
    /// INF ファイルを開きます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupOpenInfFile(
        string FileName,
        string InfClass,
        uint InfStyle,
        out uint ErrorLine);

    /// <summary>
    /// SetupInstallFromInfSectionW function
    /// INF セクションからインストールを実行します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupInstallFromInfSection(
        IntPtr Owner,
        IntPtr InfHandle,
        string SectionName,
        uint Flags,
        IntPtr RelativeKeyRoot,
        string SourceRootPath,
        uint CopyFlags,
        IntPtr MsgHandler,
        IntPtr Context,
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData);

    /// <summary>
    /// SetupInstallServicesFromInfSectionW function
    /// INF の Services セクションからサービスをインストールします
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupInstallServicesFromInfSection(
        IntPtr InfHandle,
        string SectionName,
        uint Flags);

    /// <summary>
    /// SetupCloseInfFile function
    /// INF ファイルハンドルを閉じます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern void SetupCloseInfFile(IntPtr InfHandle);

    /// <summary>
    /// SetupFindFirstLineW function
    /// INF ファイル内で指定されたセクションの最初の行を検索します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupFindFirstLine(
        IntPtr InfHandle,
        string Section,
        string Key,
        out INFCONTEXT Context);

    #endregion

    #region Kernel32 Functions

    /// <summary>
    /// GetLastError function
    /// 最後のエラーコードを取得します
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();

    /// <summary>
    /// FormatMessage function
    /// エラーコードをメッセージに変換します
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint FormatMessage(
        uint dwFlags,
        IntPtr lpSource,
        uint dwMessageId,
        uint dwLanguageId,
        StringBuilder lpBuffer,
        uint nSize,
        IntPtr Arguments);

    // FormatMessage flags
    public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
    public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;

    #endregion

    #region Structures

    /// <summary>
    /// INFCONTEXT structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INFCONTEXT
    {
        public IntPtr Inf;
        public IntPtr CurrentInf;
        public uint Section;
        public uint Line;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// INVALID_HANDLE_VALUE constant
    /// </summary>
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    /// <summary>
    /// INF Style flags
    /// </summary>
    public const uint INF_STYLE_WIN4 = 0x00000002;

    #endregion
}