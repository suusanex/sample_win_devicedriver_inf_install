# 統合テストガイド

## 概要
このプロジェクトには、Windows デバイスドライバ INF インストーラの統合テストが含まれており、quickstart.mdに記載されたシナリオをテストコードで検証できます。

## 必須ルール（spec-kit 共通）
- CI で実行される統合テストは、実OS環境（レジストリ、SetupAPI、サービス、ドライバ、デバイス等）を変更しない。
- OS依存APIは必ずインターフェースで抽象化し、統合テストではスタブを注入する。
  - 本プロジェクトでは ISetupApiWrapper を使用し、IntegrationTests では SetupApiStub をDIで注入する。
- 実OS変更を伴う検証は docs/os-integration-test-spec.md に従い、専用環境でのみ実施する（CIでは実行しない）。

## テストカテゴリ

### 1. UnifiedInstallationScenarioTests
- 統一されたインストール機能の包括的テスト
- INFファイルに従った包括的なインストール検証
- セクション適用とサービス登録の自動実行確認

### 2. ErrorHandlingScenarioTests
- 様々なエラー条件での適切な処理確認
- 日本語エラーメッセージの検証
- 権限不足やファイル不正などの異常系テスト

### 3. VerificationScenarioTests
- インストール後の状態確認
- 内部API使い分けの正常動作確認
- セッション管理と相関ID追跡の検証

### 4. QuickstartScenarioTests
- quickstart.mdのシナリオを直接テスト
- パフォーマンス要件の検証（30秒以内完了）
- サイレント実行とログ出力の確認

## 実行前提条件

### 必要な権限
```cmd
# 管理者権限は不要（OS環境テストを除く）
```

### 環境要件
- Windows 10 または Windows 11
- .NET 10 SDK
- Visual Studio または dotnet CLI

## テスト実行方法

### Visual Studio での実行
1. Visual Studio を起動
2. テストエクスプローラーでテストを実行
3. 各テストクラスまたは個別テストメソッドを実行可能

### コマンドラインでの実行
```cmd
# すべての統合テストを実行
dotnet test sample_win_devicedriver_inf_install.IntegrationTests --configuration Release

# 特定のテストクラスのみ実行
dotnet test sample_win_devicedriver_inf_install.IntegrationTests --filter "FullyQualifiedName~UnifiedInstallationScenarioTests" --configuration Release

# quickstart.mdシナリオのみ実行  
dotnet test sample_win_devicedriver_inf_install.IntegrationTests --filter "FullyQualifiedName~QuickstartScenarioTests" --configuration Release

# 詳細ログ付きで実行
dotnet test sample_win_devicedriver_inf_install.IntegrationTests --configuration Release --logger "console;verbosity=detailed"
```

## テストデータ

### 自動生成されるテストファイル
テスト実行時に一時ディレクトリに以下のファイルが作成されます：
- `valid_driver.inf` - 正常なINFファイル
- `invalid_format.inf` - 不正な形式のINFファイル  
- `services_driver.inf` - Servicesセクション付きINFファイル
- その他、各テストシナリオに応じたINFファイル

### 組み込みテストデータ
`TestData/SampleDrivers/` に以下のファイルが含まれています：
- `sample_driver.inf` - サンプルドライバ（Servicesセクション付き）
- `simple_driver.inf` - シンプルなドライバ
- `invalid_driver.inf` - 意図的に無効なドライバ

## テスト結果の確認

### 成功したテストの確認事項
- インストール処理が適切に実行された
- ログエントリが期待される内容で記録された
- エラーハンドリングが正しく動作した
- パフォーマンス要件が満たされた

### 失敗した場合のトラブルシューティング
1. **依存注入の確認**: SetupApiStub がDIに登録されているか
2. **ファイルパス**: テストINFの生成・パスが正しいか
3. **CIの制限**: セキュリティポリシーによる制限がないか

## CI/CD 環境での実行

### GitHub Actions 等での設定例
```yaml
- name: Run Integration Tests
  run: dotnet test sample_win_devicedriver_inf_install.IntegrationTests --configuration Release --no-build --verbosity normal
```

### 制限事項
- CI/CD環境では管理者権限やSetupAPI機能に依存しないよう設計しているため、OSの状態変更は行われない

## テストの拡張

新しいテストシナリオを追加する場合：
1. 適切なテストクラス（Scenarios フォルダ内）に新しいテストメソッドを追加
2. `TestEnvironmentFixture` を使用してテスト環境を設定
3. FluentAssertions を使用して検証条件を記述
4. 必要に応じて新しいINFファイルテンプレートを作成

## 関連ドキュメント
- `docs/test-strategy.md` - テスト戦略とOS環境影響の分離（必須ルール）
- `docs/os-integration-test-spec.md` - OS環境テスト仕様（専用環境のみ）
- `docs/test-environment-setup.md` - テスト環境構築ガイド
- `specs/001-inf-microsoft-windows/quickstart.md` - 実際の使用シナリオ