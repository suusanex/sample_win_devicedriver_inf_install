# 実装タスク: Windows デバイスドライバ INF インストーラ（宣言的インストール専用）

## タスク概要

このドキュメントは、Windows デバイスドライバ INF インストーラのコンソールアプリケーション実装のための実行可能タスクリストです。**宣言的インストール専用**に焦点を絞り、機能単位でのまとまりを重視し、Plan の機能要件を満たす粒度でタスクを設計しています。

**機能**: INF ファイルに従った包括的な宣言的インストール
**対象**: .NET 10 コンソールアプリケーション  
**技術スタック**: SetupAPI（宣言的セクション適用専用）, Microsoft.Extensions.Logging, Microsoft.Extensions.Hosting (GenericHost), リソースベースローカライゼーション

## 変更点（CLI オプション統一）
- **CLI 統一**: `--install` による単一のインストール操作に統合
- **内部実装**: SetupInstallFromInfSectionW + SetupInstallServicesFromInfSectionW の使い分けは内部処理
- **ユーザー観点**: 「INF に従ってインストールを実行する」という直感的な 1 単位操作

## タスク設計方針

- **機能完結性**: 各タスクで Plan の機能が最低1つ動作するまとまりを実現
- **レビュー可能性**: 機能の正しさを一目で判断できる粒度
- **テスト統合**: 機能単位でのテストを含む
- **ブラックボックス化**: 内部処理の詳細ではなく、機能としての完結性を重視

## タスク実行ルール

- **[P]** = 並列実行可能（独立した機能を実装するタスク）
- **依存関係**: 番号順に実行、特に記載がない限り前のタスク完了を待つ
- **機能テスト**: 各タスクで実装した機能が動作することを確認
- **検証**: 各タスク完了後に機能動作確認を実行

---

## T001: プロジェクト基盤とデータ構造の構築

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

## T002: Windows API統合とエラーハンドリング機能（変更なし）

**機能目標**: SetupAPI（宣言的インストール専用）との統合機能を実装し、API エラーを適切に日本語メッセージに変換できる機能を実現

**実装範囲**:
- SetupAPI の P/Invoke ラッパー実装
  - SetupOpenInfFileW, SetupInstallFromInfSectionW, SetupInstallServicesFromInfSectionW, SetupCloseInfFile
  - GetLastError
  - 関連構造体と定数の定義
- Windows API エラーハンドラーの実装
  - エラーコードの解析と処理
  - 日本語メッセージへの変換
  - ApiErrorInfo オブジェクトの生成
- リソースファイルによるローカライゼーション
  - 主要な SetupAPI エラーコードの日本語メッセージ
  - エラーメッセージリソースファイル

**成果物**:
```
sample_win_devicedriver_inf_install/
  ├── Native/
  │   └── SetupApi.cs（宣言的インストール API のみ）
  ├── Services/
  │   ├── WindowsApiErrorHandler.cs
  │   └── LocalizationService.cs
  └── Resources/
      ├── ErrorMessages.ja.resx
      └── Messages.ja.resx
```

**機能テスト**:
- SetupAPI 関数（宣言的インストール用）が正しく呼び出せる
- 意図的にエラーを発生させて日本語メッセージが取得できる
- 未知のエラーコードでも適切なフォールバック処理が動作する
- リソースファイルからメッセージが正しく取得できる

**完了条件**: Windows API エラーが発生した際に、適切な日本語メッセージが生成され、技術詳細も含めて記録される

---

## T003: ドライバインストール機能の実装 [P]（更新）

**機能目標**: INF ファイルに従った包括的な宣言的インストール機能を実装し、内部的な API 使い分けを自動化

**実装範囲（更新）**:
- IDriverInstallationService の実装（統一されたインストール専用）
  - InstallFromInfAsync(infPath, sectionName="DefaultInstall", flags=0)
  - 内部での SetupInstallFromInfSectionW と SetupInstallServicesFromInfSectionW の適切な使い分け
  - .Services セクションの自動検出と条件付き実行
  - インストールセッション管理
  - 進捗追跡とステータス更新
  - エラーハンドリング
  - キャンセレーション対応

**実装詳細**:
- INF セクション解析による .Services セクションの存在確認
- SetupInstallFromInfSectionW による指定セクションの適用
- SetupInstallServicesFromInfSectionW による Services セクションの条件付き適用
- API 呼び出しエラーの統合処理
- 処理順序の最適化とロールバック処理

**成果物（更新）**:
```
sample_win_devicedriver_inf_install/Services/
  └── DriverInstallationService.cs（統一されたインストール専用）
```

**機能テスト（更新）**:
- 有効な INF ファイルで包括的なインストールが実行される
- .Services セクションが自動検出・適用される
- セクションが存在しない場合に適切に処理される
- インストール進捗が適切に追跡される
- エラー発生時に適切な ApiErrorInfo が生成される
- インストールセッションが正しく管理される

**完了条件**: INF ファイルパスを指定して `InstallFromInfAsync` を呼び出すと、INF に記述されたすべての関連処理が自動実行され、結果が InstallationResult として返される

---

## T004: ログ機能の実装 [P]（変更なし）

**機能目標**: インストール処理の詳細なログ記録機能を実装し、トラブルシューティングに必要な情報を提供

**実装範囲**:
- IInstallationLogger の実装
  - Microsoft.Extensions.Logging を使用したログ実装
  - 構造化ログ機能
  - ファイル出力とコンソール出力の分離
  - 相関ID による処理追跡
  - 日本語UI用ログと英語技術ログの分離
- ログローテーションとエクスポート機能
  - ファイルサイズベースのローテーション
  - JSON, XML, CSV 形式でのエクスポート
  - ログレベルの動的変更

**成果物**:
```
sample_win_devicedriver_inf_install/Services/
  └── InstallationLogger.cs
```

**機能テスト**:
- ログが適切なレベルで記録される
- 相関ID によるセッション追跡が動作する
- ファイルローテーションが正しく実行される
- 構造化ログが適切な形式で出力される
- SetupAPI 呼び出し結果が詳細に記録される

**完了条件**: インストール処理中のすべての操作がログに記録され、セッション単位での追跡とトラブルシューティングが可能になる

---

## T005: CLI インターフェースの実装（更新）

**機能目標**: コマンドラインから統一された宣言的インストール機能を使用できるユーザーインターフェースを実装

**実装範囲（更新）**:
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
  - 進捗表示メッセージ
  - 成功/失敗メッセージ
  - ヘルプテキスト

**成果物（更新）**:
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

**機能テスト（更新）**:
- `sample_win_devicedriver_inf_install.exe --install --inf driver.inf` でインストールが実行される
- インストール中にセクション適用とサービス登録が自動実行される
- エラー時に適切な日本語メッセージが表示される
- `--help` で使用方法が日本語で表示される
- 終了コードが正しく設定される
- GenericHostが正しく動作する

**完了条件**: quickstart.md のコマンド例がすべて実行でき、サイレントモードで包括的な宣言的インストールが完了する

---

## T006: 基本機能統合テストの実装（更新）

**機能目標**: quickstart.md のシナリオを自動テストとして実装し、エンドツーエンドでの機能動作を検証

**実装範囲（更新）**:
- 統一されたインストールシナリオテスト
  - INF ファイル指定による包括的インストールテスト
  - セクション適用とサービス登録の自動実行検証
  - インストール成功の検証
  - ログファイル生成の確認
- エラーハンドリングシナリオテスト
  - 不正な INF ファイルでのテスト
  - 権限不足エラーのシミュレーション
  - エラーメッセージの日本語表示確認
  - 存在しないセクション指定時の処理
- インストール検証シナリオテスト
  - INF に従ったインストール後の状態確認テスト
  - 内部 API 使い分けの正常動作確認

**成果物（更新）**:
```
tests/sample_win_devicedriver_inf_install.IntegrationTests/
  ├── Scenarios/
  │   ├── UnifiedInstallationScenarioTests.cs
  │   ├── ErrorHandlingScenarioTests.cs
  │   └── VerificationScenarioTests.cs
  ├── TestData/
  │   └── SampleDrivers/
  └── Fixtures/
      └── TestEnvironmentFixture.cs
```

**機能テスト（更新）**:
- quickstart.md の統一されたインストールシナリオが自動実行される
- 内部的な API 使い分けが透過的に動作することを確認
- エラー条件でも適切に処理される
- すべてのテストが管理者権限環境で実行される

**完了条件**: quickstart.md に記載されたすべてのシナリオが自動テストとして実行でき、期待される結果が得られる

---

## T007: パフォーマンス最適化と品質向上 [P]（変更なし）

**機能目標**: パフォーマンス要件を満たし、本番環境で使用できる品質レベルを実現

**実装範囲**:
- パフォーマンステストの実装
  - インストール時間の測定（30秒以内の確認）
  - メモリ使用量の監視
  - 大量ファイル処理のテスト
  - タイムアウト処理の検証
- メモリ使用量最適化
  - SetupAPI リソースの確実な解放
  - ログエントリのバッファリング最適化
  - 大きなオブジェクトの適切な Dispose
- ユニットテストの拡充
  - 各サービスクラスの詳細テスト
  - エッジケースのテスト追加
  - 例外処理のテスト
  - モック検証の強化

**成果物**:
```
tests/sample_win_devicedriver_inf_install.PerformanceTests/
  └── PerformanceTests.cs
tests/sample_win_devicedriver_inf_install.Tests/
  ├── Services/
  │   ├── DriverInstallationServiceTests.cs
  │   ├── InstallationLoggerTests.cs
  │   └── WindowsApiErrorHandlerTests.cs
  └── Models/
      └── DataModelTests.cs
```

**機能テスト**:
- パフォーマンス要件（30秒以内等）が満たされる
- メモリリークが検出されない
- コードカバレッジが 85% 以上になる
- 長時間実行でも安定動作する

**完了条件**: quickstart.md のパフォーマンス検証がすべて通り、本番環境での使用に耐える品質が確保される

---

## T008: HLK互換性検証と最終ドキュメント（変更なし）

**機能目標**: HLK 互換性要件を満たし、ユーザーが自立して使用できるドキュメントを完成

**実装範囲**:
- HLK 互換性検証
  - HLK テスト環境での動作確認
  - Windows 署名要件の確認
  - セキュリティベストプラクティスの確認
- CLI ヘルプとドキュメントの充実
  - `--help` オプションの詳細実装
  - 使用例の追加
  - エラーメッセージの改善
- README.md の更新
  - インストール手順
  - 使用方法
  - トラブルシューティング
  - HLK テスト対応

**成果物**:
```
docs/
  ├── HlkCompatibility.md
  └── TroubleshootingGuide.md
README.md (更新)
```

**機能テスト**:
- HLK テストで互換性が確認される
- ユーザーが README だけで使用開始できる
- すべてのエラーメッセージが分かりやすい
- ヘルプが十分詳細で実用的

**完了条件**: HLK 互換性が確認され、ユーザーが自立してアプリケーションを使用できるドキュメントが完成する

---

## タスク実行の流れ

### 段階的実行
```bash
# 段階 1: 基盤構築
/tasks T001

# 段階 2: 並列実行可能な機能実装
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

### 各段階での検証
```bash
# 基盤検証
dotnet build --configuration Release

# 機能検証  
dotnet test tests/sample_win_devicedriver_inf_install.Tests/ --configuration Release

# 統合検証
dotnet test tests/sample_win_devicedriver_inf_install.IntegrationTests/ --configuration Release

# 性能検証
dotnet test tests/sample_win_devicedriver_inf_install.PerformanceTests/ --configuration Release
```

## 完了条件（更新）

すべてのタスクが完了し、以下の機能要件が満たされた時点で実装完了とする：

1. ✓ **統一されたインストール**: CLI から INF に従った包括的なインストールができる
2. ✓ **エラーハンドリング**: SetupAPI エラーが適切な日本語メッセージで表示される  
3. ✓ **ログ機能**: インストール処理の詳細がログに記録され、トラブルシューティングができる
4. ✓ **パフォーマンス**: quickstart.md の性能要件が満たされる
5. ✓ **HLK互換性**: HLK テストで互換性が確認される
6. ✓ **ドキュメント**: ユーザーが自立して使用できるドキュメントが完成する

---
*タスク数: 8タスク | 推定実装期間: 6-10営業日（統一されたインストール処理により簡素化） | 機能完結性重視設計*