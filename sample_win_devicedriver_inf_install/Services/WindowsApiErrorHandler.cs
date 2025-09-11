using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// Windows API エラーハンドラー（宣言的インストール専用）
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

        // 宣言的インストール用のSetupAPIエラーコード説明を追加
        var setupApiExplanation = GetDeclarativeInstallErrorExplanation(errorCode);
        if (!string.IsNullOrEmpty(setupApiExplanation))
        {
            details += $", SetupAPI Context: {setupApiExplanation}";
        }
        
        return details;
    }

    /// <summary>
    /// 宣言的インストールに関連する SetupAPI エラーコードの技術的説明を取得します（英語）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <returns>技術的説明、または既知のエラーでない場合は null</returns>
    private string? GetDeclarativeInstallErrorExplanation(uint errorCode)
    {
        return errorCode switch
        {
            // ファイルシステムエラー
            2 => "The INF file was not found.",
            3 => "The INF file path is invalid.",
            5 => "Access denied. Administrator privileges required for installation.",
            87 => "Invalid parameter passed to SetupAPI function.",
            
            // INF 固有エラー
            0xE0000100 => "INF file format is invalid or corrupted.",
            0xE0000101 => "Required section not found in INF file.",
            0xE0000102 => "INF file line format is invalid.",
            0xE0000103 => "Required key not found in INF section.",
            0xE0000104 => "Registry operation failed during installation.",
            0xE0000108 => "File copy operation failed during installation.",
            
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
        
        return details;
    }
}