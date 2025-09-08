using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WindowsDriverInstaller.Contracts
{
    /// <summary>
    /// インストールログサービスのコントラクト
    /// </summary>
    public interface IInstallationLogger
    {
        /// <summary>
        /// ログエントリを記録します
        /// </summary>
        /// <param name="level">ログレベル</param>
        /// <param name="message">英語ログメッセージ</param>
        /// <param name="context">コンテキスト情報</param>
        /// <param name="correlationId">相関識別子</param>
        /// <param name="exception">例外情報（オプション）</param>
        void Log(LogLevel level, string message, string context = null, Guid? correlationId = null, Exception exception = null);

        /// <summary>
        /// 情報ログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="context">コンテキスト</param>
        /// <param name="correlationId">相関識別子</param>
        void LogInformation(string message, string context = null, Guid? correlationId = null);

        /// <summary>
        /// 警告ログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="context">コンテキスト</param>
        /// <param name="correlationId">相関識別子</param>
        void LogWarning(string message, string context = null, Guid? correlationId = null);

        /// <summary>
        /// エラーログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">例外情報</param>
        /// <param name="context">コンテキスト</param>
        /// <param name="correlationId">相関識別子</param>
        void LogError(string message, Exception exception = null, string context = null, Guid? correlationId = null);

        /// <summary>
        /// デバッグログを記録します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="context">コンテキスト</param>
        /// <param name="correlationId">相関識別子</param>
        void LogDebug(string message, string context = null, Guid? correlationId = null);

        /// <summary>
        /// Windows API 呼び出し結果をログに記録します
        /// </summary>
        /// <param name="apiName">API 名</param>
        /// <param name="success">成功フラグ</param>
        /// <param name="errorCode">エラーコード</param>
        /// <param name="parameters">API パラメータ</param>
        /// <param name="correlationId">相関識別子</param>
        void LogWindowsApiCall(string apiName, bool success, int? errorCode = null, 
            Dictionary<string, object> parameters = null, Guid? correlationId = null);

        /// <summary>
        /// インストール進捗をログに記録します
        /// </summary>
        /// <param name="sessionId">セッション識別子</param>
        /// <param name="progress">進捗率</param>
        /// <param name="currentStep">現在のステップ</param>
        /// <param name="details">詳細情報</param>
        void LogInstallationProgress(Guid sessionId, int progress, string currentStep, string details = null);

        /// <summary>
        /// セッションのログエントリを取得します
        /// </summary>
        /// <param name="correlationId">相関識別子（セッション ID）</param>
        /// <returns>ログエントリリスト</returns>
        Task<IEnumerable<LogEntry>> GetLogEntriesAsync(Guid correlationId);

        /// <summary>
        /// 期間内のログエントリを取得します
        /// </summary>
        /// <param name="startTime">開始時刻</param>
        /// <param name="endTime">終了時刻</param>
        /// <param name="level">最小ログレベル</param>
        /// <returns>ログエントリリスト</returns>
        Task<IEnumerable<LogEntry>> GetLogEntriesByTimeRangeAsync(DateTime startTime, DateTime endTime, LogLevel level = LogLevel.Debug);

        /// <summary>
        /// ログファイルパスを取得します
        /// </summary>
        /// <param name="correlationId">相関識別子</param>
        /// <returns>ログファイルパス</returns>
        string GetLogFilePath(Guid? correlationId = null);

        /// <summary>
        /// 構造化ログをエクスポートします
        /// </summary>
        /// <param name="correlationId">相関識別子</param>
        /// <param name="format">出力形式（JSON, XML, CSV）</param>
        /// <returns>エクスポートされたデータ</returns>
        Task<string> ExportLogsAsync(Guid correlationId, LogExportFormat format = LogExportFormat.Json);

        /// <summary>
        /// ログレベルを動的に変更します
        /// </summary>
        /// <param name="level">新しいログレベル</param>
        void SetLogLevel(LogLevel level);

        /// <summary>
        /// ログ設定を取得します
        /// </summary>
        /// <returns>ログ設定情報</returns>
        LogConfiguration GetLogConfiguration();

        /// <summary>
        /// ログファイルをローテーションします
        /// </summary>
        /// <returns>ローテーション実行結果</returns>
        Task<LogRotationResult> RotateLogFilesAsync();
    }

    /// <summary>
    /// ログエントリ
    /// </summary>
    public class LogEntry
    {
        /// <summary>タイムスタンプ</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>ログレベル</summary>
        public LogLevel Level { get; set; }

        /// <summary>英語ログメッセージ</summary>
        public string Message { get; set; }

        /// <summary>コンテキスト</summary>
        public string Context { get; set; }

        /// <summary>相関識別子</summary>
        public Guid? CorrelationId { get; set; }

        /// <summary>例外情報</summary>
        public string ExceptionDetails { get; set; }

        /// <summary>Windows エラーコード</summary>
        public int? WindowsErrorCode { get; set; }

        /// <summary>プロセス ID</summary>
        public int ProcessId { get; set; }

        /// <summary>スレッド ID</summary>
        public int ThreadId { get; set; }

        /// <summary>追加プロパティ</summary>
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// ログ設定
    /// </summary>
    public class LogConfiguration
    {
        /// <summary>現在のログレベル</summary>
        public LogLevel CurrentLevel { get; set; }

        /// <summary>ログファイルパス</summary>
        public string LogFilePath { get; set; }

        /// <summary>最大ファイルサイズ（MB）</summary>
        public int MaxFileSizeMb { get; set; }

        /// <summary>保持するファイル数</summary>
        public int RetainedFileCount { get; set; }

        /// <summary>コンソール出力有効フラグ</summary>
        public bool ConsoleOutputEnabled { get; set; }

        /// <summary>ファイル出力有効フラグ</summary>
        public bool FileOutputEnabled { get; set; }

        /// <summary>構造化ログ有効フラグ</summary>
        public bool StructuredLoggingEnabled { get; set; }
    }

    /// <summary>
    /// ログローテーション結果
    /// </summary>
    public class LogRotationResult
    {
        /// <summary>ローテーション成功フラグ</summary>
        public bool Success { get; set; }

        /// <summary>ローテーションされたファイル数</summary>
        public int RotatedFileCount { get; set; }

        /// <summary>削除されたファイル数</summary>
        public int DeletedFileCount { get; set; }

        /// <summary>エラーメッセージ</summary>
        public string ErrorMessage { get; set; }

        /// <summary>ローテーション実行時間</summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// ログエクスポート形式
    /// </summary>
    public enum LogExportFormat
    {
        /// <summary>JSON 形式</summary>
        Json,
        /// <summary>XML 形式</summary>
        Xml,
        /// <summary>CSV 形式</summary>
        Csv,
        /// <summary>プレーンテキスト</summary>
        Text
    }
}