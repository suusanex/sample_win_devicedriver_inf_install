using sample_win_devicedriver_inf_install.Core.Models.ValueObjects;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install.UI.Services;

/// <summary>
/// エラーメッセージプロバイダー（UI層 - FR-014準拠日本語メッセージ）
/// コア層のApiErrorInfoから日本語ユーザーメッセージを生成します
/// </summary>
public class ErrorMessageProvider
{
    private readonly LocalizationService _localizationService;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="localizationService">ローカライゼーションサービス</param>
    /// <exception cref="ArgumentNullException">localizationService が null の場合</exception>
    public ErrorMessageProvider(LocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    /// <summary>
    /// ApiErrorInfo から日本語エラーメッセージを生成します
    /// </summary>
    /// <param name="errorInfo">コア層のエラー情報</param>
    /// <returns>日本語エラーメッセージ</returns>
    public string GetLocalizedErrorMessage(ApiErrorInfo errorInfo)
    {
        var localizedMessage = GetJapaneseErrorMessage(errorInfo.ErrorCode, errorInfo.ApiFunction);
        return $"{localizedMessage} (エラーコード: 0x{errorInfo.ErrorCode:X8})";
    }

    /// <summary>
    /// 操作結果用の成功メッセージを取得します
    /// </summary>
    /// <param name="operationName">操作名</param>
    /// <returns>成功メッセージ</returns>
    public string GetSuccessMessage(string operationName)
    {
        return operationName switch
        {
            "InstallDriver" => "ドライバのインストールが正常に完了しました。",
            "VerifyInstallation" => "インストールの検証が正常に完了しました。",
            "LoadINF" => "INFファイルが正常に読み込まれました。",
            _ => $"{operationName} が正常に完了しました。"
        };
    }

    /// <summary>
    /// エラーコードから日本語エラーメッセージを取得します
    /// </summary>
    /// <param name="errorCode">エラーコード</param>
    /// <param name="operationName">操作名</param>
    /// <returns>日本語エラーメッセージ</returns>
    private string GetJapaneseErrorMessage(uint errorCode, string operationName)
    {
        // まずリソースファイルからの取得を試行
        var resourceMessage = _localizationService.GetErrorMessage(errorCode.ToString());
        if (resourceMessage != null)
        {
            return resourceMessage;
        }

        // 一般的なWindowsエラーコードの日本語メッセージを提供
        var localizedMessage = errorCode switch
        {
            // ファイルシステムエラー
            2 => "指定されたファイルが見つかりません。",
            3 => "指定されたパスが見つかりません。",
            5 => "アクセスが拒否されました。管理者権限で実行してください。",
            32 => "ファイルが他のプロセスで使用されています。",
            87 => "パラメータが正しくありません。",
            
            // Setup API 固有のエラー
            0xE0000100 => "INF ファイルが見つからないか、形式が正しくありません。",
            0xE0000101 => "INF ファイルで指定されたセクションが見つかりません。",
            0xE0000102 => "INF ファイルで指定された行が見つかりません。",
            0xE0000103 => "INF ファイルで指定されたキーが見つかりません。",
            0xE0000104 => "レジストリへの書き込みに失敗しました。",
            0xE0000105 => "ドライバがデジタル署名されていません。",
            0xE0000106 => "デバイスの検出に失敗しました。",
            0xE0000107 => "ドライバのインストールに失敗しました。",
            0xE0000108 => "ドライバファイルのコピーに失敗しました。",
            0xE0000109 => "デバイス設定の更新に失敗しました。",
            0xE000010A => "適合するドライバが見つかりません。",
            
            // デバイスインストールエラー
            0xE000020E => "デバイスマネージャーでデバイスが無効になっています。",
            0xE000020F => "デバイスに問題が発生しています。",
            0xE0000210 => "デバイスドライバが正しくインストールされていません。",
            0xE0000211 => "デバイスが正常に動作していません。",
            0xE0000212 => "リソースの競合が発生しています。",
            0xE0000213 => "デバイスの初期化に失敗しました。",
            
            // セキュリティと権限エラー
            0x80070005 => "アクセスが拒否されました。管理者権限で実行してください。",
            0x800B0100 => "証明書の検証に失敗しました。",
            0x800B0101 => "証明書が信頼できません。",
            0x800B0109 => "証明書チェーンの処理中にエラーが発生しました。",
            
            // メモリとリソースエラー
            8 => "メモリが不足しています。",
            14 => "メモリが不足しています。",
            1450 => "システムリソースが不足しています。",
            
            // ネットワークと接続エラー
            50 => "ネットワークリクエストがサポートされていません。",
            53 => "ネットワークパスが見つかりません。",
            64 => "指定されたネットワーク名は利用できません。",
            
            _ => null
        };
        
        // フォールバック処理
        return localizedMessage ?? GetOperationSpecificMessage(operationName);
    }

    /// <summary>
    /// 操作固有のフォールバックメッセージを取得します
    /// </summary>
    /// <param name="operationName">操作名</param>
    /// <returns>操作固有のエラーメッセージ</returns>
    private string GetOperationSpecificMessage(string operationName)
    {
        return operationName switch
        {
            "InstallDriver" => "ドライバのインストール中にエラーが発生しました。",
            "LoadINF" => "INFファイルの読み込み中にエラーが発生しました。",
            "VerifyInstallation" => "インストールの検証中にエラーが発生しました。",
            "CopyFiles" => "ファイルのコピー中にエラーが発生しました。",
            "RegisterDevice" => "デバイスの登録中にエラーが発生しました。",
            _ => "操作の実行中にエラーが発生しました。"
        };
    }

    /// <summary>
    /// 詳細なトラブルシューティング情報を日本語で提供します
    /// </summary>
    /// <param name="errorInfo">エラー情報</param>
    /// <returns>日本語のトラブルシューティングガイド</returns>
    public string GetTroubleshootingGuidance(ApiErrorInfo errorInfo)
    {
        return errorInfo.ErrorCode switch
        {
            5 or 0x80070005 => 
                "このエラーを解決するには：\n" +
                "1. 管理者としてコマンドプロンプトを実行してください\n" +
                "2. ユーザーアカウント制御(UAC)が有効になっている場合は、管理者権限の確認ダイアログで「はい」を選択してください",
            
            0xE0000105 => 
                "このエラーを解決するには：\n" +
                "1. ドライバが正しくデジタル署名されていることを確認してください\n" +
                "2. テスト署名モードを有効にしてください (bcdedit /set testsigning on)\n" +
                "3. システムを再起動してください",
            
            0xE0000100 => 
                "このエラーを解決するには：\n" +
                "1. INFファイルのパスが正しいことを確認してください\n" +
                "2. INFファイルの構文エラーをチェックしてください\n" +
                "3. INFファイルがWindows標準形式に準拠していることを確認してください",
            
            _ => "詳細な技術情報についてはログファイルを確認してください。"
        };
    }
}