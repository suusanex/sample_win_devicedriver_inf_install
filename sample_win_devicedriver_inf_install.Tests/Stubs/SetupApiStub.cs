using System;
using System.Collections.Generic;
using System.Text;
using sample_win_devicedriver_inf_install.Contracts;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Native;
using System.Runtime.InteropServices;

namespace sample_win_devicedriver_inf_install.Tests.Stubs;

/// <summary>
/// SetupAPI のスタブ実装
/// UnitTest および IntegrationTest で使用される
/// 実際の OS 環境を変更せずにテストを実行するため
/// </summary>
public class SetupApiStub : ISetupApiWrapper
{
    private uint _lastError;
    private readonly Dictionary<IntPtr, string> _openedInfFiles;
    private readonly Dictionary<IntPtr, bool> _fileQueues;
    private readonly Dictionary<string, Dictionary<string, List<string>>> _infContents;
    private IntPtr _nextHandle;
    private TimeSpan _installFromInfSectionDelay;
    private TimeSpan _installServicesFromInfSectionDelay;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SetupApiStub()
    {
        _lastError = 0;
        _openedInfFiles = new Dictionary<IntPtr, string>();
        _fileQueues = new Dictionary<IntPtr, bool>();
        _infContents = new Dictionary<string, Dictionary<string, List<string>>>();
        _nextHandle = new IntPtr(1000);
        _installFromInfSectionDelay = TimeSpan.Zero;
        _installServicesFromInfSectionDelay = TimeSpan.Zero;
    }

    /// <summary>
    /// スタブの状態をリセットします（テスト用）
    /// </summary>
    public void Reset()
    {
        _lastError = 0;
        _openedInfFiles.Clear();
        _fileQueues.Clear();
        _infContents.Clear();
        _nextHandle = new IntPtr(1000);
        _installFromInfSectionDelay = TimeSpan.Zero;
        _installServicesFromInfSectionDelay = TimeSpan.Zero;
    }

    /// <summary>
    /// 最後のエラーコードを設定します（テスト用）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    public void SetLastError(uint errorCode)
    {
        _lastError = errorCode;
    }

    /// <summary>
    /// エラーを設定します（テスト用）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    public void SetupError(uint errorCode)
    {
        _lastError = errorCode;
    }

    /// <summary>
    /// INF コンテンツを設定します（テスト用）
    /// </summary>
    /// <param name="infPath">INFファイルパス</param>
    /// <param name="sections">セクションとその内容</param>
    public void SetupInfContent(string infPath, Dictionary<string, List<string>> sections)
    {
        _infContents[infPath] = sections;
    }

    /// <summary>
    /// SetupInstallFromInfSection の遅延時間を設定します（テスト用）
    /// </summary>
    /// <param name="delay">遅延時間</param>
    public void SetInstallFromInfSectionDelay(TimeSpan delay)
    {
        _installFromInfSectionDelay = delay;
    }

    /// <summary>
    /// SetupInstallServicesFromInfSection の遅延時間を設定します（テスト用）
    /// </summary>
    /// <param name="delay">遅延時間</param>
    public void SetInstallServicesFromInfSectionDelay(TimeSpan delay)
    {
        _installServicesFromInfSectionDelay = delay;
    }

    /// <summary>
    /// INFファイルを開きます（スタブ実装）
    /// </summary>
    public IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine)
    {
        errorLine = 0;

        // ファイル存在チェック（簡易）
        if (string.IsNullOrEmpty(fileName) || fileName.Contains("nonexistent"))
        {
            _lastError = 2; // ERROR_FILE_NOT_FOUND
            return SetupApi.INVALID_HANDLE_VALUE;
        }

        // 有効なハンドルを生成
        var handle = _nextHandle;
        _nextHandle = new IntPtr(_nextHandle.ToInt64() + 1);
        _openedInfFiles[handle] = fileName;
        _lastError = 0;
        return handle;
    }

    /// <summary>
    /// INFセクションからインストールを実行します（スタブ実装）
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
        // 遅延シミュレーション
        if (_installFromInfSectionDelay > TimeSpan.Zero)
        {
            System.Threading.Thread.Sleep(_installFromInfSectionDelay);
        }

        // 無効なハンドルチェック
        if (infHandle == SetupApi.INVALID_HANDLE_VALUE || !_openedInfFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        // セクション名チェック
        if (string.IsNullOrEmpty(sectionName) || sectionName.Contains("InvalidSection"))
        {
            _lastError = 1168; // ERROR_NOT_FOUND (セクションが見つからない)
            return false;
        }

        _lastError = 0;
        return true;
    }

    /// <summary>
    /// Services セクションからサービスをインストールします（スタブ実装）
    /// </summary>
    public bool SetupInstallServicesFromInfSection(IntPtr infHandle, string sectionName, uint flags)
    {
        // 遅延シミュレーション
        if (_installServicesFromInfSectionDelay > TimeSpan.Zero)
        {
            System.Threading.Thread.Sleep(_installServicesFromInfSectionDelay);
        }

        // 無効なハンドルチェック
        if (infHandle == SetupApi.INVALID_HANDLE_VALUE || !_openedInfFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        // セクション名チェック
        if (string.IsNullOrEmpty(sectionName) || sectionName.Contains("InvalidSection"))
        {
            _lastError = 1168; // ERROR_NOT_FOUND
            return false;
        }

        _lastError = 0;
        return true;
    }

    /// <summary>
    /// INFファイルハンドルを閉じます（スタブ実装）
    /// </summary>
    public void SetupCloseInfFile(IntPtr infHandle)
    {
        if (_openedInfFiles.ContainsKey(infHandle))
        {
            _openedInfFiles.Remove(infHandle);
        }
    }

    /// <summary>
    /// INFファイル内で指定されたセクションの最初の行を検索します（スタブ実装）
    /// </summary>
    public bool SetupFindFirstLine(IntPtr infHandle, string section, string? key, out SetupApi.INFCONTEXT context)
    {
        context = new SetupApi.INFCONTEXT();

        // 無効なハンドルチェック
        if (infHandle == SetupApi.INVALID_HANDLE_VALUE || !_openedInfFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        // セクション名チェック
        if (string.IsNullOrEmpty(section))
        {
            _lastError = 87; // ERROR_INVALID_PARAMETER
            return false;
        }

        // INFコンテンツから実際にセクションが存在するかチェック
        if (_openedInfFiles.TryGetValue(infHandle, out string? infPath) && 
            _infContents.TryGetValue(infPath, out var sections))
        {
            if (sections.ContainsKey(section))
            {
                _lastError = 0;
                return true;
            }
            else
            {
                _lastError = 1168; // ERROR_NOT_FOUND
                return false;
            }
        }

        // フォールバック: デフォルトの動作
        // "DefaultInstall.Services" セクションは存在するものとして扱う
        if (section == "DefaultInstall.Services" || section == "TestInstall.Services")
        {
            _lastError = 0;
            return true;
        }

        // 通常のセクション（DefaultInstall 等）も存在するものとして扱う
        if (section == "DefaultInstall" || section == "TestInstall")
        {
            _lastError = 0;
            return true;
        }

        // 存在しないセクション
        if (section.Contains("InvalidSection"))
        {
            _lastError = 1168; // ERROR_NOT_FOUND
            return false;
        }

        // デフォルトでは存在するものとして扱う
        _lastError = 0;
        return true;
    }

    #region File Queue Operations for CopyFiles Support

    /// <summary>
    /// ファイルキューを開きます（スタブ実装）
    /// </summary>
    public IntPtr SetupOpenFileQueue()
    {
        var queueHandle = _nextHandle;
        _nextHandle = new IntPtr(_nextHandle.ToInt64() + 1);
        _fileQueues[queueHandle] = true;
        _lastError = 0;
        return queueHandle;
    }

    /// <summary>
    /// INF セクションからファイル操作をキューに追加します（スタブ実装）
    /// </summary>
    public bool SetupInstallFilesFromInfSection(
        IntPtr infHandle,
        IntPtr layoutInfHandle,
        IntPtr fileQueue,
        string sectionName,
        string? sourceRootPath,
        uint copyStyle)
    {
        // 無効なハンドルチェック
        if (infHandle == SetupApi.INVALID_HANDLE_VALUE || !_openedInfFiles.ContainsKey(infHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        // 無効なファイルキューチェック
        if (fileQueue == IntPtr.Zero || !_fileQueues.ContainsKey(fileQueue))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        // セクション名チェック
        if (string.IsNullOrEmpty(sectionName) || sectionName.Contains("InvalidSection"))
        {
            _lastError = 1168; // ERROR_NOT_FOUND
            return false;
        }

        _lastError = 0;
        return true;
    }

    /// <summary>
    /// ファイルキューをコミット（実際にファイル操作を実行）します（スタブ実装）
    /// </summary>
    public bool SetupCommitFileQueue(
        IntPtr owner,
        IntPtr queueHandle,
        IntPtr msgHandler,
        IntPtr context)
    {
        // 無効なファイルキューチェック
        if (queueHandle == IntPtr.Zero || !_fileQueues.ContainsKey(queueHandle))
        {
            _lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        _lastError = 0;
        return true;
    }

    /// <summary>
    /// ファイルキューをサイレントコールバックでコミットします（スタブ実装）
    /// </summary>
    public bool SetupCommitFileQueueWithSilentCallback(
        IntPtr owner,
        IntPtr queueHandle,
        sample_win_devicedriver_inf_install.Services.SilentFileQueueCallback silentCallback)
    {
        // スタブでは実際のファイル操作は行わないが、コールバックのテストは可能
        if (silentCallback == null)
            return false;

        try
        {
            var callback = silentCallback.GetCallback();
            
            // モックのファイル操作通知を送信（NEEDMEDIA通知は送信しない）
            callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_STARTQUEUE, IntPtr.Zero, IntPtr.Zero);
            callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_STARTSUBQUEUE, IntPtr.Zero, IntPtr.Zero);
            
            // テスト用のモックファイル操作（実際のファイルコピー通知）
            var mockFilePaths = new SetupApi.FILEPATHS
            {
                Source = "C:\\test\\source.sys",
                Target = "C:\\Windows\\System32\\drivers\\test.sys",
                Win32Error = 0,
                Flags = 0
            };
            
            IntPtr mockPtr = IntPtr.Zero;
            try
            {
                mockPtr = Marshal.AllocHGlobal(Marshal.SizeOf<SetupApi.FILEPATHS>());
                Marshal.StructureToPtr(mockFilePaths, mockPtr, false);
                
                callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_STARTCOPY, mockPtr, IntPtr.Zero);
                callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_ENDCOPY, mockPtr, IntPtr.Zero);
            }
            finally
            {
                if (mockPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(mockPtr);
            }
            
            callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_ENDSUBQUEUE, IntPtr.Zero, IntPtr.Zero);
            callback(IntPtr.Zero, SetupApi.SPFILENOTIFY_ENDQUEUE, IntPtr.Zero, IntPtr.Zero);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ファイルキューを閉じます（スタブ実装）
    /// </summary>
    public bool SetupCloseFileQueue(IntPtr queueHandle)
    {
        if (_fileQueues.ContainsKey(queueHandle))
        {
            _fileQueues.Remove(queueHandle);
            _lastError = 0;
            return true;
        }

        _lastError = 6; // ERROR_INVALID_HANDLE
        return false;
    }

    #endregion

    /// <summary>
    /// 最後のエラーコードを取得します（スタブ実装）
    /// </summary>
    public uint GetLastError()
    {
        return _lastError;
    }

    /// <summary>
    /// エラーコードをメッセージに変換します（スタブ実装）
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
        // 簡易的なエラーメッセージマッピング（拡張版）
        string message = dwMessageId switch
        {
            0 => "The operation completed successfully.",
            2 => "The system cannot find the file specified.",
            5 => "Access is denied.",
            6 => "The handle is invalid.",
            87 => "The parameter is incorrect.",
            1168 => "Element not found.",
            0xE0000101u => "The required section was not found in the INF file.",
            0xE0000104u => "Registry operation failed.",
            _ => $"Unknown error (Error code: 0x{dwMessageId:X8})"  // WindowsApiErrorHandlerと同じ形式
        };

        if (lpBuffer.Capacity >= message.Length + 1)
        {
            lpBuffer.Clear();
            lpBuffer.Append(message);
            return (uint)message.Length;
        }

        return 0;
    }
}