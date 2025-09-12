using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Tests.Stubs;

/// <summary>
/// SetupAPIのスタブ実装
/// UnitTest時に実際のOS環境に影響を与えることなくテストを実行するために使用
/// </summary>
public class SetupApiStub : ISetupApiWrapper
{
    private readonly Dictionary<IntPtr, InfFileInfo> _openFiles = new();
    private IntPtr _nextHandle = new IntPtr(1000);
    private uint _lastError = 0;
    private readonly Dictionary<string, Dictionary<string, List<string>>> _infSections = new();
    
    // タイムアウトテスト用の設定
    private TimeSpan _installFromInfSectionDelay = TimeSpan.Zero;
    private TimeSpan _installServicesFromInfSectionDelay = TimeSpan.Zero;

    /// <summary>
    /// スタブ設定: INFファイルの内容を設定
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="sections">セクション情報</param>
    public void SetupInfContent(string filePath, Dictionary<string, List<string>> sections)
    {
        var key = NormalizePath(filePath);
        _infSections[key] = sections;
    }

    /// <summary>
    /// スタブ設定: エラー動作を設定
    /// </summary>
    /// <param name="errorCode">返すエラーコード</param>
    public void SetupError(uint errorCode)
    {
        _lastError = errorCode;
    }

    /// <summary>
    /// スタブ設定: SetupInstallFromInfSectionの遅延時間を設定（タイムアウトテスト用）
    /// </summary>
    /// <param name="delay">遅延時間</param>
    public void SetInstallFromInfSectionDelay(TimeSpan delay)
    {
        _installFromInfSectionDelay = delay;
    }

    /// <summary>
    /// スタブ設定: SetupInstallServicesFromInfSectionの遅延時間を設定（タイムアウトテスト用）
    /// </summary>
    /// <param name="delay">遅延時間</param>
    public void SetInstallServicesFromInfSectionDelay(TimeSpan delay)
    {
        _installServicesFromInfSectionDelay = delay;
    }

    /// <summary>
    /// スタブ設定: リセット
    /// </summary>
    public void Reset()
    {
        _openFiles.Clear();
        _lastError = 0;
        _infSections.Clear();
        _nextHandle = new IntPtr(1000);
        _installFromInfSectionDelay = TimeSpan.Zero;
        _installServicesFromInfSectionDelay = TimeSpan.Zero;
    }

    public IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine)
    {
        errorLine = 0;

        if (_lastError != 0)
        {
            var error = _lastError;
            _lastError = 0; // Reset after use
            return SetupApi.INVALID_HANDLE_VALUE;
        }

        var normalizedPath = NormalizePath(fileName);
        if (!_infSections.ContainsKey(normalizedPath))
        {
            _lastError = 2; // ERROR_FILE_NOT_FOUND
            return SetupApi.INVALID_HANDLE_VALUE;
        }

        var handle = _nextHandle;
        _nextHandle = new IntPtr(_nextHandle.ToInt32() + 1);
        
        _openFiles[handle] = new InfFileInfo
        {
            FilePath = fileName,
            Sections = _infSections[normalizedPath]
        };

        return handle;
    }

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
        // タイムアウトテスト用の遅延をシミュレート
        if (_installFromInfSectionDelay > TimeSpan.Zero)
        {
            Thread.Sleep(_installFromInfSectionDelay);
        }

        if (_lastError != 0)
        {
            var error = _lastError;
            _lastError = 0;
            return false;
        }

        if (!_openFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        var infFile = _openFiles[infHandle];
        if (!infFile.Sections.ContainsKey(sectionName))
        {
            _lastError = 0xE0000101; // SPAPI_E_SECTION_NOT_FOUND
            return false;
        }

        // スタブなので実際の処理は行わないが、成功を返す
        return true;
    }

    public bool SetupInstallServicesFromInfSection(IntPtr infHandle, string sectionName, uint flags)
    {
        // タイムアウトテスト用の遅延をシミュレート
        if (_installServicesFromInfSectionDelay > TimeSpan.Zero)
        {
            Thread.Sleep(_installServicesFromInfSectionDelay);
        }

        if (_lastError != 0)
        {
            var error = _lastError;
            _lastError = 0;
            return false;
        }

        if (!_openFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        var infFile = _openFiles[infHandle];
        if (!infFile.Sections.ContainsKey(sectionName))
        {
            _lastError = 0xE0000101; // SPAPI_E_SECTION_NOT_FOUND
            return false;
        }

        // スタブなので実際の処理は行わないが、成功を返す
        return true;
    }

    public void SetupCloseInfFile(IntPtr infHandle)
    {
        _openFiles.Remove(infHandle);
    }

    public bool SetupFindFirstLine(IntPtr infHandle, string section, string? key, out SetupApi.INFCONTEXT context)
    {
        context = new SetupApi.INFCONTEXT();

        if (!_openFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        var infFile = _openFiles[infHandle];
        if (!infFile.Sections.ContainsKey(section))
        {
            _lastError = 0xE0000101; // SPAPI_E_SECTION_NOT_FOUND
            return false;
        }

        var sectionLines = infFile.Sections[section];
        if (sectionLines.Count == 0)
        {
            return false;
        }

        // スタブなので実際のコンテキストは設定しないが、存在することを示す
        context.Inf = infHandle;
        context.Section = 1;
        context.Line = 1;

        return true;
    }

    public uint GetLastError()
    {
        var error = _lastError;
        return error;
    }

    public uint FormatMessage(
        uint dwFlags,
        IntPtr lpSource,
        uint dwMessageId,
        uint dwLanguageId,
        StringBuilder lpBuffer,
        uint nSize,
        IntPtr arguments)
    {
        // スタブなので簡単なメッセージを返す
        var message = dwMessageId switch
        {
            2 => "The system cannot find the file specified.",
            3 => "The system cannot find the path specified.",
            5 => "Access is denied.",
            6 => "The handle is invalid.",
            87 => "The parameter is incorrect.",
            0xE0000100 => "The INF file is invalid.",
            0xE0000101 => "The specified section was not found in the INF file.",
            0xE0000102 => "The specified line was not found in the INF file.",
            0xE0000103 => "The specified key was not found in the INF file.",
            0xE0000104 => "Registry write operation failed.",
            0xE0000108 => "File copy operation failed.",
            _ => $"Unknown error (Error code: 0x{dwMessageId:X8})"
        };

        if (lpBuffer.Capacity >= message.Length)
        {
            lpBuffer.Clear();
            lpBuffer.Append(message);
            return (uint)message.Length;
        }

        return 0;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// スタブで管理するINFファイル情報
    /// </summary>
    private class InfFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public Dictionary<string, List<string>> Sections { get; set; } = new();
    }
}