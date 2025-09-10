# データモデル: Windows デバイスドライバ INF インストーラ（宣言的インストール専用）

## コアエンティティ

### DriverPackage
**目的**: ドライバインストールに必要なファイルの基本情報。事前検証は行わず、パス情報のみ管理。

**プロパティ**:
- `InfFilePath: string` - INF ファイルへの絶対パス
- `DriverDirectory: string` - ドライバファイルが配置されているディレクトリ
- `InstallationPath: string?` - 対象インストールディレクトリ（オプション）
- `Description: string?` - パッケージの説明（オプション）

**検証ルール**:
- InfFilePath は空でない文字列である必要がある
- DriverDirectory は空でない文字列である必要がある
- 実際のファイル存在チェックは実行しない（実行者の責務）

**注意**: 
- INF ファイルの内容検証、形式チェック、参照ファイル確認は行わない
- Windows API 実行時にエラーが発生した場合のみエラー処理を行う

### InstallationSession
**目的**: 単一の INF ベース宣言的インストール操作の状態と進捗を追跡。

**プロパティ**:
- `SessionId: Guid` - 一意のセッション識別子
- `StartTime: DateTime` - インストール開始タイムスタンプ
- `EndTime: DateTime?` - インストール完了タイムスタンプ
- `Status: InstallationStatus` - 現在の状態（保留中、進行中、完了、失敗）
- `Progress: int` - 進捗率（0-100）
- `CurrentStep: string` - 現在の操作説明
- `DriverPackage: DriverPackage` - インストール中のパッケージ
- `LogEntries: List<LogEntry>` - インストールログメッセージ
- `ApiError: ApiErrorInfo?` - Windows API エラー情報
- `RequiresReboot: bool` - システム再起動が必要かどうか
- `SectionName: string` - 適用対象セクション名（例: "DefaultInstall"）

**検証ルール**:
- SessionId は一意でなければならない
- 進捗開始前に StartTime が設定されている必要がある
- Progress は 0-100 の範囲でなければならない
- EndTime は状態が完了または失敗の場合のみ設定
- SectionName は空でない文字列でなければならない

**状態遷移**:
- `保留中` → `進行中` → `完了`
- `保留中` → `進行中` → `失敗`
- 逆方向の遷移は許可されない

**注意**: セクション適用とサービス登録の詳細な進捗は内部処理であり、ユーザーには統一されたインストール進捗として提供される

### InstallationResult
**目的**: INF ベース宣言的インストール操作の最終結果と詳細情報。

**プロパティ**:
- `Success: bool` - インストール成功フラグ
- `SessionId: Guid` - 関連するセッション識別子
- `ApiError: ApiErrorInfo?` - Windows API エラー情報（失敗時）
- `LocalizedMessage: string` - 日本語ユーザーメッセージ
- `TechnicalDetails: string` - 英語技術詳細（ログ用）
- `RequiresReboot: bool` - システム再起動が必要かどうか
- `Duration: TimeSpan` - インストール実行時間
- `LogEntries: List<LogEntry>` - 関連ログエントリ
- `InfSectionProcessed: bool` - INF セクション処理が完了したかどうか
- `ServicesProcessed: bool` - サービス関連処理が完了したかどうか（該当する場合）

**検証ルール**:
- Success が false の場合、ApiError または LocalizedMessage が設定されている必要がある
- SessionId は有効な Guid でなければならない
- Duration は非負の値でなければならない

**注意**: 個別の API 呼び出し結果ではなく、INF に従った包括的なインストール結果を表現

## 値オブジェクト

### ApiErrorInfo
**目的**: Windows API 実行時のエラー詳細情報。

**プロパティ**:
- `ApiName: string` - 失敗した API 名
- `ErrorCode: int` - Windows エラーコード（GetLastError の値）
- `HResult: int?` - HRESULT 値（該当する場合）
- `ErrorMessage: string` - 英語システムエラーメッセージ
- `LocalizedMessage: string` - 日本語ユーザー向けメッセージ
- `Context: string` - エラーが発生したコンテキスト
- `Timestamp: DateTime` - エラー発生時刻
- `AdditionalInfo: Dictionary<string, object>?` - 追加情報（パラメータ等）

### LogEntry
**目的**: インストールセッション内の個別ログメッセージ。

**プロパティ**:
- `Timestamp: DateTime` - メッセージがログされた時刻
- `Level: LogLevel` - 情報、警告、エラー、デバッグ
- `Message: string` - 英語ログメッセージ
- `Context: string` - 操作コンテキスト
- `ApiError: ApiErrorInfo?` - 関連する Windows API エラー
- `CorrelationId: Guid` - セッション相関識別子

## 列挙型

### InstallationStatus
- `保留中` - インストールがキューに入っているが開始されていない
- `進行中` - インストールが現在実行中
- `完了` - インストール成功
- `失敗` - インストール失敗
- `キャンセル` - ユーザーによってインストールがキャンセルされた

### LogLevel
- `Debug` - デバッグ情報
- `Information` - 一般情報
- `Warning` - 警告
- `Error` - エラー
- `Critical` - 重大エラー

## エンティティ関係

```
DriverPackage (1) ←→ (1) InstallationSession
InstallationSession (1) ←→ (*) LogEntry
InstallationSession (1) ←→ (1) InstallationResult
InstallationResult (1) ←→ (0..1) ApiErrorInfo
LogEntry (1) ←→ (0..1) ApiErrorInfo
```

## データベーススキーマ考慮事項
*注意: このコンソールアプリケーションはインメモリオブジェクトストレージを使用しますが、設計は将来の永続化をサポートします。*

**主キー**:
- DriverPackage: InfFilePath
- InstallationSession: SessionId
- InstallationResult: SessionId

**インデックス**（永続化を実装する場合）:
- InstallationSession.StartTime（時系列クエリ用）
- LogEntry.CorrelationId（セッションログ取得用）

## 責務境界の明確化

### システムが実行すること
- INF ファイルに基づく包括的なインストール処理の実行
- 適切な順序でのセクション適用とサービス登録（SetupInstallFromInfSectionW / SetupInstallServicesFromInfSectionW の内部使い分け）
- API エラーレスポンスの処理と日本語メッセージ変換
- インストール進捗の追跡とログ記録

### システムが実行しないこと
- INF ファイルの事前構文検証
- ドライバファイルの事前存在チェック
- 管理者権限の事前チェック
- 競合状況の事前検出
- デジタル署名の事前検証
- カスタムビジネスルール検証
- INF ファイル内容の解析や変更
- PnP デバイス個体への適用やデバイス状態確認

### 内部実装詳細（ユーザーに露出しない）
- SetupInstallFromInfSectionW と SetupInstallServicesFromInfSectionW の使い分けロジック
- .Services セクションの存在確認と条件付き実行
- API 呼び出しの最適な順序制御
- 各 API のエラー状態管理と統合

**理由**: ユーザーは「INF に従ってインストールを実行する」という単一の操作を求めており、内部的な API の使い分けは実装詳細である。システムは INF に記述された内容を適切に解釈し、必要な処理を自動的に実行することで、シンプルで直感的なインターフェースを提供する。