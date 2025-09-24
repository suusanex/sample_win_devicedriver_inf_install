# FR-014 & FR-009 アーキテクチャ分離ドキュメント

## 概要
このドキュメントでは、明確なアーキテクチャ分離を通してコードベースがFR-014とFR-009の要件にどのように準拠しているかを説明します。

## FR-014 準拠: 言語分離
**要件**: ドライバインストールロジックは、保守用トレースログも含めて全て英語で出力する必要があります。日本語UIステータスメッセージやエラー報告は分離してUI層で実装する必要があります。

### 実装戦略

#### コア層（英語のみ）
- **名前空間**: `sample_win_devicedriver_inf_install.Core.*`
- **目的**: すべてのビジネスロジックと技術的処理を含む
- **言語**: コメント、ログ、メッセージ、技術的データはすべて英語のみ
- **コンポーネント**:
  - `Core.Models.*` - 英語のみのデータを持つドメインモデル
  - `Core.Services.*` - ビジネスロジックサービス
  - `Core.Contracts.*` - サービスインターフェース
  - `Core.Models.ValueObjects.*` - `ApiErrorInfo`などの値オブジェクト

#### UI層（日本語インターフェース）
- **名前空間**: `sample_win_devicedriver_inf_install.UI.*`
- **目的**: ユーザーインターフェース関連の処理とローカライズされたメッセージング
- **言語**: 日本語ユーザーメッセージ、ローカライズされたエラーメッセージ
- **コンポーネント**:
  - `UI.Services.ErrorMessageProvider` - コアエラーを日本語メッセージに変換
  - `UI.Services.*` - UI固有のサービス
  - メイン `Program.cs` - 日本語コンソールインターフェース

#### サービス層（ローカライゼーションブリッジ）
- **名前空間**: `sample_win_devicedriver_inf_install.Services.*`
- **目的**: UI層向けのローカライゼーションサービス提供
- **コンポーネント**:
  - `LocalizationService` - リソースファイル管理
  - リソースファイル (`*.resx`) - ローカライズされたメッセージストレージ

## FR-009 準拠: 再利用可能なコンポーネント分離
**要件**: ドライバインストールロジックは、他のインストーラでの再利用のために分離可能なコンポーネントとして実装する必要があります。

### 実装戦略

#### 分離可能なコアコンポーネント
すべてのコア層コンポーネントは抽出・再利用可能に設計されています：

1. **コアモデル** (`Core.Models.*`)
   - 独立したドメインモデル
   - UI依存なし
   - 別ライブラリとしてパッケージ可能

2. **コアサービス** (`Core.Services.*`)
   - UI関連処理を含まないビジネスロジック
   - 依存性注入のためのインターフェースベース設計
   - プラットフォーム独立契約

3. **コア契約** (`Core.Contracts.*`)
   - すべてのサービスの抽象インターフェース
   - 異なる実装を可能にする
   - テストとモック作成をサポート

#### 再利用性設計パターン

1. **依存性注入**
   - すべてのコアサービスはコンストラクタ注入を使用
   - インターフェースを通じて定義された依存関係
   - 実装の簡単な交換が可能

2. **設定ベース**
   - ハードコードされたUI依存なし
   - DIコンテナを通じて設定可能
   - 異なるホスティング環境をサポート

3. **Async/Awaitパターン**
   - すべての操作は非同期
   - 異なる実行コンテキストをサポート
   - キャンセレーショントークンサポート

## 名前空間構造

```
sample_win_devicedriver_inf_install/
├── Core/                           # FR-014: 英語のみビジネスロジック
│   ├── Models/                     # ドメインモデル
│   │   ├── ValueObjects/           # ApiErrorInfo等
│   │   ├── DriverPackage.cs
│   │   └── InstallationResult.cs
│   ├── Services/                   # ビジネスロジックサービス
│   │   └── WindowsApiErrorHandler.cs
│   └── Contracts/                  # サービスインターフェース
│       ├── IDriverInstallationService.cs
│       └── IInstallationLogger.cs
├── UI/                             # FR-014: 日本語ユーザーインターフェース
│   └── Services/
│       └── ErrorMessageProvider.cs
├── Services/                       # ローカライゼーションブリッジ
│   └── LocalizationService.cs
├── Resources/                      # ローカライズリソース
│   ├── ErrorMessages.ja.resx
│   └── Messages.ja.resx
├── Native/                         # Windows API ラッパー
│   └── SetupApi.cs
├── Enums/                         # 共有列挙型
└── Program.cs                      # UI層エントリーポイント
```

## 使用例: 層分離

### コア層（英語技術データ）
```csharp
// コア層は英語で技術的エラー情報を生成
var errorInfo = errorHandler.CreateApiErrorInfo(5, "InstallDriver", "Access denied");
logger.LogError("Core Error: {LogMessage}", errorInfo.GetLogMessage());
```

### UI層（日本語ユーザーメッセージ）
```csharp
// UI層はコアデータを日本語ユーザーメッセージに変換
var japaneseMessage = errorProvider.GetLocalizedErrorMessage(errorInfo);
Console.WriteLine($"エラー: {japaneseMessage}");
```

## 利点

1. **FR-014 準拠**
   - 技術的（英語）とユーザー向け（日本語）の関心事の明確な分離
   - コアロジックは言語中立で再利用可能な状態を維持
   - UI層がすべてのローカライゼーションニーズを処理

2. **FR-009 準拠**
   - コアコンポーネントは別のNuGetパッケージとして抽出可能
   - ビジネスロジックにUI依存なし
   - インターフェースベース設計が異なる実装をサポート

3. **保守性**
   - 明確な責任境界
   - モック実装を使用した簡単なテスト
   - 層間の結合度削減

4. **拡張性**
   - UI層を拡張することで新しい言語の追加が容易
   - 新しいUI実装でもコアロジックは変更不要
   - 異なるデプロイメントシナリオをサポート（コンソール、サービス、GUI）