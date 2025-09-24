using System.ComponentModel.DataAnnotations;

namespace sample_win_devicedriver_inf_install.Models;

/// <summary>
/// ドライバパッケージ情報モデル（宣言的インストール専用）
/// </summary>
public class DriverPackage
{
    /// <summary>
    /// ドライバパッケージID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// INFファイルパス
    /// </summary>
    [Required]
    public required string InfPath { get; set; }

    /// <summary>
    /// ドライバ名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// バージョン
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 提供者
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 作成タイムスタンプ
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// INFファイルが存在するかを検証します
    /// </summary>
    /// <returns>存在する場合は true</returns>
    public bool ValidateInfFileExists()
    {
        return !string.IsNullOrEmpty(InfPath) && File.Exists(InfPath);
    }

    /// <summary>
    /// ドライバパッケージの基本検証を実行します
    /// </summary>
    /// <returns>検証結果</returns>
    public ValidationResult Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        Validator.TryValidateObject(this, context, results, true);

        if (!ValidateInfFileExists())
        {
            results.Add(new ValidationResult("INF file does not exist", [nameof(InfPath)]));
        }

        return results.Count == 0 
            ? ValidationResult.Success! 
            : new ValidationResult(string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    /// <summary>
    /// ログ用の技術的説明を取得します
    /// </summary>
    /// <returns>技術的説明</returns>
    public string GetTechnicalDescription()
    {
        return $"Package ID: {Id}, INF: {InfPath}, Provider: {Provider}, Version: {Version}";
    }
}