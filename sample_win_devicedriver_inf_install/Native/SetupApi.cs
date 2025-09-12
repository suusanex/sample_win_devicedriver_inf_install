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

    // File queue copy flags
    public const uint SP_COPY_DELETESOURCE = 0x0000001;
    public const uint SP_COPY_REPLACEONLY = 0x0000002;
    public const uint SP_COPY_NEWER = 0x0000004;
    public const uint SP_COPY_NEWER_OR_SAME = 0x0000008;
    public const uint SP_COPY_NOOVERWRITE = 0x0000010;
    public const uint SP_COPY_NODECOMP = 0x0000020;
    public const uint SP_COPY_LANGUAGEAWARE = 0x0000040;
    public const uint SP_COPY_SOURCE_ABSOLUTE = 0x0000080;
    public const uint SP_COPY_SOURCEPATH_ABSOLUTE = 0x0000100;
    public const uint SP_COPY_IN_USE_NEEDS_REBOOT = 0x0000200;
    public const uint SP_COPY_FORCE_IN_USE = 0x0000400;
    public const uint SP_COPY_NOSKIP = 0x0000800;
    public const uint SP_COPY_FORCE_NOOVERWRITE = 0x0001000;
    public const uint SP_COPY_FORCE_NEWER = 0x0002000;
    public const uint SP_COPY_WARNIFSKIP = 0x0004000;
    public const uint SP_COPY_NOBROWSE = 0x0008000;
    public const uint SP_COPY_NEWER_ONLY = 0x0010000;
    public const uint SP_COPY_RESERVED = 0x0020000;
    public const uint SP_COPY_OEMINF_CATALOG_ONLY = 0x0040000;
    public const uint SP_COPY_REPLACE_BOOT_FILE = 0x0080000;
    public const uint SP_COPY_NOPRUNE = 0x0100000;

    // File Queue Notification values
    public const uint SPFILENOTIFY_STARTQUEUE = 0x00000001;
    public const uint SPFILENOTIFY_ENDQUEUE = 0x00000002;
    public const uint SPFILENOTIFY_STARTSUBQUEUE = 0x00000003;
    public const uint SPFILENOTIFY_ENDSUBQUEUE = 0x00000004;
    public const uint SPFILENOTIFY_STARTDELETE = 0x00000005;
    public const uint SPFILENOTIFY_ENDDELETE = 0x00000006;
    public const uint SPFILENOTIFY_DELETEERROR = 0x00000007;
    public const uint SPFILENOTIFY_STARTRENAME = 0x00000008;
    public const uint SPFILENOTIFY_ENDRENAME = 0x00000009;
    public const uint SPFILENOTIFY_RENAMEERROR = 0x0000000A;
    public const uint SPFILENOTIFY_STARTCOPY = 0x0000000B;
    public const uint SPFILENOTIFY_ENDCOPY = 0x0000000C;
    public const uint SPFILENOTIFY_COPYERROR = 0x0000000D;
    public const uint SPFILENOTIFY_NEEDMEDIA = 0x0000000E;
    public const uint SPFILENOTIFY_QUEUESCAN = 0x0000000F;
    public const uint SPFILENOTIFY_CABINETINFO = 0x00000010;
    public const uint SPFILENOTIFY_FILEINCABINET = 0x00000011;
    public const uint SPfilenotify_NEEDNEWCABINET = 0x00000012;
    public const uint SPFILENOTIFY_FILEEXTRACTED = 0x00000013;
    public const uint SPFILENOTIFY_FILEOPDELAYED = 0x00000014;
    public const uint SPFILENOTIFY_STARTBACKUP = 0x00000015;
    public const uint SPFILENOTIFY_BACKUPERROR = 0x00000016;
    public const uint SPFILENOTIFY_ENDBACKUP = 0x00000017;
    public const uint SPFILENOTIFY_QUEUESCAN_EX = 0x00000018;

    // File Queue callback return values
    public const uint FILEOP_ABORT = 0;
    public const uint FILEOP_DOIT = 1;
    public const uint FILEOP_SKIP = 2;
    public const uint FILEOP_RETRY = 3;
    public const uint FILEOP_NEWPATH = 4;

    #endregion

    #region SetupAPI Functions for Declarative Installation

    /// <summary>
    /// SetupOpenInfFileW function
    /// INF ファイルを開きます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupOpenInfFile(
        string FileName,
        string? InfClass,
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
        string? SourceRootPath,
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
        string? Key,
        out INFCONTEXT Context);

    #endregion

    #region File Queue Operations for CopyFiles Support

    /// <summary>
    /// SetupOpenFileQueue function
    /// ファイルキューを開きます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupOpenFileQueue();

    /// <summary>
    /// SetupInstallFilesFromInfSectionW function
    /// INF セクションからファイル操作をキューに追加します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupInstallFilesFromInfSection(
        IntPtr InfHandle,
        IntPtr LayoutInfHandle,
        IntPtr FileQueue,
        string SectionName,
        string? SourceRootPath,
        uint CopyStyle);

    /// <summary>
    /// ファイルコピー通知のコールバック デリゲート（PSP_FILE_CALLBACK）
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate uint PSP_FILE_CALLBACK(IntPtr Context, uint Notification, IntPtr Param1, IntPtr Param2);

    /// <summary>
    /// SetupCommitFileQueue function (Callback 指定版)
    /// ファイルキューをコミット（実際にファイル操作を実行）します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupCommitFileQueue(
        IntPtr Owner,
        IntPtr QueueHandle,
        PSP_FILE_CALLBACK MsgHandler,
        IntPtr Context);

    /// <summary>
    /// SetupCommitFileQueue function (従来の IntPtr 版)
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupCommitFileQueue(
        IntPtr Owner,
        IntPtr QueueHandle,
        IntPtr MsgHandler,
        IntPtr Context);

    /// <summary>
    /// SetupDefaultQueueCallback function
    /// 既定のファイルキュー コールバック
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint SetupDefaultQueueCallback(
        IntPtr Context,
        uint Notification,
        IntPtr Param1,
        IntPtr Param2);

    /// <summary>
    /// SetupInitDefaultQueueCallback function
    /// 既定のコールバック用コンテキストを初期化
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupInitDefaultQueueCallback(IntPtr Owner);

    /// <summary>
    /// SetupTermDefaultQueueCallback function
    /// 既定のコールバック用コンテキストを解放
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern void SetupTermDefaultQueueCallback(IntPtr Context);

    /// <summary>
    /// SetupCloseFileQueue function
    /// ファイルキューを閉じます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupCloseFileQueue(IntPtr QueueHandle);

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

    /// <summary>
    /// FILEPATHS structure - ファイル操作情報
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILEPATHS
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Target;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Source;
        public uint Win32Error;
        public uint Flags;
    }

    /// <summary>
    /// SOURCE_MEDIA_W structure - NEEDMEDIA通知で使用される構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SOURCE_MEDIA_W
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Reserved;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Tagfile;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Description;
        public uint SourcePath;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SourceFile;
        public uint Flags;
    }

    /// <summary>
    /// SP_FILE_CALLBACK_W structure - NEEDMEDIA通知のParam2で渡される構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_FILE_CALLBACK_W
    {
        public IntPtr hwnd;
        public uint Notification;
        public IntPtr Param1;
        public IntPtr Param2;
    }

    /// <summary>
    /// NEWPATHINFO structure - NEEDMEDIA通知への応答で新しいパスを指定する構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NEWPATHINFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string NewPath;
    }

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