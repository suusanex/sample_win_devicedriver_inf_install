using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using sample_win_devicedriver_inf_install.Core.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Core.Services;

/// <summary>
/// Windows API エラーハンドラー（コア層 - FR-014準拠）
/// ローカライズされたメッセージを含まない技術的エラー解析を提供します
/// </summary>
public class WindowsApiErrorHandler
{
    /// <summary>
    /// 最後の Windows API エラーを取得し、ApiErrorInfo オブジェクトを作成します
    /// </summary>
    /// <param name="operationName">失敗した操作の名前</param>
    /// <param name="additionalContext">追加のコンテキスト情報</param>
    /// <returns>技術詳細を含むエラー情報</returns>
    public ApiErrorInfo GetLastError(string operationName, string? additionalContext = null)
    {
        uint errorCode = SetupApi.GetLastError();
        return CreateApiErrorInfo(errorCode, operationName, additionalContext);
    }

    /// <summary>
    /// 指定されたエラーコードから ApiErrorInfo オブジェクトを作成します
    /// </summary>
    /// <param name="errorCode">Windows エラーコード</param>
    /// <param name="operationName">失敗した操作の名前</param>
    /// <param name="additionalContext">追加のコンテキスト情報</param>
    /// <returns>技術詳細を含むエラー情報</returns>
    public ApiErrorInfo CreateApiErrorInfo(uint errorCode, string operationName, string? additionalContext = null)
    {
        var systemMessage = GetSystemErrorMessage(errorCode);
        var technicalDetails = BuildTechnicalDetails(operationName, systemMessage, additionalContext, errorCode);
        
        return ApiErrorInfo.Create(
            errorCode: errorCode,
            systemMessage: systemMessage,
            technicalDetails: technicalDetails,
            apiFunction: operationName
        );
    }

    /// <summary>
    /// Windows からシステムエラーメッセージを取得します（英語）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <returns>英語のシステムエラーメッセージ</returns>
    private string GetSystemErrorMessage(uint errorCode)
    {
        const uint bufferSize = 1024;
        var buffer = new StringBuilder((int)bufferSize);
        
        uint result = SetupApi.FormatMessage(
            SetupApi.FORMAT_MESSAGE_FROM_SYSTEM | SetupApi.FORMAT_MESSAGE_IGNORE_INSERTS,
            IntPtr.Zero,
            errorCode,
            0, // Default language (English)
            buffer,
            bufferSize,
            IntPtr.Zero);
            
        if (result == 0)
        {
            return $"Unknown error (Error code: 0x{errorCode:X8})";
        }
        
        return buffer.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// ログ用の技術詳細文字列を構築します
    /// </summary>
    /// <param name="operationName">操作名</param>
    /// <param name="systemMessage">システムメッセージ</param>
    /// <param name="additionalContext">追加コンテキスト</param>
    /// <param name="errorCode">エラーコード</param>
    /// <returns>フォーマット済み技術詳細</returns>
    private string BuildTechnicalDetails(string operationName, string systemMessage, string? additionalContext, uint errorCode)
    {
        var details = $"Operation: {operationName}, Error Code: 0x{errorCode:X8}, System Message: {systemMessage}";
        
        if (!string.IsNullOrEmpty(additionalContext))
        {
            details += $", Additional Context: {additionalContext}";
        }

        // トラブルシューティング用の一般的な SetupAPI エラーコード説明を追加
        var setupApiExplanation = GetSetupApiErrorExplanation(errorCode);
        if (!string.IsNullOrEmpty(setupApiExplanation))
        {
            details += $", SetupAPI Context: {setupApiExplanation}";
        }
        
        return details;
    }

    /// <summary>
    /// 一般的な SetupAPI エラーコードの技術的説明を取得します（英語）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <returns>技術的説明、または既知の SetupAPI エラーでない場合は null</returns>
    private string? GetSetupApiErrorExplanation(uint errorCode)
    {
        return errorCode switch
        {
            // ファイルシステムエラー
            2 => "The system cannot find the file specified.",
            3 => "The system cannot find the path specified.",
            5 => "Access is denied. Administrator privileges may be required.",
            32 => "The process cannot access the file because it is being used by another process.",
            87 => "The parameter is incorrect.",
            
            // Setup API 固有エラー
            0xE0000100 => "INF file not found or invalid format.",
            0xE0000101 => "INF file section not found.",
            0xE0000102 => "INF file line not found.",
            0xE0000103 => "INF file key not found.",
            0xE0000104 => "Registry write operation failed.",
            0xE0000105 => "Driver is not digitally signed.",
            0xE0000106 => "Device detection failed.",
            0xE0000107 => "Driver installation failed.",
            0xE0000108 => "Driver file copy operation failed.",
            0xE0000109 => "Device configuration update failed.",
            0xE000010A => "No compatible driver found.",
            
            // デバイスインストールエラー
            0xE000020E => "Device is disabled in Device Manager.",
            0xE000020F => "Device has a problem.",
            0xE0000210 => "Device driver is not properly installed.",
            0xE0000211 => "Device is not functioning properly.",
            0xE0000212 => "Resource conflict occurred.",
            0xE0000213 => "Device initialization failed.",
            
            // セキュリティと権限エラー
            0x80070005 => "Access denied. Administrator privileges required.",
            0x800B0100 => "Certificate verification failed.",
            0x800B0101 => "Certificate is not trusted.",
            0x800B0109 => "Error occurred during certificate chain processing.",
            
            // メモリとリソースエラー
            8 => "Not enough storage is available to process this command.",
            14 => "Not enough storage is available to complete this operation.",
            1450 => "Insufficient system resources exist to complete the requested service.",
            
            _ => null
        };
    }

    /// <summary>
    /// 例外から ApiErrorInfo を作成します
    /// </summary>
    /// <param name="exception">例外オブジェクト</param>
    /// <param name="operationName">操作名</param>
    /// <returns>ApiErrorInfo オブジェクト</returns>
    public ApiErrorInfo CreateFromException(Exception exception, string operationName)
    {
        uint errorCode = 0;
        string systemMessage;
        
        // Win32Exception からエラーコードを抽出
        if (exception is Win32Exception win32Exception)
        {
            errorCode = (uint)win32Exception.NativeErrorCode;
            systemMessage = GetSystemErrorMessage(errorCode);
        }
        // COMException からエラーコードを抽出
        else if (exception is COMException comException)
        {
            errorCode = (uint)comException.HResult;
            systemMessage = comException.Message;
        }
        else
        {
            systemMessage = exception.Message;
        }
        
        var technicalDetails = BuildExceptionTechnicalDetails(operationName, exception, errorCode);
        
        return ApiErrorInfo.Create(
            errorCode: errorCode,
            systemMessage: systemMessage,
            technicalDetails: technicalDetails,
            apiFunction: operationName
        );
    }

    /// <summary>
    /// 例外から技術詳細を構築します
    /// </summary>
    /// <param name="operationName">操作名</param>
    /// <param name="exception">例外</param>
    /// <param name="errorCode">エラーコード</param>
    /// <returns>技術詳細</returns>
    private string BuildExceptionTechnicalDetails(string operationName, Exception exception, uint errorCode)
    {
        var details = $"Operation: {operationName}, Exception: {exception.GetType().Name}, Message: {exception.Message}";
        
        if (errorCode != 0)
        {
            details += $", Error Code: 0x{errorCode:X8}";
        }
        
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            details += $", StackTrace: {exception.StackTrace}";
        }
        
        return details;
    }

    /// <summary>
    /// HRESULT が成功を示すかどうかをチェックします
    /// </summary>
    /// <param name="hresult">HRESULT 値</param>
    /// <returns>成功した場合は true</returns>
    public static bool Succeeded(int hresult) => hresult >= 0;

    /// <summary>
    /// HRESULT が失敗を示すかどうかをチェックします
    /// </summary>
    /// <param name="hresult">HRESULT 値</param>
    /// <returns>失敗した場合は true</returns>
    public static bool Failed(int hresult) => hresult < 0;
}