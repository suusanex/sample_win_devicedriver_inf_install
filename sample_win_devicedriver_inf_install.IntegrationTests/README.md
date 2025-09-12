# 統合テストガイド

## 概要
このプロジェクトには、Windows デバイスドライバ INF インストーラの統合テストが含まれており、quickstart.mdに記載されたシナリオをテストコードで検証できます。

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
# 管理者権限でのテスト実行を推奨
# 一部のテストは管理者権限なしでも実行可能ですが、SetupAPI関連の機能は制限されます
```

### 環境要件
- Windows 10 または Windows 11
- .NET 10 SDK
- 管理者権限（推奨）
- Visual Studio または dotnet CLI

## テスト実行方法

### Visual Studio での実行
1. Visual Studio を管理者権限で起動
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

### 管理者権限でのテスト実行
```cmd
# PowerShellを管理者権限で起動してから実行
Start-Process powershell -Verb RunAs
cd "C:\YourPath\sample_win_devicedriver_inf_install"
dotnet test sample_win_devicedriver_inf_install.IntegrationTests --configuration Release
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
1. **権限エラー**: 管理者権限で実行しているか確認
2. **ファイルアクセスエラー**: ウイルス対策ソフトの除外設定を確認
3. **SetupAPI エラー**: Windows のテスト署名モードを確認
4. **タイムアウトエラー**: テスト実行環境のパフォーマンスを確認

## CI/CD 環境での実行

### GitHub Actions 等での設定例
```yaml
- name: Run Integration Tests
  run: dotnet test sample_win_devicedriver_inf_install.IntegrationTests --configuration Release --no-build --verbosity normal
  # 注意: CI環境では管理者権限やSetupAPI機能に制限がある場合があります
```

### 制限事項
- CI/CD環境では完全なSetupAPI機能テストに制限がある場合があります
- 一部のテストはWindows実機環境でのみ完全に動作します
- クラウドCI環境では管理者権限が制限される場合があります

## テストの拡張

新しいテストシナリオを追加する場合：
1. 適切なテストクラス（Scenarios フォルダ内）に新しいテストメソッドを追加
2. `TestEnvironmentFixture` を使用してテスト環境を設定
3. FluentAssertions を使用して検証条件を記述
4. 必要に応じて新しいINFファイルテンプレートを作成

## 関連ドキュメント
- `specs/001-inf-microsoft-windows/quickstart.md` - 実際の使用シナリオ
- `specs/001-inf-microsoft-windows/tasks.md` - 実装タスクの詳細
- `README.md` - プロジェクト全体の概要