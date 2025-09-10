namespace sample_win_devicedriver_inf_install.Enums;

/// <summary>
/// インストール状態を表す列挙型
/// </summary>
public enum InstallationStatus
{
    /// <summary>待機中</summary>
    Pending,
    
    /// <summary>実行中</summary>
    InProgress,
    
    /// <summary>インストール中</summary>
    Installing,
    
    /// <summary>アンインストール中</summary>
    Uninstalling,
    
    /// <summary>成功</summary>
    Completed,
    
    /// <summary>失敗</summary>
    Failed,
    
    /// <summary>キャンセル済み</summary>
    Cancelled,
    
    /// <summary>タイムアウト</summary>
    TimedOut
}