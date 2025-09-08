# データモデル: Windows デバイスドライバ INF インストーラ

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
**目的**: 単一のドライバインストール操作の状態と進捗を追跡。

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
- `InstalledDevices: List<DeviceInstance>` - 正常にインストールされたデバイス

**検証ルール**:
- SessionId は一意でなければならない
- 進捗開始前に StartTime が設定されている必要がある
- Progress は 0-100 の範囲でなければならない
- EndTime は状態が完了または失敗の場合のみ設定

**状態遷移**:
- `保留中` → `進行中` → `完了`
- `保留中` → `進行中` → `失敗`
- 逆方向の遷移は許可されない

### DeviceInstance
**目的**: インストールされたドライバに関連付けられた特定のハードウェアデバイスを表現。

**プロパティ**:
- `DeviceInstanceId: string` - Windows デバイスインスタンス識別子
- `HardwareId: string?` - 主要ハードウェア識別子（API から取得可能な場合）
- `DeviceName: string?` - 人間が読める形式のデバイス名（API から取得可能な場合）
- `DeviceClass: string?` - デバイスクラス（API から取得可能な場合）
- `DriverVersion: Version?` - インストールされたドライババージョン（API から取得可能な場合）
- `DriverDate: DateTime?` - ドライバ日付（API から取得可能な場合）
- `Status: DeviceStatus` - デバイス動作状態
- `InstallationSession: Guid` - 関連するインストールセッション
- `IsPresent: bool` - デバイスが現在存在するかどうか
- `LastSeen: DateTime` - 最後の検出タイムスタンプ

**検証ルール**:
- DeviceInstanceId は空でない文字列でなければならない
- Status は有効な DeviceStatus 列挙値でなければならない
- InstallationSession は有効な Guid でなければならない

**関係**:
- 一つの InstallationSession に属する
- 一つの DriverPackage を参照する場合がある

### InstallationResult
**目的**: インストール操作の最終結果と詳細情報。

**プロパティ**:
- `Success: bool` - インストール成功フラグ
- `SessionId: Guid` - 関連するセッション識別子
- `ApiError: ApiErrorInfo?` - Windows API エラー情報（失敗時）
- `LocalizedMessage: string` - 日本語ユーザーメッセージ
- `TechnicalDetails: string` - 英語技術詳細（ログ用）
- `InstalledDevices: List<DeviceInstance>` - インストールされたデバイスリスト
- `RequiresReboot: bool` - システム再起動が必要かどうか
- `Duration: TimeSpan` - インストール実行時間
- `LogEntries: List<LogEntry>` - 関連ログエントリ

**検証ルール**:
- Success が false の場合、ApiError または LocalizedMessage が設定されている必要がある
- SessionId は有効な Guid でなければならない
- Duration は非負の値でなければならない

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

### DeviceStatus
- `動作中` - デバイスが正常に機能している
- `エラー` - デバイスに問題がある
- `無効` - ユーザー/システムによってデバイスが無効化されている
- `不明` - 状態を判定できない
- `不存在` - デバイスが現在接続されていない

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
InstallationSession (1) ←→ (*) DeviceInstance
InstallationSession (1) ←→ (1) InstallationResult
InstallationResult (1) ←→ (0..1) ApiErrorInfo
LogEntry (1) ←→ (0..1) ApiErrorInfo
```

## データベーススキーマ考慮事項
*注意: このコンソールアプリケーションはインメモリオブジェクトストレージを使用しますが、設計は将来の永続化をサポートします。*

**主キー**:
- DriverPackage: InfFilePath
- InstallationSession: SessionId
- DeviceInstance: DeviceInstanceId
- InstallationResult: SessionId

**インデックス**（永続化を実装する場合）:
- InstallationSession.StartTime（時系列クエリ用）
- DeviceInstance.HardwareId（デバイス検索用）
- LogEntry.CorrelationId（セッションログ取得用）

## 責務境界の明確化

### システムが実行すること
- Windows API 呼び出しの実行
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

**理由**: これらの検証は実行者（システム利用者）の責務であり、システムは Windows API の実行とその結果の処理のみに焦点を当てることで、責務を明確化し複雑性を削減する。必要な前提条件（管理者権限、適切な署名、競合なし等）は満たされているものと仮定し、不足時は Windows API からのエラーとして検出・処理する。