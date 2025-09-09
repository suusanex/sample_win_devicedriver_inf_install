using System;
using System.Runtime.InteropServices;
using System.Text;

namespace sample_win_devicedriver_inf_install.Native;

/// <summary>
/// Windows Setup API の P/Invoke ラッパー
/// </summary>
public static class SetupApi
{
    #region Constants

    public const int MAX_PATH = 260;
    public const int LINE_LEN = 256;
    
    // SetupCopyOEMInf flags
    public const uint SPOST_NONE = 0x00000000;
    public const uint SPOST_PATH = 0x00000001;
    public const uint SPOST_URL = 0x00000002;
    
    // Device installation parameters flags
    public const uint DI_QUIET = 0x00800000;
    public const uint DI_NOFILECOPY = 0x01000000;
    public const uint DI_FORCECOPY = 0x02000000;
    
    // Class installer function codes
    public const uint DIF_SELECTDEVICE = 0x00000001;
    public const uint DIF_INSTALLDEVICE = 0x00000002;
    public const uint DIF_ASSIGNRESOURCES = 0x00000003;
    public const uint DIF_PROPERTIES = 0x00000004;
    public const uint DIF_REMOVE = 0x00000005;
    public const uint DIF_FIRSTTIMESETUP = 0x00000006;
    public const uint DIF_FOUNDDEVICE = 0x00000007;
    public const uint DIF_SELECTCLASSDRIVERS = 0x00000008;
    public const uint DIF_VALIDATECLASSDRIVERS = 0x00000009;
    public const uint DIF_INSTALLCLASSDRIVERS = 0x0000000A;
    public const uint DIF_CALCDISKSPACE = 0x0000000B;
    public const uint DIF_DESTROYPRIVATEDATA = 0x0000000C;
    public const uint DIF_VALIDATEDRIVER = 0x0000000D;
    public const uint DIF_MOVEDEVICE = 0x0000000E;
    public const uint DIF_DETECT = 0x0000000F;
    public const uint DIF_INSTALLWIZARD = 0x00000010;
    public const uint DIF_DESTROYWIZARDDATA = 0x00000011;
    public const uint DIF_PROPERTYCHANGE = 0x00000012;
    public const uint DIF_ENABLECLASS = 0x00000013;
    public const uint DIF_DETECTVERIFY = 0x00000014;
    public const uint DIF_INSTALLDEVICEFILES = 0x00000015;
    public const uint DIF_UNREMOVE = 0x00000016;
    public const uint DIF_SELECTBESTCOMPATDRV = 0x00000017;
    public const uint DIF_ALLOW_INSTALL = 0x00000018;
    public const uint DIF_REGISTERDEVICE = 0x00000019;
    public const uint DIF_NEWDEVICEWIZARD_PRESELECT = 0x0000001A;
    public const uint DIF_NEWDEVICEWIZARD_SELECT = 0x0000001B;
    public const uint DIF_NEWDEVICEWIZARD_PREANALYZE = 0x0000001C;
    public const uint DIF_NEWDEVICEWIZARD_POSTANALYZE = 0x0000001D;
    public const uint DIF_NEWDEVICEWIZARD_FINISHINSTALL = 0x0000001E;

    // DIGCF flags for SetupDiGetClassDevs
    public const uint DIGCF_DEFAULT = 0x00000001;
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_PROFILE = 0x00000008;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    // Registry property codes
    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_COMPATIBLEIDS = 0x00000002;
    public const uint SPDRP_SERVICE = 0x00000004;
    public const uint SPDRP_CLASS = 0x00000007;
    public const uint SPDRP_CLASSGUID = 0x00000008;
    public const uint SPDRP_DRIVER = 0x00000009;
    public const uint SPDRP_CONFIGFLAGS = 0x0000000A;
    public const uint SPDRP_MFG = 0x0000000B;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    public const uint SPDRP_LOCATION_INFORMATION = 0x0000000D;
    public const uint SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E;
    public const uint SPDRP_CAPABILITIES = 0x0000000F;
    public const uint SPDRP_UI_NUMBER = 0x00000010;
    public const uint SPDRP_UPPERFILTERS = 0x00000011;
    public const uint SPDRP_LOWERFILTERS = 0x00000012;
    public const uint SPDRP_BUSTYPEGUID = 0x00000013;
    public const uint SPDRP_LEGACYBUSTYPE = 0x00000014;
    public const uint SPDRP_BUSNUMBER = 0x00000015;
    public const uint SPDRP_ENUMERATOR_NAME = 0x00000016;

    #endregion

    #region Structures

    /// <summary>
    /// SP_DEVINFO_DATA structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    /// <summary>
    /// SP_DEVICE_INTERFACE_DATA structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    /// <summary>
    /// SP_DEVICE_INTERFACE_DETAIL_DATA structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public uint cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string DevicePath;
    }

    /// <summary>
    /// SP_DRVINFO_DATA structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DRVINFO_DATA
    {
        public uint cbSize;
        public uint DriverType;
        public IntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = LINE_LEN)]
        public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = LINE_LEN)]
        public string MfgName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = LINE_LEN)]
        public string ProviderName;
        public System.Runtime.InteropServices.ComTypes.FILETIME DriverDate;
        public ulong DriverVersion;
    }

    #endregion

    #region SetupAPI Functions

    /// <summary>
    /// SetupCopyOEMInf function
    /// INF ファイルをシステムの INF ディレクトリにコピーします
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupCopyOEMInf(
        string SourceInfFileName,
        string OEMSourceMediaLocation,
        uint OEMSourceMediaType,
        uint CopyStyle,
        StringBuilder DestinationInfFileName,
        uint DestinationInfFileNameSize,
        out uint RequiredSize,
        out StringBuilder DestinationInfFileNameComponent);

    /// <summary>
    /// SetupDiCallClassInstaller function
    /// クラスインストーラを呼び出します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiCallClassInstaller(
        uint InstallFunction,
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData);

    /// <summary>
    /// SetupDiGetClassDevs function
    /// 指定されたクラスのデバイス情報セットを取得します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        string Enumerator,
        IntPtr hwndParent,
        uint Flags);

    /// <summary>
    /// SetupDiEnumDeviceInfo function
    /// デバイス情報セット内のデバイス情報要素を列挙します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        IntPtr DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData);

    /// <summary>
    /// SetupDiGetDeviceRegistryProperty function
    /// デバイスのレジストリプロパティを取得します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    /// <summary>
    /// SetupDiDestroyDeviceInfoList function
    /// デバイス情報セットを破棄します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    /// <summary>
    /// SetupDiCreateDeviceInfoList function
    /// 空のデバイス情報セットを作成します
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiCreateDeviceInfoList(
        ref Guid ClassGuid,
        IntPtr hwndParent);

    /// <summary>
    /// SetupDiOpenDevRegKey function
    /// デバイスのレジストリキーを開きます
    /// </summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiOpenDevRegKey(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Scope,
        uint HwProfile,
        uint KeyType,
        uint samDesired);

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
    public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
    public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
    public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;

    #endregion

    #region Helper Methods

    /// <summary>
    /// INVALID_HANDLE_VALUE constant
    /// </summary>
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    /// <summary>
    /// SP_DEVINFO_DATA のサイズを初期化します
    /// </summary>
    /// <returns>初期化された SP_DEVINFO_DATA</returns>
    public static SP_DEVINFO_DATA CreateDevInfoData()
    {
        var devInfoData = new SP_DEVINFO_DATA();
        devInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
        return devInfoData;
    }

    /// <summary>
    /// SP_DEVICE_INTERFACE_DATA のサイズを初期化します
    /// </summary>
    /// <returns>初期化された SP_DEVICE_INTERFACE_DATA</returns>
    public static SP_DEVICE_INTERFACE_DATA CreateDeviceInterfaceData()
    {
        var devInterfaceData = new SP_DEVICE_INTERFACE_DATA();
        devInterfaceData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
        return devInterfaceData;
    }

    /// <summary>
    /// SP_DRVINFO_DATA のサイズを初期化します
    /// </summary>
    /// <returns>初期化された SP_DRVINFO_DATA</returns>
    public static SP_DRVINFO_DATA CreateDriverInfoData()
    {
        var drvInfoData = new SP_DRVINFO_DATA();
        drvInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DRVINFO_DATA));
        return drvInfoData;
    }

    #endregion
}