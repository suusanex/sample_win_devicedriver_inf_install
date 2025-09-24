namespace sample_win_devicedriver_inf_install.Models.ValueObjects;

/// <summary>
/// Windows API エラー情報値オブジェクト（宣言的インストール専用）
/// </summary>
/// <param name="ErrorCode">Windows エラーコード</param>
/// <param name="SystemMessage">システムエラーメッセージ（英語）</param>
/// <param name="TechnicalDetails">ログ用技術詳細</param>
/// <param name="ApiFunction">エラーを引き起こしたAPI関数名</param>
/// <param name="Timestamp">エラー発生タイムスタンプ</param>
/// <param name="LocalizedMessage">ローカライズされたユーザー向けメッセージ</param>
public readonly record struct ApiErrorInfo(
    uint ErrorCode,
    string SystemMessage,
    string TechnicalDetails,
    string ApiFunction,
    DateTime Timestamp,
    string? LocalizedMessage = null
)
{
    /// <summary>
    /// ユーザー向けメッセージ（CLI用）
    /// </summary>
    public string UserMessage => LocalizedMessage ?? SystemMessage;

    /// <summary>
    /// Windows API エラー情報を作成します
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <param name="systemMessage">システムエラーメッセージ（英語）</param>
    /// <param name="technicalDetails">技術詳細</param>
    /// <param name="apiFunction">API関数名</param>
    /// <param name="localizedMessage">ローカライズされたメッセージ</param>
    /// <returns>ApiErrorInfo インスタンス</returns>
    public static ApiErrorInfo Create(uint errorCode, string systemMessage, string technicalDetails, string apiFunction, string? localizedMessage = null)
        => new(errorCode, systemMessage, technicalDetails, apiFunction, DateTime.UtcNow, localizedMessage);

    /// <summary>
    /// 不明なエラー情報を作成します
    /// </summary>
    /// <param name="apiFunction">API関数名</param>
    /// <param name="localizedMessage">ローカライズされたメッセージ</param>
    /// <returns>不明エラーの ApiErrorInfo</returns>
    public static ApiErrorInfo CreateUnknownError(string apiFunction, string? localizedMessage = null)
        => new(0, "Unknown error occurred", "Unknown error occurred", apiFunction, DateTime.UtcNow, localizedMessage);

    /// <summary>
    /// 技術的ログ用のフォーマット済みログメッセージを取得します
    /// </summary>
    /// <returns>フォーマット済みログメッセージ</returns>
    public string GetLogMessage()
        => $"API Error in {ApiFunction}: Code=0x{ErrorCode:X8}, Message={SystemMessage}, Details={TechnicalDetails}";
}