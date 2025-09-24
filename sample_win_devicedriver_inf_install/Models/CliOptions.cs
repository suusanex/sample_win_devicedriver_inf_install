namespace sample_win_devicedriver_inf_install.Models;

/// <summary>
/// コマンドライン引数オプションを表現するクラス
/// </summary>
public class CliOptions
{
    /// <summary>
    /// インストールコマンドが指定されているかどうか
    /// </summary>
    public bool Install { get; set; }

    /// <summary>
    /// INFファイルのパス
    /// </summary>
    public string? InfPath { get; set; }

    /// <summary>
    /// インストールセクション名（デフォルト: DefaultInstall）
    /// </summary>
    public string Section { get; set; } = "DefaultInstall";

    /// <summary>
    /// 詳細モードが有効かどうか
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// 出力形式（json など）
    /// </summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// ドライランモード（実際のインストールを行わない）
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// ヘルプ表示
    /// </summary>
    public bool Help { get; set; }

    /// <summary>
    /// オプションの妥当性をチェックする
    /// </summary>
    /// <returns>妥当性チェック結果とエラーメッセージ</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (Help)
        {
            return (true, null);
        }

        if (!Install)
        {
            return (false, "コマンドが指定されていません。--install または --help を指定してください。");
        }

        if (string.IsNullOrEmpty(InfPath))
        {
            return (false, "--inf オプションでINFファイルのパスを指定してください。");
        }

        if (!File.Exists(InfPath))
        {
            return (false, $"指定されたINFファイルが見つかりません: {InfPath}");
        }

        if (!string.IsNullOrEmpty(OutputFormat) && OutputFormat.ToLowerInvariant() != "json")
        {
            return (false, $"無効な出力形式: {OutputFormat}");
        }

        return (true, null);
    }
}