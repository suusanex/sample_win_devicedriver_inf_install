# 実装タスク: Windows デバイスドライバ INF インストーラ（宣言的インストール専用・ファイル操作対応版）

## タスク概要（重要修正）

このドキュメントは、Windows デバイスドライバ INF インストーラのコンソールアプリケーション実装のための実行可能タスクリストです。**宣言的インストール専用**に焦点を絞り、**CopyFiles を含む完全なINF セクション適用**を実現するため、機能単位でのまとまりを重視し、Plan の機能要件を満たす粒度でタスクを設計しています。

**重要な修正**: SetupInstallFromInfSection だけでは CopyFiles が実行されないことが判明。**ファイルキュー操作を含む3段階実行**に修正。

**機能**: INF ファイルに従った**完全な**宣言的インストール（ファイル操作・レジストリ操作・サービス登録）
**対象**: .NET 10 コンソールアプリケーション  
**技術スタック**: SetupAPI（**ファイルキュー操作を含む**完全な宣言的セクション適用）, Microsoft.Extensions.Logging, Microsoft.Extensions.Hosting (GenericHost), リソースベースローカライゼーション

## 実装方針の重要な変更

### 従来の実装（不完全・CopyFiles未実行）
```csharp
// 1段階実行（CopyFiles が実行されない）
SetupInstallFromInfSection(infHandle, sectionName, SPINST_ALL, ...);
SetupInstallServicesFromInfSection(infHandle, servicesSectionName, ...);
```

### 修正後の実装（完全・3段階実行）
```csharp
// 3段階実行（CopyFiles を含む完全なインストール）
// 1. ファイル操作段階
var fileQueue = SetupOpenFileQueue();
SetupInstallFilesFromInfSection(infHandle, sectionName, fileQueue, ...);
SetupCommitFileQueue(owner, fileQueue, ...);
SetupCloseFileQueue(fileQueue);

// 2. レジストリ操作段階  
SetupInstallFromInfSection(infHandle, sectionName, SPINST_REGISTRY | SPINST_INIFILES, ...);

// 3. サービス登録段階
SetupInstallServicesFromInfSection(infHandle, servicesSectionName, ...);
```

## 変更点（CLI オプション統一）
- **CLI 統一**: `--install` による単一のインストール操作に統合
- **内部実装**: **3段階実行**（ファイル→レジストリ→サービス）による完全なインストール
- **ユーザー観点**: 「INF に従ってインストールを実行する」という直感的な 1 単位操作

## 緊急修正が必要な箇所

### 1. ISetupApiWrapper インターフェース（T002で修正）
**不足メソッド**: ファイルキュー操作用メソッドが未実装
```csharp
// 追加が必要なメソッド
IntPtr SetupOpenFileQueue();
bool SetupInstallFilesFromInfSection(IntPtr infHandle, string sectionName, IntPtr fileQueue, string? sourceRootPath, uint copyStyle);
bool SetupCommitFileQueue(IntPtr owner, IntPtr fileQueue, IntPtr msgHandler, IntPtr context);
void SetupCloseFileQueue(IntPtr fileQueue);
```

### 2. Native/SetupApi.cs（T002で修正）
**不足P/Invoke**: ファイルキュー関連API宣言が未実装
```csharp
// 追加が必要なP/Invoke宣言
[DllImport("setupapi.dll", SetLastError = true)]
public static extern IntPtr SetupOpenFileQueue();

[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern bool SetupInstallFilesFromInfSection(...);
// 他のファイルキューAPI
```

### 3. DriverInstallationService（T003で修正）  
**実装変更**: 1段階実行から3段階実行への変更
```csharp
// 従来: InstallFromInfSectionAsync の単純実装
// 修正後: 3段階実行ロジックの実装
private async Task InstallFromInfSectionAsync(...) {
    // 1. ファイル操作段階
    await InstallFilesFromInfAsync(...);
    // 2. レジストリ操作段階  
    await InstallRegistryFromInfAsync(...);
    // 3. サービス登録段階
    await InstallServicesFromInfAsync(...);
}
```

## タスク設計方針

- **機能完結性**: 各タスクで Plan の機能が最低1つ動作するまとまりを実現
- **レビュー可能性**: 機能の正しさを一目で判断できる粒度
- **テスト統合**: 機能単位でのテストを含む
- **ブラックボックス化**: 内部処理の詳細ではなく、機能としての完結性を重視
- **緊急修正優先**: CopyFiles 未実行問題の解決を最優先

## タスク実行ルール

- **[P]** = 並列実行可能（独立した機能を実装するタスク）
- **[URGENT]** = CopyFiles 問題解決のための緊急修正タスク
- **依存関係**: 番号順に実行、特に記載がない限り前のタスク完了を待つ
- **機能テスト**: 各タスクで実装した機能が動作することを確認
- **検証**: 各タスク完了後に機能動作確認を実行

---

## T001: プロジェクト基盤とデータ構造の構築（変更なし）

**機能目標**: アプリケーションの基盤となるプロジェクト構造とデータモデルを構築し、ビルドが通る状態を実現

**実装範囲**:
- 既存の sample_win_devicedriver_inf_install.csproj（コンソールアプリ部分）を引き継ぐ
- 必要な NuGet パッケージの追加
  - Microsoft.Extensions.Hosting（GenericHost）
  - Microsoft.Extensions.Logging
  - Microsoft.Extensions.DependencyInjection
- ソリューション構造の構築
- コンソールアプリプロジェクトへのGenericHostの導入
  - HostBuilder の設定
  - DI コンテナの設定
  - サービス登録
- データモデルエンティティの実装（宣言的インストール専用に簡素化）
  - DriverPackage, InstallationSession, InstallationResult
  - ApiErrorInfo, LogEntry などの値オブジェクト
  - InstallationStatus, LogLevel などの列挙型
- コントラクトインターフェースの定義
  - IDriverInstallationService（統一されたインストール専用）, IInstallationLogger

**成果物**:
```
sample_win_devicedriver_inf_install/
  ├── sample_win_devicedriver_inf_install.csproj (既存・更新)
  ├── Program.cs (GenericHost対応)
  ├── Models/
  │   ├── DriverPackage.cs
  │   ├── InstallationSession.cs
  │   ├── InstallationResult.cs
  │   └── ValueObjects/
  │       ├── ApiErrorInfo.cs
  │       └── LogEntry.cs
  ├── Contracts/
  │   ├── IDriverInstallationService.cs
  │   └── IInstallationLogger.cs
  └── Enums/
      ├── InstallationStatus.cs
      └── LogLevel.cs
tests/sample_win_devicedriver_inf_install.Tests/
tests/sample_win_devicedriver_inf_install.IntegrationTests/
```

**機能テスト**:
- プロジェクトが正常にビルドされる
- GenericHostが正しく初期化される
- データモデルの検証ルールが正しく動作する
- インターフェースが適切に定義されている
- DI コンテナでサービスが正しく登録される

**完了条件**: ソリューション全体がビルドエラーなく構築され、GenericHostベースのコンソールアプリケーションが起動し、データ構造が設計通りに動作する

---

## T002: Windows API統合とエラーハンドリング機能（緊急修正・ファイルキュー対応）[URGENT]

**機能目標**: SetupAPI（**ファイルキュー操作を含む完全な**宣言的インストール）との統合機能を実装し、API エラーを適切に日本語メッセージに変換できる機能を実現

**実装範囲（緊急修正）**:
- SetupAPI の P/Invoke ラッパー実装（**ファイルキュー操作を追加**）
  - **既存**: SetupOpenInfFileW, SetupInstallFromInfSectionW, SetupInstallServicesFromInfSectionW, SetupCloseInfFile
  - **追加**: SetupOpenFileQueue, SetupInstallFilesFromInfSection, SetupCommitFileQueue, SetupCloseFileQueue
  - GetLastError
  - 関連構造体と定数の定義
- ISetupApiWrapper インターフェースの拡張（**ファイルキューメソッド追加**）
  - 既存メソッドの保持
  - ファイルキュー操作メソッドの追加
- SetupApiWrapper 実装クラスの拡張
  - ファイルキュー操作メソッドの実装
- SetupApiStub テストスタブの拡張
  - ファイルキュー操作のスタブ実装
- Windows API エラーハンドラーの実装
  - エラーコードの解析と処理
  - 日本語メッセージへの変換
  - **ファイル操作関連エラー**の処理追加
  - ApiErrorInfo オブジェクトの生成
- リソースファイルによるローカライゼーション
  - 主要な SetupAPI エラーコードの日本語メッセージ
  - **ファイルキュー操作エラー**の日本語メッセージ追加
  - エラーメッセージリソースファイル

**成果物（修正）**:
```
sample_win_devicedriver_inf_install/
  ├── Native/
  │   └── SetupApi.cs（ファイルキューP/Invoke追加）
  ├── Contracts/
  │   └── ISetupApiWrapper.cs（ファイルキューメソッド追加）
  ├── Services/
  │   ├── SetupApiWrapper.cs（ファイルキュー実装追加）
  │   ├── WindowsApiErrorHandler.cs
  │   └── LocalizationService.cs
  └── Resources/
      ├── ErrorMessages.ja.resx（ファイル操作エラー追加）
      └── Messages.ja.resx
sample_win_devicedriver_inf_install.Tests/Stubs/
  └── SetupApiStub.cs（ファイルキューメソッド追加）
```

**機能テスト（修正）**:
- SetupAPI 関数（**ファイルキューを含む**宣言的インストール用）が正しく呼び出せる
- **ファイルキュー操作**が正しく動作する
- 意図的にエラーを発生させて日本語メッセージが取得できる（**ファイル操作エラーを含む**）
- 未知のエラーコードでも適切なフォールバック処理が動作する
- リソースファイルからメッセージが正しく取得できる

**完了条件**: Windows API エラーが発生した際に、適切な日本語メッセージが生成され、技術詳細も含めて記録される。**ファイルキュー操作がすべて利用可能になる**。

---

## T003: ドライバインストール機能の実装（緊急修正・3段階実行対応）[P][URGENT]

**機能目標**: INF ファイルに従った**完全な**宣言的インストール機能を実装し、**3段階実行**（ファイル→レジストリ→サービス）による CopyFiles を含む包括的インストールを実現

**実装範囲（緊急修正）**:
- IDriverInstallationService の実装（**3段階実行による完全インストール**）
  - InstallFromInfAsync(infPath, sectionName="DefaultInstall", flags=0)
  - **内部3段階実行**:
    1. **ファイル操作段階**: SetupInstallFilesFromInfSection + SetupCommitFileQueue
    2. **レジストリ操作段階**: SetupInstallFromInfSectionW（非ファイルフラグのみ）
    3. **サービス登録段階**: SetupInstallServicesFromInfSectionW
  - .Services セクションの自動検出と条件付き実行
  - インストールセッション管理
  - 進捗追跡とステータス更新（**3段階の個別進捗**）
  - エラーハンドリング（**段階別エラー処理**）
  - キャンセレーション対応

**実装詳細（修正）**:
- INF セクション解析による .Services セクションの存在確認
- **3段階実行ロジック**:
  ```csharp
  // 1. ファイル操作段階（CopyFiles等）
  private async Task InstallFilesFromInfAsync(IntPtr infHandle, string sectionName, ...);
  
  // 2. レジストリ操作段階（AddReg等）
  private async Task InstallRegistryFromInfAsync(IntPtr infHandle, string sectionName, ...);
  
  // 3. サービス登録段階（Services セクション）
  private async Task InstallServicesFromInfAsync(IntPtr infHandle, string baseSectionName, ...);
  ```
- API 呼び出しエラーの統合処理（**段階別エラー情報**）
- 処理順序の最適化とロールバック処理
- **タイムアウト制御**: 各段階で30秒のタイムアウト（合計最大90秒）

**成果物（修正）**:
```
sample_win_devicedriver_inf_install/Services/
  └── DriverInstallationService.cs（3段階実行による完全インストール）
```

**機能テスト（修正）**:
- 有効な INF ファイルで**完全な**インストールが実行される（**CopyFiles を含む**）
- **3段階実行**が正しい順序で実行される
- .Services セクションが自動検出・適用される
- セクションが存在しない場合に適切に処理される
- インストール進捗が**段階別に**適切に追跡される
- エラー発生時に**段階情報を含む**適切な ApiErrorInfo が生成される
- インストールセッションが正しく管理される
- **ファイル操作**（CopyFiles）が実際に実行されることを確認

**完了条件**: INF ファイルパスを指定して `InstallFromInfAsync` を呼び出すと、INF に記述されたすべての関連処理（**CopyFiles、AddReg、サービス登録**）が3段階で自動実行され、完全な宣言的インストールが実現される。

---

## T004: ログ機能の実装（3段階実行対応）[P]

**機能目標**: インストール処理の詳細なログ記録機能を実装し、**3段階実行**の各ステップを個別追跡してトラブルシューティングに必要な情報を提供

**実装範囲（修正）**:
- IInstallationLogger の実装
  - Microsoft.Extensions.Logging を使用したログ実装
  - 構造化ログ機能（**段階別ログカテゴリ**）
  - ファイル出力とコンソール出力の分離
  - 相関ID による処理追跡
  - 日本語UI用ログと英語技術ログの分離
  - **3段階実行の個別ログ**:
    - "FileOperation" カテゴリ（ファイル操作段階）
    - "RegistryOperation" カテゴリ（レジストリ操作段階）
    - "ServiceOperation" カテゴリ（サービス登録段階）
- ログローテーションとエクスポート機能
  - ファイルサイズベースのローテーション
  - JSON, XML, CSV 形式でのエクスポート
  - ログレベルの動的変更

**成果物**:
```
sample_win_devicedriver_inf_install/Services/
  └── InstallationLogger.cs（3段階実行対応）
```

**機能テスト（修正）**:
- ログが適切なレベルで記録される
- 相関ID によるセッション追跡が動作する
- **3段階実行の各ステップ**が個別にログ記録される
- ファイルローテーションが正しく実行される
- 構造化ログが適切な形式で出力される
- SetupAPI 呼び出し結果が**段階別に**詳細に記録される

**完了条件**: インストール処理中のすべての操作が**3段階に分けて**ログに記録され、セッション単位での追跡とトラブルシューティングが可能になる

---

## T005: CLI インターフェースの実装（変更なし）

**機能目標**: コマンドラインから統一された宣言的インストール機能を使用できるユーザーインターフェースを実装

**実装範囲**:
- CLI パーサーの実装
  - `--install --inf <INFファイルパス> [--section DefaultInstall]` 統一オプション
  - `--verbose` デバッグオプション
  - `--output json` 出力形式オプション
  - `--dry-run` 事前チェックオプション
  - `--help` ヘルプ表示
- GenericHostベースのメインプログラムの実装
  - HostBuilder の設定とサービス登録
  - CLI コマンドの実行制御
  - 終了コードの適切な設定
  - 例外ハンドリング
  - IHostedService を使用したコマンド実行
- 日本語ユーザーインターフェース
  - 進捗表示メッセージ（**3段階実行の進捗**）
  - 成功/失敗メッセージ
  - ヘルプテキスト

**成果物**:
```
sample_win_devicedriver_inf_install/
  ├── CliParser.cs
  ├── Program.cs (GenericHost対応)
  ├── Services/
  │   └── CommandExecutorService.cs
  └── CommandHandlers/
      ├── InstallCommand.cs（統一されたインストールコマンド）
      └── HelpCommand.cs
```

**機能テスト**:
- `sample_win_devicedriver_inf_install.exe --install --inf driver.inf` で**完全な**インストールが実行される
- インストール中に**3段階実行**（ファイル→レジストリ→サービス）が自動実行される
- エラー時に適切な日本語メッセージが表示される
- `--help` で使用方法が日本語で表示される
- 終了コードが正しく設定される
- GenericHostが正しく動作する

**完了条件**: quickstart.md のコマンド例がすべて実行でき、サイレントモードで**CopyFiles を含む**包括的な宣言的インストールが完了する

---

## T006: 基本機能統合テストの実装（3段階実行検証対応）

**機能目標**: quickstart.md のシナリオを自動テストとして実装し、エンドツーエンドでの**3段階実行による完全なインストール**機能動作を検証

**実装範囲（修正）**:
- **完全インストールシナリオテスト**
  - INF ファイル指定による**3段階実行**インストールテスト
  - **CopyFiles 実行の検証**（ファイル操作段階）
  - **AddReg 実行の検証**（レジストリ操作段階）
  - **サービス登録の検証**（サービス登録段階）
  - インストール成功の検証
  - ログファイル生成の確認（**段階別ログ**）
- エラーハンドリングシナリオテスト
  - 不正な INF ファイルでのテスト
  - 権限不足エラーのシミュレーション
  - **ファイル操作エラー**のシミュレーション（ディスク容量不足、アクセス権限等）
  - エラーメッセージの日本語表示確認
  - 存在しないセクション指定時の処理
  - **段階別エラー発生**時の処理確認
- インストール検証シナリオテスト
  - INF に従った**完全インストール**後の状態確認テスト
  - **3段階実行**の正常動作確認
  - 各段階の完了確認テスト

**成果物（修正）**:
```
tests/sample_win_devicedriver_inf_install.IntegrationTests/
  ├── Scenarios/
  │   ├── CompleteInstallationScenarioTests.cs（3段階実行検証）
  │   ├── FileOperationScenarioTests.cs（CopyFiles検証）
  │   ├── RegistryOperationScenarioTests.cs（AddReg検証）
  │   ├── ServiceOperationScenarioTests.cs（サービス登録検証）
  │   ├── ErrorHandlingScenarioTests.cs（段階別エラー処理）
  │   └── VerificationScenarioTests.cs
  ├── TestData/
  │   └── SampleDrivers/
  │       ├── complete_driver.inf（CopyFiles + AddReg + Services を含む）
  │       ├── file_only_driver.inf（CopyFiles のみ）
  │       └── registry_only_driver.inf（AddReg のみ）
  └── Fixtures/
      └── TestEnvironmentFixture.cs
```

**機能テスト（修正）**:
- quickstart.md の**完全インストールシナリオ**が自動実行される
- **3段階実行**が正しい順序で透過的に動作することを確認
- **CopyFiles が実際に実行される**ことを確認
- 各段階でのエラー条件でも適切に処理される
- すべてのテストが管理者権限環境で実行される

**完了条件**: quickstart.md に記載されたすべてのシナリオが自動テストとして実行でき、**CopyFiles を含む完全なインストール**の期待される結果が得られる

---

## T007: パフォーマンス最適化と品質向上（3段階実行対応）[P]

**機能目標**: パフォーマンス要件を満たし、**3段階実行による完全インストール**が本番環境で使用できる品質レベルを実現

**実装範囲（修正）**:
- パフォーマンステストの実装
  - **3段階実行**インストール時間の測定（合計90秒以内の確認）
  - メモリ使用量の監視（**ファイルキュー操作を含む**）
  - 大量ファイル処理のテスト（**CopyFiles 大量ファイル対応**）
  - タイムアウト処理の検証（**段階別タイムアウト**）
- メモリ使用量最適化
  - SetupAPI リソースの確実な解放（**ファイルキューハンドルを含む**）
  - ログエントリのバッファリング最適化
  - 大きなオブジェクトの適切な Dispose
  - **ファイルキュー操作のリソース管理**
- ユニットテストの拡充
  - 各サービスクラスの詳細テスト（**3段階実行ロジック**）
  - エッジケースのテスト追加（**ファイル操作エラー等**）
  - 例外処理のテスト（**段階別例外処理**）
  - モック検証の強化（**ファイルキューAPI**）

**成果物（修正）**:
```
tests/sample_win_devicedriver_inf_install.PerformanceTests/
  └── PerformanceTests.cs（3段階実行性能測定）
tests/sample_win_devicedriver_inf_install.Tests/
  ├── Services/
  │   ├── DriverInstallationServiceTests.cs（3段階実行ロジック）
  │   ├── InstallationLoggerTests.cs（段階別ログ）
  │   └── WindowsApiErrorHandlerTests.cs（ファイル操作エラー）
  └── Models/
      └── DataModelTests.cs
```

**機能テスト（修正）**:
- パフォーマンス要件（**3段階合計90秒以内**等）が満たされる
- メモリリークが検出されない（**ファイルキューハンドル含む**）
- コードカバレッジが 85% 以上になる
- 長時間実行でも安定動作する（**ファイル操作を含む**）

**完了条件**: quickstart.md のパフォーマンス検証がすべて通り、**3段階実行による完全インストール**が本番環境での使用に耐える品質が確保される

---

## T008: HLK互換性検証と最終ドキュメント（CopyFiles対応確認）

**機能目標**: HLK 互換性要件を満たし、**CopyFiles を含む完全なインストール**機能についてユーザーが自立して使用できるドキュメントを完成

**実装範囲（修正）**:
- HLK 互換性検証
  - HLK テスト環境での動作確認（**CopyFiles 実行確認**）
  - Windows 署名要件の確認
  - セキュリティベストプラクティスの確認
  - **完全なINF処理**の互換性確認
- CLI ヘルプとドキュメントの充実
  - `--help` オプションの詳細実装
  - 使用例の追加（**CopyFiles を含むINF例**）
  - エラーメッセージの改善（**段階別エラー説明**）
- README.md の更新
  - インストール手順
  - 使用方法（**3段階実行の説明**）
  - トラブルシューティング（**CopyFiles関連問題**）
  - HLK テスト対応

**成果物（修正）**:
```
docs/
  ├── HlkCompatibility.md（CopyFiles実行確認項目）
  ├── TroubleshootingGuide.md（ファイル操作問題対応）
  └── ThreePhaseInstallation.md（3段階実行の詳細説明）
README.md (更新・CopyFiles対応説明)
```

**機能テスト（修正）**:
- HLK テストで互換性が確認される（**CopyFiles実行確認**）
- ユーザーが README だけで使用開始できる
- すべてのエラーメッセージが分かりやすい（**段階別エラー含む**）
- ヘルプが十分詳細で実用的（**完全インストールの説明**）

**完了条件**: HLK 互換性が確認され、ユーザーが**CopyFiles を含む完全なインストール**機能を自立して使用できるドキュメントが完成する

---

## 緊急修正タスクの実行順序

### 最優先実行（CopyFiles問題解決）
```bash
# 緊急修正: ファイルキュー操作の実装
/tasks T002  # ISetupApiWrapper + Native API 拡張
/tasks T003  # DriverInstallationService 3段階実行化

# 検証
dotnet test --filter "Category=FileOperation"
```

### 段階的実行（修正後）
```bash
# 段階 1: 基盤構築
/tasks T001

# 段階 2: 緊急修正（並列実行可能）
/tasks T002 & /tasks T003 & /tasks T004
wait

# 段階 3: UI実装
/tasks T005

# 段階 4: 検証・品質向上
/tasks T006 & /tasks T007
wait

# 段階 5: 最終検証
/tasks T008
```

## 完了条件（修正・重要）

すべてのタスクが完了し、以下の機能要件が満たされた時点で実装完了とする：

1. ✓ **完全なインストール**: CLI から INF に従った**CopyFiles を含む**包括的なインストールができる
2. ✓ **3段階実行**: ファイル操作→レジストリ操作→サービス登録の正しい順序で実行される
3. ✓ **エラーハンドリング**: SetupAPI エラーが**段階別に**適切な日本語メッセージで表示される  
4. ✓ **ログ機能**: インストール処理の**3段階すべて**がログに記録され、トラブルシューティングができる
5. ✓ **パフォーマンス**: quickstart.md の性能要件が**3段階実行で**満たされる
6. ✓ **HLK互換性**: HLK テストで**CopyFiles実行を含む**互換性が確認される
7. ✓ **ドキュメント**: ユーザーが**完全インストール**機能を自立して使用できるドキュメントが完成する

**最重要検証項目**: CopyFiles 指示が実際に実行され、INF で指定されたファイルがターゲット場所にコピーされることを確認

---
*タスク数: 8タスク | 推定実装期間: 8-12営業日（3段階実行による複雑化を考慮） | CopyFiles問題解決重視設計*