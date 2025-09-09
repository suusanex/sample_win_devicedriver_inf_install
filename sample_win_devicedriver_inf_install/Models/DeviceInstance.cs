using sample_win_devicedriver_inf_install.Enums;

namespace sample_win_devicedriver_inf_install.Models;

/// <summary>
/// デバイスインスタンス情報を表すモデル
/// </summary>
public class DeviceInstance
{
    /// <summary>
    /// デバイスインスタンスID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// デバイス名
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// ハードウェアID
    /// </summary>
    public string? HardwareId { get; set; }

    /// <summary>
    /// 互換ID一覧
    /// </summary>
    public List<string> CompatibleIds { get; set; } = new();

    /// <summary>
    /// デバイス状態
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    /// <summary>
    /// デバイスクラス
    /// </summary>
    public string? DeviceClass { get; set; }

    /// <summary>
    /// デバイスクラスGUID
    /// </summary>
    public string? DeviceClassGuid { get; set; }

    /// <summary>
    /// ドライバファイルパス
    /// </summary>
    public string? DriverPath { get; set; }

    /// <summary>
    /// ドライババージョン
    /// </summary>
    public string? DriverVersion { get; set; }

    /// <summary>
    /// ドライバ日付
    /// </summary>
    public DateTime? DriverDate { get; set; }

    /// <summary>
    /// 製造者
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// 問題コード（デバイスに問題がある場合）
    /// </summary>
    public uint? ProblemCode { get; set; }

    /// <summary>
    /// 問題の説明
    /// </summary>
    public string? ProblemDescription { get; set; }

    /// <summary>
    /// 最終更新日時
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// デバイスが正常に動作しているかを判定します
    /// </summary>
    public bool IsWorking => Status == DeviceStatus.Working && ProblemCode == null;

    /// <summary>
    /// デバイスに問題があるかを判定します
    /// </summary>
    public bool HasProblem => Status == DeviceStatus.Problem || ProblemCode.HasValue;

    /// <summary>
    /// 指定されたハードウェアIDに一致するかを判定します
    /// </summary>
    /// <param name="hardwareId">チェックするハードウェアID</param>
    /// <returns>一致する場合はtrue</returns>
    public bool MatchesHardwareId(string hardwareId)
    {
        if (string.IsNullOrEmpty(hardwareId))
            return false;

        return string.Equals(HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase) ||
               CompatibleIds.Any(id => string.Equals(id, hardwareId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// デバイス情報を更新します
    /// </summary>
    /// <param name="status">新しいステータス</param>
    /// <param name="problemCode">問題コード</param>
    /// <param name="problemDescription">問題の説明</param>
    public void UpdateStatus(DeviceStatus status, uint? problemCode = null, string? problemDescription = null)
    {
        Status = status;
        ProblemCode = problemCode;
        ProblemDescription = problemDescription;
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// 新しいデバイスインスタンスを作成します
    /// </summary>
    /// <param name="instanceId">インスタンスID</param>
    /// <param name="deviceName">デバイス名</param>
    /// <param name="hardwareId">ハードウェアID</param>
    /// <returns>新しいDeviceInstanceインスタンス</returns>
    public static DeviceInstance Create(string instanceId, string? deviceName = null, string? hardwareId = null)
    {
        return new DeviceInstance
        {
            InstanceId = instanceId,
            DeviceName = deviceName,
            HardwareId = hardwareId
        };
    }
}