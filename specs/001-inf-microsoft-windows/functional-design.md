# 外部仕様書

## 概要

Windows デバイスドライバ INF インストーラは、INF ファイルを使用してデバイスドライバの宣言的インストールを実行するコンソールアプリケーションです。このソフトウェアは、SetupAPI を使用して INF ファイルの指示（CopyFiles、AddReg、Services等）を安全に適用し、開発者やシステム管理者が再利用可能でテスト可能なドライバインストール処理を実現できます。

## 機能

### 主要機能

1. **宣言的INFインストール実行**
   - 指定されたINFファイルの指定セクション（デフォルト: DefaultInstall）を実行
   - ファイルコピー操作（CopyFiles）の実行
   - レジストリ操作（AddReg）の実行
   - サービス登録（.Services セクション）の実行

2. **事前チェック機能（ドライランモード）**
   - 実際のインストールを行わずに、INFファイルの妥当性を検証
   - 必要なファイルの存在確認
   - セクションの存在確認

3. **詳細ログ出力**
   - インストール処理の詳細な進行状況を表示
   - エラー発生時の詳細な診断情報を提供

4. **日本語ローカライゼーション**
   - ユーザー向けメッセージを日本語で表示
   - 技術的なログは英語で出力（開発者・トラブルシューティング用）

5. **複数出力形式対応**
   - 標準テキスト出力
   - JSON形式での構造化出力

## ユーザーインターフェース

### コマンドライン引数

#### 基本構文
```
sample_win_devicedriver_inf_install.exe [オプション]
```

#### オプション一覧

| オプション | 必須 | 説明 | 例 |
|-----------|------|------|-----|
| `--install` | ○ | インストールコマンドを実行 | `--install` |
| `--inf <パス>` | ○ | INFファイルのパスを指定 | `--inf "C:\Driver\sample.inf"` |
| `--section <名前>` | × | インストールセクション名を指定（デフォルト: DefaultInstall） | `--section CustomInstall` |
| `--verbose` | × | 詳細なログ出力を有効化 | `--verbose` |
| `--output <形式>` | × | 出力形式を指定（json） | `--output json` |
| `--dry-run` | × | 事前チェックのみ実行（実際のインストールは行わない） | `--dry-run` |
| `--help` | × | ヘルプメッセージを表示 | `--help` |

#### 使用例

**基本的なインストール**
```cmd
sample_win_devicedriver_inf_install.exe --install --inf "C:\Driver\mydriver.inf"
```

**特定セクションの詳細インストール**
```cmd
sample_win_devicedriver_inf_install.exe --install --inf "C:\Driver\mydriver.inf" --section MyInstall --verbose
```

**事前チェック（JSON出力）**
```cmd
sample_win_devicedriver_inf_install.exe --install --inf "C:\Driver\mydriver.inf" --dry-run --output json
```

### 出力メッセージ

#### 成功時のメッセージ

| 条件 | メッセージ | 表示タイミング |
|------|-----------|---------------|
| 正常完了 | "インストールが正常に完了しました。" | インストール成功時 |
| ドライラン完了 | "事前チェックが完了しました。問題は見つかりませんでした。" | ドライラン成功時 |
| 詳細モード開始 | "詳細モードが有効になりました。" | --verbose指定時 |

#### エラーメッセージ

| エラー条件 | メッセージ | 終了コード |
|-----------|-----------|-----------|
| INFファイル未指定 | "--inf オプションでINFファイルのパスを指定してください。" | 1 |
| INFファイル未存在 | "指定されたINFファイルが見つかりません: {パス}" | 1 |
| 不正な出力形式 | "サポートされていない出力形式です: {形式}" | 1 |
| SetupAPI エラー | "インストール中にエラーが発生しました: {詳細}" | 1 |
| アクセス権限不足 | "管理者権限が必要です。管理者として実行してください。" | 1 |

## ソフトウェアインターフェース

### .NETクラスライブラリインターフェース

本アプリケーションは以下のパブリックインターフェースを通じて、他のソフトウェアからの統合が可能です。

#### IDriverInstallationService

```csharp
/// <summary>
/// ドライバインストールサービスのインターフェース
/// </summary>
public interface IDriverInstallationService
{
    /// <summary>
    /// 指定されたINFファイルとセクションを使用してドライバをインストールします
    /// </summary>
    /// <param name="infPath">INFファイルのパス</param>
    /// <param name="sectionName">インストールセクション名</param>
    /// <param name="dryRun">ドライランモード（true: 実際のインストールを行わない）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>インストール結果</returns>
    Task<InstallationResult> InstallAsync(string infPath, string sectionName, bool dryRun = false, CancellationToken cancellationToken = default);
}
```

#### IInstallationLogger

```csharp
/// <summary>
/// インストールログ記録のインターフェース
/// </summary>
public interface IInstallationLogger
{
    /// <summary>
    /// ログエントリを記録します
    /// </summary>
    /// <param name="level">ログレベル</param>
    /// <param name="message">メッセージ</param>
    /// <param name="exception">例外情報（オプション）</param>
    void Log(LogLevel level, string message, Exception? exception = null);
    
    /// <summary>
    /// インストールセッションを開始します
    /// </summary>
    /// <param name="infPath">INFファイルパス</param>
    /// <param name="sectionName">セクション名</param>
    /// <returns>インストールセッション</returns>
    InstallationSession StartSession(string infPath, string sectionName);
}
```

### JSON出力形式

#### 成功時の出力形式
```json
{
  "status": "success",
  "timestamp": "2025-09-24T10:30:00Z",
  "infPath": "C:\\Driver\\sample.inf",
  "sectionName": "DefaultInstall",
  "operations": [
    {
      "type": "CopyFiles",
      "status": "completed",
      "details": "3 files copied successfully"
    },
    {
      "type": "AddReg", 
      "status": "completed",
      "details": "Registry entries added"
    },
    {
      "type": "Services",
      "status": "completed", 
      "details": "Service registered successfully"
    }
  ],
  "rebootRequired": false
}
```

#### エラー時の出力形式
```json
{
  "status": "error",
  "timestamp": "2025-09-24T10:30:00Z",
  "infPath": "C:\\Driver\\sample.inf",
  "sectionName": "DefaultInstall",
  "error": {
    "code": "SETUP_API_ERROR",
    "message": "指定されたセクションが見つかりません",
    "details": "Section 'DefaultInstall' not found in INF file"
  }
}
```

## 実現方式

### アーキテクチャ概要

本ソフトウェアは、依存性注入（DI）パターンと抽象化層を使用したクリーンアーキテクチャを採用しています。

```
┌─────────────────────────────────────────┐
│             CLI Layer                   │
│  CliParser, Program, CommandHandlers    │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          Application Layer              │
│     Services, LocalizationService      │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│           Domain Layer                  │
│      Models, Contracts, Enums          │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│       Infrastructure Layer             │
│   SetupApiWrapper, Native/SetupApi     │
└─────────────────────────────────────────┘
```

### 使用技術スタック

- **フレームワーク**: .NET 10
- **依存性注入**: Microsoft.Extensions.DependencyInjection
- **ログ**: Microsoft.Extensions.Logging
- **ホスティング**: Microsoft.Extensions.Hosting
- **ネイティブAPI**: Windows SetupAPI (setupapi.dll)
- **ローカライゼーション**: .NET Resources (.resx)

### 重要な設計方針

1. **抽象化による分離**
   - `ISetupApiWrapper`により、Windows SetupAPIの呼び出しを抽象化
   - テスト時はモック/スタブで置き換え可能

2. **3段階インストール処理**
   - Phase 1: ファイル操作（CopyFiles等） - ファイルキューAPI使用
   - Phase 2: レジストリ操作（AddReg等） - SetupInstallFromInfSectionW使用
   - Phase 3: サービス登録（.Services） - SetupInstallServicesFromInfSectionW使用

3. **エラーハンドリング**
   - WindowsApiErrorHandler による Windows API エラーの日本語化
   - 構造化ログによる詳細なエラー情報記録

4. **テスト戦略**
   - UnitTest: OS環境を変更せず、全てモック/スタブで実行
   - IntegrationTest: スタブを使用してCIで安全に実行
   - OS環境テスト: 専用環境でのみ実際のOS変更を伴う検証

## 保守機能

### ログ機能

#### ログレベル
- **Error**: エラー発生時の詳細情報
- **Warning**: 警告事項（継続可能な問題）
- **Information**: 処理の進行状況
- **Debug**: 詳細なデバッグ情報（--verboseモード時）

#### ログ出力先
- **コンソール**: ユーザー向けメッセージ（日本語）
- **構造化ログ**: 技術者向け詳細情報（英語）

#### ログ内容例
```
2025-09-24 10:30:00 [INFO] インストールを開始します: C:\Driver\sample.inf
2025-09-24 10:30:01 [DEBUG] Opening INF file: C:\Driver\sample.inf
2025-09-24 10:30:01 [DEBUG] File queue created successfully
2025-09-24 10:30:02 [INFO] ファイルコピー操作が完了しました
2025-09-24 10:30:03 [INFO] レジストリ操作が完了しました
2025-09-24 10:30:04 [INFO] サービス登録が完了しました
2025-09-24 10:30:04 [INFO] インストールが正常に完了しました
```

### 設定管理

本アプリケーションは設定ファイルを使用せず、全ての設定をコマンドライン引数で指定します。これにより、設定ファイルの管理負担を軽減し、スクリプトからの自動化を容易にしています。

### 監視ポイント

1. **インストール成功率**: 正常完了 vs エラー終了の比率
2. **頻繁なエラーパターン**: 特定のエラーコードの出現頻度
3. **実行時間**: 大きなドライバパッケージでの処理時間
4. **リソース使用量**: メモリ使用量とファイルハンドル数

## 開発環境

### 必要な環境

#### 開発時
- **OS**: Windows 10/11 (x64)
- **開発環境**: Visual Studio 2022 または Visual Studio Code
- **.NET SDK**: .NET 10 SDK
- **テスト実行**: xUnit テストランナー

#### ビルド時
```cmd
# Debug ビルド
dotnet build --configuration Debug

# Release ビルド
dotnet build --configuration Release

# 単体テスト実行
dotnet test

# 発行
dotnet publish --configuration Release --runtime win-x64 --self-contained false
```

#### 実行時
- **OS**: Windows 10/11
- **.NET Runtime**: .NET 10 Runtime
- **権限**: 管理者権限（ドライバインストール時）

### 開発ガイドライン

#### コーディング規約
- C# コーディング規約に準拠
- XMLドキュメントコメントを日本語で記述
- ログメッセージ: UI向けは日本語、技術ログは英語

#### テスト方針
- **UnitTest**: 外部環境を変更しない、完全密閉型テスト
- **IntegrationTest**: スタブ使用、CI実行可能
- **OS環境テスト**: 専用環境でのみ実行、手動実行

#### 依存関係管理
- 新しい外部依存の追加は最小限に制限
- OS固有機能は必ずインターフェースで抽象化
- テスト時の差し替えを考慮した設計

### 配布・展開

#### 配布形式
- **フレームワーク依存**: .NET 10 Runtime が必要（推奨）
- **自己完結型**: Runtime 含む（サイズ大）

#### インストール手順
1. .NET 10 Runtime のインストール（フレームワーク依存版の場合）
2. 実行ファイルを任意のフォルダに配置
3. PATH環境変数への追加（オプション）
4. 管理者権限での実行確認