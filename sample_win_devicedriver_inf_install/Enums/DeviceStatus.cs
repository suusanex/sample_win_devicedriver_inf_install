namespace sample_win_devicedriver_inf_install.Enums;

/// <summary>
/// デバイス状態を表す列挙型
/// </summary>
public enum DeviceStatus
{
    /// <summary>不明</summary>
    Unknown,
    
    /// <summary>正常動作中</summary>
    Working,
    
    /// <summary>問題あり</summary>
    Problem,
    
    /// <summary>問題のあるデバイス</summary>
    ProblemDevice,
    
    /// <summary>無効</summary>
    Disabled,
    
    /// <summary>未インストール</summary>
    NotInstalled,
    
    /// <summary>存在しない</summary>
    NotPresent,
    
    /// <summary>インストール中</summary>
    Installing
}