using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// Windows API エラーハンドラー（宣言的インストール専用）
/// </summary>
public class WindowsApiErrorHandler
{
    private readonly LocalizationService _localization;
    private readonly ISetupApiWrapper _setupApiWrapper;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="localization">ローカライゼーションサービス</param>
    /// <param name="setupApiWrapper">SetupAPI ラッパー</param>
    public WindowsApiErrorHandler(LocalizationService localization, ISetupApiWrapper setupApiWrapper)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _setupApiWrapper = setupApiWrapper ?? throw new ArgumentNullException(nameof(setupApiWrapper));
    }

    /// <summary>
    /// 最後の Windows API エラーを取得し、ApiErrorInfo オブジェクトを作成します
    /// </summary>
    /// <param name="operationName">失敗した操作の名前</param>
    /// <param name="additionalContext">追加のコンテキスト情報</param>
    /// <returns>技術詳細を含むエラー情報</returns>
    public ApiErrorInfo GetLastError(string operationName, string? additionalContext = null)
    {
        uint errorCode = _setupApiWrapper.GetLastError();
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
        var localizedMessage = GetLocalizedErrorMessage(errorCode, operationName);
        var technicalDetails = BuildTechnicalDetails(operationName, systemMessage, additionalContext, errorCode);
        
        return ApiErrorInfo.Create(
            errorCode: errorCode,
            systemMessage: systemMessage,
            technicalDetails: technicalDetails,
            apiFunction: operationName,
            localizedMessage: localizedMessage
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
        
        uint result = _setupApiWrapper.FormatMessage(
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
    /// ローカライズされたエラーメッセージを取得します（日本語）
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <param name="operationName">操作名</param>
    /// <returns>日本語のエラーメッセージ</returns>
    private string GetLocalizedErrorMessage(uint errorCode, string operationName)
    {
        // 主要なエラーコードの日本語メッセージ
        var messageKey = errorCode switch
        {
            2 => "Error_FileNotFound",
            3 => "Error_PathNotFound", 
            5 => "Error_AccessDenied",
            87 => "Error_InvalidParameter",
            0xE0000100 => "Error_InvalidInfFormat",
            0xE0000101 => "Error_SectionNotFound",
            0xE0000102 => "Error_InvalidInfLine",
            0xE0000103 => "Error_KeyNotFound",
            0xE0000104 => "Error_RegistryOperationFailed",
            0xE0000108 => "Error_FileCopyFailed",
            _ => null
        };

        try
        {
            if (!string.IsNullOrEmpty(messageKey))
            {
                // エラーメッセージリソースから取得
                var message = _localization.GetErrorMessage(messageKey!);
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }

            // 不明なエラーコードは汎用メッセージを使用（エラーコード付き）
            var unknown = _localization.GetFormattedErrorMessage("Error_UnknownSetupApiError", errorCode.ToString("X8"));
            return !string.IsNullOrEmpty(unknown)
                ? unknown
                : GetFallbackLocalizedMessage(errorCode, operationName);
        }
        catch
        {
            // ローカライゼーション失敗時のフォールバック
            return GetFallbackLocalizedMessage(errorCode, operationName);
        }
    }

    /// <summary>
    /// フォールバック用の日本語エラーメッセージを取得します
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <param name="operationName">操作名</param>
    /// <returns>フォールバック用の日本語メッセージ</returns>
    private string GetFallbackLocalizedMessage(uint errorCode, string operationName)
    {
        return errorCode switch
        {
            2 => "指定されたファイルが見つかりません。",
            3 => "指定されたパスが見つかりません。",
            5 => "アクセスが拒否されました。管理者権限で実行してください。",
            87 => "不正なパラメータが指定されました。",
            0xE0000100 => "INFファイルの形式が無効です。",
            0xE0000101 => "指定されたセクションがINFファイルに見つかりません。",
            0xE0000102 => "INFファイルの行形式が無効です。",
            0xE0000103 => "指定されたキーがINFファイルに見つかりません。",
            0xE0000104 => "レジストリ操作が失敗しました。",
            0xE0000108 => "ファイルのコピー操作が失敗しました。",
            _ => $"インストール中にエラーが発生しました (エラーコード: 0x{errorCode:X8})"
        };
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
        string localizedMessage;
        
        // Win32Exception からエラーコードを抽出
        if (exception is Win32Exception win32Exception)
        {
            errorCode = (uint)win32Exception.NativeErrorCode;
            systemMessage = GetSystemErrorMessage(errorCode);
            localizedMessage = GetLocalizedErrorMessage(errorCode, operationName);
        }
        // COMException からエラーコードを抽出
        else if (exception is COMException comException)
        {
            errorCode = (uint)comException.HResult;
            systemMessage = comException.Message;
            localizedMessage = GetLocalizedMessageFromException(comException, operationName);
        }
        // FileNotFoundException の場合
        else if (exception is FileNotFoundException)
        {
            errorCode = 2; // ERROR_FILE_NOT_FOUND
            systemMessage = exception.Message;
            localizedMessage = "指定されたINFファイルが見つかりません。ファイルパスを確認してください。";
        }
        // DirectoryNotFoundException の場合
        else if (exception is DirectoryNotFoundException)
        {
            errorCode = 3; // ERROR_PATH_NOT_FOUND
            systemMessage = exception.Message;
            localizedMessage = "指定されたパスが見つかりません。";
        }
        // InvalidOperationException の場合
        else if (exception is InvalidOperationException)
        {
            systemMessage = exception.Message;
            localizedMessage = GetLocalizedMessageFromInvalidOperation(exception.Message);
        }
        // その他の例外
        else
        {
            systemMessage = exception.Message;
            localizedMessage = "インストール中に予期しないエラーが発生しました。";
        }
        
        var technicalDetails = BuildExceptionTechnicalDetails(operationName, exception, errorCode);
        
        return ApiErrorInfo.Create(
            errorCode: errorCode,
            systemMessage: systemMessage,
            technicalDetails: technicalDetails,
            apiFunction: operationName,
            localizedMessage: localizedMessage
        );
    }

    /// <summary>
    /// COMExceptionから日本語メッセージを取得します
    /// </summary>
    /// <param name="comException">COMException</param>
    /// <param name="operationName">操作名</param>
    /// <returns>日本語メッセージ</returns>
    private string GetLocalizedMessageFromException(COMException comException, string operationName)
    {
        var hResult = (uint)comException.HResult;
        return hResult switch
        {
            0x80070005 => "アクセスが拒否されました。管理者権限で実行してください。",
            0x80070002 => "指定されたファイルが見つかりません。",
            0x80070003 => "指定されたパスが見つかりません。",
            _ => "インストール中にシステムエラーが発生しました。"
        };
    }

    /// <summary>
    /// InvalidOperationExceptionのメッセージから日本語メッセージを取得します
    /// </summary>
    /// <param name="exceptionMessage">例外メッセージ</param>
    /// <returns>日本語メッセージ</returns>
    private string GetLocalizedMessageFromInvalidOperation(string exceptionMessage)
    {
        // INFファイル関連
        if (exceptionMessage.Contains("INF file", StringComparison.OrdinalIgnoreCase))
        {
            if (exceptionMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return "指定されたINFファイルが見つかりません。";
            if (exceptionMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase) || 
                exceptionMessage.Contains("Version", StringComparison.OrdinalIgnoreCase))
                return "INFファイルの形式が無効です。[Version]セクションを確認してください。";
            if (exceptionMessage.Contains("section", StringComparison.OrdinalIgnoreCase))
                return "指定されたセクションがINFファイルに見つかりません。";
        }

        // セクション未検出（INF section/Section not found等の一般表現も拾う）
        if (exceptionMessage.Contains("section not found", StringComparison.OrdinalIgnoreCase) ||
            exceptionMessage.Contains("INF section", StringComparison.OrdinalIgnoreCase) ||
            (exceptionMessage.Contains("section", StringComparison.OrdinalIgnoreCase) && exceptionMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)))
        {
            return "指定されたセクションがINFファイルに見つかりません。";
        }
        
        if (exceptionMessage.Contains("registry", StringComparison.OrdinalIgnoreCase) ||
            exceptionMessage.Contains("レジストリ", StringComparison.OrdinalIgnoreCase))
            return "レジストリ操作が失敗しました。管理者権限で実行してください。";
        
        if (exceptionMessage.Contains("access", StringComparison.OrdinalIgnoreCase) ||
            exceptionMessage.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return "アクセスが拒否されました。管理者権限で実行してください。";
        
        return "インストール処理中にエラーが発生しました。";
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
        
        if (exception.InnerException != null)
        {
            details += $", Inner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
        }
        
        return details;
    }
}