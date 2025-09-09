# 実装タスク: Windows デバイスドライバ INF インストーラ

## タスク概要

このドキュメントは、Windows デバイスドライバ INF インストーラのコンソールアプリケーション実装のための実行可能タスクリストです。**機能単位でのまとまり**を重視し、Plan の機能要件を満たす粒度でタスクを設計しています。

**機能**: INF ファイルを使用した Windows デバイスドライバのサイレントインストール  
**対象**: .NET 10 コンソールアプリケーション  
**技術スタック**: SetupAPI, Microsoft.Extensions.Logging, Microsoft.Extensions.Hosting (GenericHost), リソースベースローカライゼーション

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
- 既存のsample_win_devicedriver_inf_install.slnx及びsample_win_devicedriver_inf_install.csproj（コンソールアプリ部分）を引き継ぐ
- 必要な NuGet パッケージの追加
  - Microsoft.Extensions.Hosting（GenericHost）
  - Microsoft.Extensions.Logging
  - Microsoft.Extensions.DependencyInjection
- ソリューション構造の構築
- コンソールアプリプロジェクトへのGenericHostの導入
  - HostBuilder の設定
  - DI コンテナの設定
  - サービス登録
- データモデルエンティティの完全実装
  - DriverPackage, InstallationSession, DeviceInstance, InstallationResult
  - ApiErrorInfo, LogEntry などの値オブジェクト
  - InstallationStatus, DeviceStatus, LogLevel などの列挙型
- コントラクトインターフェースの定義
  - IDriverInstallationService, IInstallationLogger

**成果物**:
```
sample_win_devicedriver_inf_install.slnx (既存)
sample_win_devicedriver_inf_install/
  ├── sample_win_devicedriver_inf_install.csproj (既存・更新)
  ├── Program.cs (GenericHost対応)
  ├── Models/
  │   ├── DriverPackage.cs
  │   ├── InstallationSession.cs
  │   ├── DeviceInstance.cs
  │   ├── InstallationResult.cs
  │   └── ValueObjects/
  │       ├── ApiErrorInfo.cs
  │       └── LogEntry.cs
  ├── Contracts/
  │   ├── IDriverInstallationService.cs
  │   └── IInstallationLogger.cs
  └── Enums/
      ├── InstallationStatus.cs
      ├── DeviceStatus.cs
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

## T002: Windows API統合とエラーハンドリング機能

**機能目標**: Windows SetupAPI との統合機能を実装し、API エラーを適切に日本語メッセージに変換できる機能を実現

**実装範囲**:
- SetupAPI の P/Invoke ラッパー実装
  - SetupDiCallClassInstaller, SetupCopyOEMInf, GetLastError
  - 関連構造体と定数の定義
- Windows API エラーハンドラーの実装
  - エラーコードの解析と処理
  - 日本語メッセージへの変換
  - ApiErrorInfo オブジェクトの生成
- リソースファイルによるローカライゼーション
  - 主要な Windows API エラーコードの日本語メッセージ
  - エラーメッセージリソースファイル

**成果物**:
```
sample_win_devicedriver_inf_install/
  ├── Native/
  │   └── SetupApi.cs
  ├── Services/
  │   ├── WindowsApiErrorHandler.cs
  │   └── LocalizationService.cs
  └── Resources/
      ├── ErrorMessages.ja.resx
      └── Messages.ja.resx
```

**機能テスト**:
- SetupAPI 関数が正しく呼び出せる
- 意図的にエラーを発生させて日本語メッセージが取得できる
- 未知のエラーコードでも適切なフォールバック処理が動作する
- リソースファイルからメッセージが正しく取得できる

**完了条件**: Windows API エラーが発生した際に、適切な日本語メッセージが生成され、技術詳細も含めて記録される

---

## T003: ドライバインストール機能の実装 [P]

**機能目標**: INF ファイルを指定してドライバをインストールする核となる機能を実装し、インストールセッション管理と進捗追跡を実現

**実装範囲**:
- IDriverInstallationService の実装
  - SetupAPI を使用したドライバインストールロジック
  - インストールセッション管理
  - 進捗追跡とステータス更新
  - エラーハンドリングとリトライロジック
  - キャンセレーション対応
- ドライバ状態確認サービスの実装
  - デバイスマネージャー API による状態確認
  - ハードウェア ID による検索機能
  - インストール済みドライバの詳細情報取得

**成果物**:
```
sample_win_devicedriver_inf_install/Services/
  ├── DriverInstallationService.cs
  └── DriverStatusService.cs
```

**機能テスト**:
- 有効な INF ファイルでドライバインストールが実行される
- インストール進捗が適切に追跡される
- インストール完了後にデバイス状態が確認できる
- エラー発生時に適切な ApiErrorInfo が生成される
- インストールセッションが正しく管理される

**完了条件**: INF ファイルパスを指定して `InstallDriverAsync` を呼び出すと、ドライバがインストールされ、結果が InstallationResult として返される

---

## T004: ログ機能の実装 [P]

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
- Windows API 呼び出し結果が詳細に記録される

**完了条件**: インストール処理中のすべての操作がログに記録され、セッション単位での追跡とトラブルシューティングが可能になる

---

## T005: CLI インターフェースの実装

**機能目標**: コマンドラインからドライバインストール機能を使用できるユーザーインターフェースを実装

**実装範囲**:
- CLI パーサーの実装
  - `--install <INFファイルパス>` オプション
  - `--status <INFファイルパス>` オプション
  - `--verbose` デバッグオプション
  - `--output json` 出力形式オプション
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

**成果物**:
```
sample_win_devicedriver_inf_install/
  ├── CliParser.cs
  ├── Program.cs (GenericHost対応)
  ├── Services/
  │   └── CommandExecutorService.cs
  └── CommandHandlers/
      ├── InstallCommand.cs
      ├── StatusCommand.cs
      └── HelpCommand.cs
```

**機能テスト**:
- `sample_win_devicedriver_inf_install.exe --install driver.inf` でインストールが実行される
- `sample_win_devicedriver_inf_install.exe --status driver.inf` で状態確認ができる
- エラー時に適切な日本語メッセージが表示される
- `--help` で使用方法が日本語で表示される
- 終了コードが正しく設定される
- GenericHostが正しく動作する

**完了条件**: quickstart.md のコマンド例がすべて実行でき、サイレントモードでドライバインストールが完了する

---

## T006: 基本機能統合テストの実装

**機能目標**: quickstart.md のシナリオを自動テストとして実装し、エンドツーエンドでの機能動作を検証

**実装範囲**:
- 基本インストールシナリオテスト
  - テスト用 INF ファイルでのインストール
  - インストール成功の検証
  - ログファイル生成の確認
  - デバイス状態の確認
- エラーハンドリングシナリオテスト
  - 不正な INF ファイルでのテスト
  - 権限不足エラーのシミュレーション
  - エラーメッセージの日本語表示確認
- インストール検証シナリオテスト
  - インストール後の状態確認テスト
  - デバイス動作状態の検証

**成果物**:
```
tests/sample_win_devicedriver_inf_install.IntegrationTests/
  ├── Scenarios/
  │   ├── BasicInstallationScenarioTests.cs
  │   ├── ErrorHandlingScenarioTests.cs
  │   └── VerificationScenarioTests.cs
  ├── TestData/
  │   └── SampleDrivers/
  └── Fixtures/
      └── TestEnvironmentFixture.cs
```

**機能テスト**:
- quickstart.md のシナリオ 1 が自動実行される
- quickstart.md のシナリオ 2 が自動実行される
- エラー条件でも適切に処理される
- すべてのテストが管理者権限環境で実行される

**完了条件**: quickstart.md に記載されたすべてのシナリオが自動テストとして実行でき、期待される結果が得られる

---

## T007: パフォーマンス最適化と品質向上 [P]

**機能目標**: パフォーマンス要件を満たし、本番環境で使用できる品質レベルを実現

**実装範囲**:
- パフォーマンステストの実装
  - インストール時間の測定（30秒以内の確認）
  - メモリ使用量の監視
  - 大量ファイル処理のテスト
  - タイムアウト処理の検証
- メモリ使用量最適化
  - Windows API リソースの確実な解放
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

## T008: HLK互換性検証と最終ドキュメント

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

## 完了条件

すべてのタスクが完了し、以下の機能要件が満たされた時点で実装完了とする：

1. ✓ **基本機能**: CLI からINFファイルを指定してドライバインストールができる
2. ✓ **エラーハンドリング**: Windows API エラーが適切な日本語メッセージで表示される  
3. ✓ **ログ機能**: インストール処理の詳細がログに記録され、トラブルシューティングができる
4. ✓ **状態確認**: インストール済みドライバの状態確認ができる
5. ✓ **パフォーマンス**: quickstart.md の性能要件が満たされる
6. ✓ **HLK互換性**: HLK テストで互換性が確認される
7. ✓ **ドキュメント**: ユーザーが自立して使用できるドキュメントが完成する

---
*タスク数: 8タスク | 推定実装期間: 8-12営業日 | 機能完結性重視設計*