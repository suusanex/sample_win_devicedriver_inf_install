namespace sample_win_devicedriver_inf_install.Enums;

/// <summary>
/// ログレベルを表す列挙型
/// </summary>
public enum LogLevel
{
    /// <summary>トレース</summary>
    Trace,
    
    /// <summary>デバッグ</summary>
    Debug,
    
    /// <summary>情報</summary>
    Information,
    
    /// <summary>警告</summary>
    Warning,
    
    /// <summary>エラー</summary>
    Error,
    
    /// <summary>重大</summary>
    Critical
}