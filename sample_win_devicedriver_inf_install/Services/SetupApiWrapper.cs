using System;
using System.Text;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Native;
using Microsoft.Extensions.Logging;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// SetupAPIの実際の実装
/// 本番環境で使用される
/// </summary>
public class SetupApiWrapper : ISetupApiWrapper
{
    private readonly ILogger<SetupApiWrapper>? _logger;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー（オプション）</param>
    public SetupApiWrapper(ILogger<SetupApiWrapper>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// INFファイルを開きます
    /// </summary>
    public IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine)
    {
        _logger?.LogDebug("SetupOpenInfFile - INPUT: fileName={FileName}, infClass={InfClass}, infStyle=0x{InfStyle:X}", 
            fileName, infClass ?? "null", infStyle);

        var result = SetupApi.SetupOpenInfFile(fileName, infClass, infStyle, out errorLine);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupOpenInfFile - OUTPUT: handle={Handle}, errorLine={ErrorLine}, lastError={LastError}", 
            result, errorLine, lastError);

        if (result == SetupApi.INVALID_HANDLE_VALUE)
        {
            _logger?.LogWarning("SetupOpenInfFile failed: fileName={FileName}, errorLine={ErrorLine}, lastError={LastError}",
                fileName, errorLine, lastError);
        }

        return result;
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
        _logger?.LogDebug("SetupInstallFromInfSection - INPUT: owner={Owner}, infHandle={InfHandle}, sectionName={SectionName}, flags=0x{Flags:X}, relativeKeyRoot={RelativeKeyRoot}, sourceRootPath={SourceRootPath}, copyFlags=0x{CopyFlags:X}, msgHandler={MsgHandler}, context={Context}, deviceInfoSet={DeviceInfoSet}, deviceInfoData={DeviceInfoData}",
            owner, infHandle, sectionName, flags, relativeKeyRoot, sourceRootPath ?? "null", copyFlags, msgHandler, context, deviceInfoSet, deviceInfoData);

        var result = SetupApi.SetupInstallFromInfSection(
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

        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupInstallFromInfSection - OUTPUT: result={Result}, lastError={LastError}",
            result, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupInstallFromInfSection failed: sectionName={SectionName}, flags=0x{Flags:X}, lastError={LastError}",
                sectionName, flags, lastError);
        }

        return result;
    }

    /// <summary>
    /// Services セクションからサービスをインストールします
    /// </summary>
    public bool SetupInstallServicesFromInfSection(IntPtr infHandle, string sectionName, uint flags)
    {
        _logger?.LogDebug("SetupInstallServicesFromInfSection - INPUT: infHandle={InfHandle}, sectionName={SectionName}, flags=0x{Flags:X}",
            infHandle, sectionName, flags);

        var result = SetupApi.SetupInstallServicesFromInfSection(infHandle, sectionName, flags);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupInstallServicesFromInfSection - OUTPUT: result={Result}, lastError={LastError}",
            result, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupInstallServicesFromInfSection failed: sectionName={SectionName}, lastError={LastError}",
                sectionName, lastError);
        }

        return result;
    }

    /// <summary>
    /// INFファイルハンドルを閉じます
    /// </summary>
    public void SetupCloseInfFile(IntPtr infHandle)
    {
        _logger?.LogDebug("SetupCloseInfFile - INPUT: infHandle={InfHandle}", infHandle);

        SetupApi.SetupCloseInfFile(infHandle);

        _logger?.LogDebug("SetupCloseInfFile - OUTPUT: completed");
    }

    /// <summary>
    /// INFファイル内で指定されたセクションの最初の行を検索します
    /// </summary>
    public bool SetupFindFirstLine(IntPtr infHandle, string section, string? key, out SetupApi.INFCONTEXT context)
    {
        _logger?.LogDebug("SetupFindFirstLine - INPUT: infHandle={InfHandle}, section={Section}, key={Key}",
            infHandle, section, key ?? "null");

        var result = SetupApi.SetupFindFirstLine(infHandle, section, key, out context);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupFindFirstLine - OUTPUT: result={Result}, context.Inf={ContextInf}, context.Section={ContextSection}, context.Line={ContextLine}, lastError={LastError}",
            result, context.Inf, context.Section, context.Line, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupFindFirstLine failed: section={Section}, key={Key}, lastError={LastError}",
                section, key ?? "null", lastError);
        }

        return result;
    }

    #region File Queue Operations for CopyFiles Support

    /// <summary>
    /// ファイルキューを開きます
    /// </summary>
    public IntPtr SetupOpenFileQueue()
    {
        _logger?.LogDebug("SetupOpenFileQueue - INPUT: (no parameters)");

        var result = SetupApi.SetupOpenFileQueue();
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupOpenFileQueue - OUTPUT: queueHandle={QueueHandle}, lastError={LastError}",
            result, lastError);

        if (result == IntPtr.Zero)
        {
            _logger?.LogWarning("SetupOpenFileQueue failed: lastError={LastError}", lastError);
        }

        return result;
    }

    /// <summary>
    /// INF セクションからファイル操作をキューに追加します
    /// </summary>
    public bool SetupInstallFilesFromInfSection(
        IntPtr infHandle,
        IntPtr layoutInfHandle,
        IntPtr fileQueue,
        string sectionName,
        string? sourceRootPath,
        uint copyStyle)
    {
        _logger?.LogDebug("SetupInstallFilesFromInfSection - INPUT: infHandle={InfHandle}, layoutInfHandle={LayoutInfHandle}, fileQueue={FileQueue}, sectionName={SectionName}, sourceRootPath={SourceRootPath}, copyStyle=0x{CopyStyle:X}",
            infHandle, layoutInfHandle, fileQueue, sectionName, sourceRootPath ?? "null", copyStyle);

        var result = SetupApi.SetupInstallFilesFromInfSection(
            infHandle,
            layoutInfHandle,
            fileQueue,
            sectionName,
            sourceRootPath,
            copyStyle);

        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupInstallFilesFromInfSection - OUTPUT: result={Result}, lastError={LastError}",
            result, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupInstallFilesFromInfSection failed: sectionName={SectionName}, lastError={LastError}",
                sectionName, lastError);
        }

        return result;
    }

    /// <summary>
    /// ファイルキューをサイレントコールバックでコミットします
    /// FR-012（サイレントモード）準拠の専用実装
    /// </summary>
    /// <param name="owner">オーナーハンドル</param>
    /// <param name="queueHandle">キューハンドル</param>
    /// <param name="silentCallback">サイレント用コールバック</param>
    /// <returns>成功時true</returns>
    public bool SetupCommitFileQueueWithSilentCallback(
        IntPtr owner,
        IntPtr queueHandle,
        SilentFileQueueCallback silentCallback)
    {
        if (silentCallback == null)
            throw new ArgumentNullException(nameof(silentCallback));

        _logger?.LogDebug("SetupCommitFileQueueWithSilentCallback - INPUT: owner={Owner}, queueHandle={QueueHandle}, silentCallback={SilentCallback}",
            owner, queueHandle, silentCallback.GetType().Name);

        try
        {
            _logger?.LogDebug("Committing file queue with silent callback");
            _logger?.LogDebug("Owner: {Owner}, QueueHandle: {QueueHandle}", owner, queueHandle);
            
            var callback = silentCallback.GetCallback();
            _logger?.LogDebug("Retrieved callback delegate: {CallbackDelegate}", callback?.Method?.Name ?? "null");
            
            if (callback == null)
            {
                _logger?.LogError("Silent callback delegate is null");
                return false;
            }
            
            _logger?.LogDebug("Calling SetupCommitFileQueue with callback");
            var result = SetupApi.SetupCommitFileQueue(owner, queueHandle, callback, IntPtr.Zero);
            var lastError = SetupApi.GetLastError();
            
            _logger?.LogDebug("SetupCommitFileQueueWithSilentCallback - OUTPUT: result={Result}, lastError={LastError}",
                result, lastError);
            
            if (!result)
            {
                _logger?.LogError("SetupCommitFileQueue failed with error: {ErrorCode}", lastError);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            var lastError = SetupApi.GetLastError();
            _logger?.LogError(ex, "SetupCommitFileQueueWithSilentCallback - EXCEPTION: lastError={LastError}", lastError);
            throw;
        }
    }

    /// <summary>
    /// ファイルキューをコミット（実際にファイル操作を実行）します
    /// サイレントモード対応: 独自コールバックまたは既定コールバックを使用
    /// </summary>
    public bool SetupCommitFileQueue(
        IntPtr owner,
        IntPtr queueHandle,
        IntPtr msgHandler,
        IntPtr context)
    {
        _logger?.LogDebug("SetupCommitFileQueue - INPUT: owner={Owner}, queueHandle={QueueHandle}, msgHandler={MsgHandler}, context={Context}",
            owner, queueHandle, msgHandler, context);

        // msgHandler が指定されていなければ、既定のコールバックでコミットする
        if (msgHandler == IntPtr.Zero)
        {
            _logger?.LogDebug("Using default queue callback (msgHandler is zero)");

            IntPtr defaultCtx = IntPtr.Zero;
            SetupApi.PSP_FILE_CALLBACK callback = SetupApi.SetupDefaultQueueCallback;
            try
            {
                _logger?.LogDebug("Initializing default queue callback context");
                defaultCtx = SetupApi.SetupInitDefaultQueueCallback(owner);
                var initLastError = SetupApi.GetLastError();

                _logger?.LogDebug("SetupInitDefaultQueueCallback - OUTPUT: defaultCtx={DefaultCtx}, lastError={InitLastError}",
                    defaultCtx, initLastError);

                if (defaultCtx == IntPtr.Zero)
                {
                    _logger?.LogWarning("SetupInitDefaultQueueCallback failed: lastError={InitLastError}", initLastError);
                    return false;
                }

                _logger?.LogDebug("Calling SetupCommitFileQueue with default callback");
                var ok = SetupApi.SetupCommitFileQueue(owner, queueHandle, callback, defaultCtx);
                var commitLastError = SetupApi.GetLastError();

                _logger?.LogDebug("SetupCommitFileQueue (with default callback) - OUTPUT: result={Result}, lastError={CommitLastError}",
                    ok, commitLastError);

                if (!ok)
                {
                    _logger?.LogWarning("SetupCommitFileQueue with default callback failed: lastError={CommitLastError}", commitLastError);
                }

                return ok;
            }
            finally
            {
                if (defaultCtx != IntPtr.Zero)
                {
                    _logger?.LogDebug("Terminating default queue callback context");
                    SetupApi.SetupTermDefaultQueueCallback(defaultCtx);
                }
            }
        }

        // 既定以外が指定されている場合は、そのまま呼び出す
        _logger?.LogDebug("Using custom message handler");
        var result = SetupApi.SetupCommitFileQueue(owner, queueHandle, msgHandler, context);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupCommitFileQueue (custom handler) - OUTPUT: result={Result}, lastError={LastError}",
            result, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupCommitFileQueue with custom handler failed: lastError={LastError}", lastError);
        }

        return result;
    }

    /// <summary>
    /// ファイルキューを閉じます
    /// </summary>
    public bool SetupCloseFileQueue(IntPtr queueHandle)
    {
        _logger?.LogDebug("SetupCloseFileQueue - INPUT: queueHandle={QueueHandle}", queueHandle);

        var result = SetupApi.SetupCloseFileQueue(queueHandle);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("SetupCloseFileQueue - OUTPUT: result={Result}, lastError={LastError}",
            result, lastError);

        if (!result)
        {
            _logger?.LogWarning("SetupCloseFileQueue failed: queueHandle={QueueHandle}, lastError={LastError}",
                queueHandle, lastError);
        }

        return result;
    }

    #endregion

    /// <summary>
    /// 最後のエラーコードを取得します
    /// </summary>
    public uint GetLastError()
    {
        var result = SetupApi.GetLastError();
        _logger?.LogTrace("GetLastError - OUTPUT: {ErrorCode}", result);
        return result;
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
        _logger?.LogDebug("FormatMessage - INPUT: dwFlags=0x{DwFlags:X}, lpSource={LpSource}, dwMessageId={DwMessageId}, dwLanguageId={DwLanguageId}, nSize={NSize}, arguments={Arguments}",
            dwFlags, lpSource, dwMessageId, dwLanguageId, nSize, arguments);

        var result = SetupApi.FormatMessage(dwFlags, lpSource, dwMessageId, dwLanguageId, lpBuffer, nSize, arguments);
        var lastError = SetupApi.GetLastError();

        _logger?.LogDebug("FormatMessage - OUTPUT: result={Result}, message={Message}, lastError={LastError}",
            result, result > 0 ? lpBuffer.ToString() : "null", lastError);

        return result;
    }
}