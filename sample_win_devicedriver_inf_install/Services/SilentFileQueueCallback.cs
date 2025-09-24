using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Native;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// サイレント実行用のファイルキューコールバック実装
/// GUI表示を行わず、エラー時は適切にログ記録してスキップまたは中断を選択
/// </summary>
public class SilentFileQueueCallback
{
    private readonly ILogger _logger;
    private readonly IInstallationLogger _installationLogger;
    private readonly string _correlationId;
    private readonly string _sourceRootPath;
    private readonly SetupApi.PSP_FILE_CALLBACK _callbackDelegate;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="installationLogger">インストールロガー</param>
    /// <param name="correlationId">相関ID</param>
    /// <param name="sourceRootPath">ソースルートパス（NEEDMEDIA対応用）</param>
    public SilentFileQueueCallback(ILogger logger, IInstallationLogger installationLogger, string correlationId, string? sourceRootPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _installationLogger = installationLogger ?? throw new ArgumentNullException(nameof(installationLogger));
        _correlationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        _sourceRootPath = sourceRootPath ?? string.Empty;
        
        // デバッグ: コールバック初期化の確認
        _logger.LogInformation("SilentFileQueueCallback initialized for correlation ID: {CorrelationId}, SourceRoot: {SourceRoot}", 
            correlationId, sourceRootPath ?? "null");
        
        // デリゲートインスタンスを保持（GC回収を防ぐため）
        _callbackDelegate = CallbackHandler;
        
        _logger.LogDebug("Silent callback delegate created: {DelegateTarget}", _callbackDelegate.Method.Name);
    }

    /// <summary>
    /// コールバックデリゲートを取得
    /// </summary>
    public SetupApi.PSP_FILE_CALLBACK GetCallback() 
    {
        _logger.LogDebug("GetCallback() called, returning delegate: {DelegateTarget}", _callbackDelegate.Method.Name);
        return _callbackDelegate;
    }

    /// <summary>
    /// サイレント用ファイルキューコールバックハンドラ
    /// </summary>
    /// <param name="context">コンテキスト（未使用）</param>
    /// <param name="notification">通知タイプ</param>
    /// <param name="param1">パラメータ1</param>
    /// <param name="param2">パラメータ2</param>
    /// <returns>処理結果</returns>
    private uint CallbackHandler(IntPtr context, uint notification, IntPtr param1, IntPtr param2)
    {
        // デバッグ: コールバック呼び出しの確認
        _logger.LogInformation("Silent callback invoked: Notification=0x{Notification:X}, Context={Context}, Param1={Param1}, Param2={Param2}",
            notification, context, param1, param2);

        try
        {
            // 非同期ログ記録（コールバック内の制約に対応）
            try
            {
                _installationLogger.LogInformationAsync(
                    $"Silent callback invoked: Notification=0x{notification:X}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log callback invocation");
            }

            return notification switch
            {
                SetupApi.SPFILENOTIFY_STARTQUEUE => HandleStartQueue(),
                SetupApi.SPFILENOTIFY_ENDQUEUE => HandleEndQueue(),
                SetupApi.SPFILENOTIFY_STARTSUBQUEUE => HandleStartSubQueue(),
                SetupApi.SPFILENOTIFY_ENDSUBQUEUE => HandleEndSubQueue(),
                SetupApi.SPFILENOTIFY_STARTCOPY => HandleStartCopy(param1),
                SetupApi.SPFILENOTIFY_ENDCOPY => HandleEndCopy(param1),
                SetupApi.SPFILENOTIFY_COPYERROR => HandleCopyError(param1),
                SetupApi.SPFILENOTIFY_NEEDMEDIA => HandleNeedMedia(param1, param2),
                SetupApi.SPFILENOTIFY_STARTDELETE => HandleStartDelete(param1),
                SetupApi.SPFILENOTIFY_ENDDELETE => HandleEndDelete(param1),
                SetupApi.SPFILENOTIFY_DELETEERROR => HandleDeleteError(param1),
                SetupApi.SPFILENOTIFY_STARTRENAME => HandleStartRename(param1),
                SetupApi.SPFILENOTIFY_ENDRENAME => HandleEndRename(param1),
                SetupApi.SPFILENOTIFY_RENAMEERROR => HandleRenameError(param1),
                _ => HandleUnknownNotification(notification)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in silent file queue callback for notification {Notification}", notification);
            
            // 非同期ログ記録を同期的に実行（コールバック内のため）
            try
            {
                _installationLogger.LogErrorAsync(
                    $"Silent callback error for notification {notification}: {ex.Message}",
                    "FileOperation",
                    _correlationId,
                    ex).Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // ログ記録エラーは無視して継続
            }
            
            return SetupApi.FILEOP_ABORT;
        }
    }

    /// <summary>
    /// キュー開始処理
    /// </summary>
    private uint HandleStartQueue()
    {
        _logger.LogDebug("File queue processing started (silent mode)");
        
        try
        {
            _installationLogger.LogInformationAsync(
                "File queue processing started (silent mode)",
                "FileOperation",
                _correlationId).Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ログエラーは無視
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// キュー完了処理
    /// </summary>
    private uint HandleEndQueue()
    {
        _logger.LogDebug("File queue processing completed (silent mode)");
        
        try
        {
            _installationLogger.LogInformationAsync(
                "File queue processing completed (silent mode)",
                "FileOperation",
                _correlationId).Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ログエラーは無視
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// サブキュー開始処理
    /// </summary>
    private uint HandleStartSubQueue()
    {
        _logger.LogDebug("File subqueue processing started (silent mode)");
        
        try
        {
            _installationLogger.LogInformationAsync(
                "File subqueue processing started (silent mode)",
                "FileOperation",
                _correlationId).Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ログエラーは無視
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// サブキュー完了処理
    /// </summary>
    private uint HandleEndSubQueue()
    {
        _logger.LogDebug("File subqueue processing completed (silent mode)");
        
        try
        {
            _installationLogger.LogInformationAsync(
                "File subqueue processing completed (silent mode)",
                "FileOperation",
                _correlationId).Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ログエラーは無視
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイルコピー開始処理
    /// </summary>
    private uint HandleStartCopy(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Starting copy: {Source} -> {Target}", filePaths.Source, filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Copying file: {filePaths.Source} -> {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log copy start information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイルコピー完了処理
    /// </summary>
    private uint HandleEndCopy(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Completed copy: {Source} -> {Target}", filePaths.Source, filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Successfully copied: {filePaths.Source} -> {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log copy completion information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイルコピーエラー処理
    /// </summary>
    private uint HandleCopyError(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogError("Copy error: {Source} -> {Target}, Error: {Error}",
                    filePaths.Source, filePaths.Target, filePaths.Win32Error);
                
                _installationLogger.LogErrorAsync(
                    $"Failed to copy file: {filePaths.Source} -> {filePaths.Target} (Error: {filePaths.Win32Error})",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log copy error information");
        }

        // サイレントモードでは重要なファイルエラーは中断する
        return SetupApi.FILEOP_ABORT;
    }

    /// <summary>
    /// メディア要求処理 - サイレントモードでNEWPATHINFO構造体に新しいパスを設定して再試行
    /// </summary>
    private uint HandleNeedMedia(IntPtr param1, IntPtr param2)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                // SOURCE_MEDIA_W 構造体から詳細情報を取得
                var sourceMedia = Marshal.PtrToStructure<SetupApi.SOURCE_MEDIA_W>(param1);
                
                _logger.LogInformation("NEEDMEDIA notification: Description={Description}, SourceFile={SourceFile}, Tagfile={Tagfile}", 
                    sourceMedia.Description ?? "null", sourceMedia.SourceFile ?? "null", sourceMedia.Tagfile ?? "null");
                
                // ソースルートパスが設定されていて、param2がNEWPATHINFO構造体のポインタの場合
                if (!string.IsNullOrEmpty(_sourceRootPath) && Directory.Exists(_sourceRootPath) && param2 != IntPtr.Zero)
                {
                    try
                    {
                        // param2にNEWPATHINFO構造体として新しいパスを設定
                        var newPathInfo = new SetupApi.NEWPATHINFO
                        {
                            NewPath = _sourceRootPath
                        };
                        
                        Marshal.StructureToPtr(newPathInfo, param2, false);
                        
                        _logger.LogInformation("NEEDMEDIA: Set new path in NEWPATHINFO structure: {SourceRootPath}", _sourceRootPath);
                        
                        try
                        {
                            _installationLogger.LogInformationAsync(
                                $"NEEDMEDIA: Providing new source path via NEWPATHINFO: {_sourceRootPath}",
                                "FileOperation",
                                _correlationId).Wait(TimeSpan.FromSeconds(1));
                        }
                        catch
                        {
                            // ログエラーは無視
                        }

                        // 新しいパスを提供したので再試行を要求
                        return SetupApi.FILEOP_NEWPATH;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error setting NEWPATHINFO structure");
                        
                        // NEWPATHINFO設定に失敗した場合はフォールバック処理
                        _logger.LogWarning("Falling back to FILEOP_SKIP due to NEWPATHINFO error");
                        return SetupApi.FILEOP_SKIP;
                    }
                }
                else if (!string.IsNullOrEmpty(_sourceRootPath) && Directory.Exists(_sourceRootPath))
                {
                    // param2がnullの場合（古いSetupAPI？）はパス設定せずに単純に再試行
                    _logger.LogWarning("NEEDMEDIA: param2 is null, cannot set NEWPATHINFO. Attempting simple retry with sourceRootPath already configured.");
                    
                    try
                    {
                        _installationLogger.LogInformationAsync(
                            $"NEEDMEDIA: Source root path available but cannot set NEWPATHINFO (param2 is null): {_sourceRootPath}",
                            "FileOperation",
                            _correlationId).Wait(TimeSpan.FromSeconds(1));
                    }
                    catch
                    {
                        // ログエラーは無視
                    }
                    
                    // 既にソースパスが設定済みの前提で再試行
                    return SetupApi.FILEOP_RETRY;
                }
                else
                {
                    _logger.LogWarning("NEEDMEDIA: No valid source root path available or param2 is null: SourceRoot={SourceRootPath}, Param2={Param2}", 
                        _sourceRootPath ?? "null", param2);
                    
                    try
                    {
                        _installationLogger.LogWarningAsync(
                            $"NEEDMEDIA: Cannot resolve media requirement - source root path not available: {_sourceRootPath ?? "null"}",
                            "FileOperation",
                            _correlationId).Wait(TimeSpan.FromSeconds(1));
                    }
                    catch
                    {
                        // ログエラーは無視
                    }
                    
                    // ソースパスが利用できない場合はスキップして継続
                    return SetupApi.FILEOP_SKIP;
                }
            }
            else
            {
                _logger.LogWarning("NEEDMEDIA notification with null param1 - skipping in silent mode");
                
                try
                {
                    _installationLogger.LogErrorAsync(
                        "NEEDMEDIA: notification parameter is null - skipping file in silent mode",
                        "FileOperation",
                        _correlationId).Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // ログエラーは無視
                }
                
                return SetupApi.FILEOP_SKIP;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NEEDMEDIA notification");
            
            try
            {
                _installationLogger.LogErrorAsync(
                    $"Error handling NEEDMEDIA notification: {ex.Message}",
                    "FileOperation",
                    _correlationId,
                    ex).Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // ログエラーは無視
            }
            
            // エラー時はスキップして継続を試行
            return SetupApi.FILEOP_SKIP;
        }
    }

    /// <summary>
    /// ファイル削除開始処理
    /// </summary>
    private uint HandleStartDelete(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Starting delete: {Target}", filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Deleting file: {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log delete start information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイル削除完了処理
    /// </summary>
    private uint HandleEndDelete(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Completed delete: {Target}", filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Successfully deleted: {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log delete completion information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイル削除エラー処理
    /// </summary>
    private uint HandleDeleteError(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogError("Delete error: {Target}, Error: {Error}",
                    filePaths.Target, filePaths.Win32Error);
                
                _installationLogger.LogErrorAsync(
                    $"Failed to delete file: {filePaths.Target} (Error: {filePaths.Win32Error})",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log delete error information");
        }

        // 削除エラーは通常続行可能
        return SetupApi.FILEOP_SKIP;
    }

    /// <summary>
    /// ファイルリネーム開始処理
    /// </summary>
    private uint HandleStartRename(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Starting rename: {Source} -> {Target}", filePaths.Source, filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Renaming file: {filePaths.Source} -> {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log rename start information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイルリネーム完了処理
    /// </summary>
    private uint HandleEndRename(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogDebug("Completed rename: {Source} -> {Target}", filePaths.Source, filePaths.Target);
                
                _installationLogger.LogInformationAsync(
                    $"Successfully renamed: {filePaths.Source} -> {filePaths.Target}",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log rename completion information");
        }
        
        return SetupApi.FILEOP_DOIT;
    }

    /// <summary>
    /// ファイルリネームエラー処理
    /// </summary>
    private uint HandleRenameError(IntPtr param1)
    {
        try
        {
            if (param1 != IntPtr.Zero)
            {
                var filePaths = Marshal.PtrToStructure<SetupApi.FILEPATHS>(param1);
                _logger.LogError("Rename error: {Source} -> {Target}, Error: {Error}",
                    filePaths.Source, filePaths.Target, filePaths.Win32Error);
                
                _installationLogger.LogErrorAsync(
                    $"Failed to rename file: {filePaths.Source} -> {filePaths.Target} (Error: {filePaths.Win32Error})",
                    "FileOperation",
                    _correlationId).Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log rename error information");
        }

        // リネームエラーは通常続行可能
        return SetupApi.FILEOP_SKIP;
    }

    /// <summary>
    /// 未知の通知処理
    /// </summary>
    private uint HandleUnknownNotification(uint notification)
    {
        _logger.LogDebug("Unknown file queue notification: {Notification:X}", notification);
        
        try
        {
            _installationLogger.LogInformationAsync(
                $"Unknown file queue notification: 0x{notification:X}",
                "FileOperation",
                _correlationId).Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ログエラーは無視
        }
        
        // 未知の通知は続行
        return SetupApi.FILEOP_DOIT;
    }
}