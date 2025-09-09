namespace sample_win_devicedriver_inf_install.Core.Models.ValueObjects;

/// <summary>
/// Windows API エラー情報値オブジェクト（コア層 - FR-014準拠）
/// FR-014に従い、ローカライズされたメッセージを含まない技術的エラーデータのみを格納します
/// </summary>
/// <param name="ErrorCode">Windows エラーコード</param>
/// <param name="SystemMessage">システムエラーメッセージ（英語）</param>
/// <param name="TechnicalDetails">ログ用技術詳細</param>
/// <param name="ApiFunction">エラーを引き起こしたAPI関数名</param>
/// <param name="Timestamp">エラー発生タイムスタンプ</param>
public readonly record struct ApiErrorInfo(
    uint ErrorCode,
    string SystemMessage,
    string TechnicalDetails,
    string ApiFunction,
    DateTime Timestamp
)
{
    /// <summary>
    /// Windows API エラー情報を作成します
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <param name="systemMessage">システムエラーメッセージ（英語）</param>
    /// <param name="technicalDetails">技術詳細</param>
    /// <param name="apiFunction">API関数名</param>
    /// <returns>ApiErrorInfo インスタンス</returns>
    public static ApiErrorInfo Create(uint errorCode, string systemMessage, string technicalDetails, string apiFunction)
        => new(errorCode, systemMessage, technicalDetails, apiFunction, DateTime.UtcNow);

    /// <summary>
    /// 不明なエラー情報を作成します
    /// </summary>
    /// <param name="apiFunction">API関数名</param>
    /// <returns>不明エラーの ApiErrorInfo</returns>
    public static ApiErrorInfo CreateUnknownError(string apiFunction)
        => new(0, "Unknown error occurred", "Unknown error occurred", apiFunction, DateTime.UtcNow);

    /// <summary>
    /// 技術的ログ用のフォーマット済みログメッセージを取得します（FR-014により英語のみ）
    /// </summary>
    /// <returns>フォーマット済みログメッセージ</returns>
    public string GetLogMessage()
        => $"API Error in {ApiFunction}: Code=0x{ErrorCode:X8}, Message={SystemMessage}, Details={TechnicalDetails}";
}