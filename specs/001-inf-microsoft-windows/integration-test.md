# 統合テスト計画

## 概要

本ドキュメントは、Windows デバイスドライバ INF インストーラの統合テスト計画を定義します。統合テストは、実際のアプリケーション実行パスを通じて機能の動作を検証しますが、CI環境での実行を考慮し、実OS環境を変更せずにスタブを使用してテストを実行します。

## テスト戦略

### テストレベル分離

1. **UnitTest**
   - 個別クラス・メソッドの動作検証
   - 外部依存なし、完全密閉型
   - 高速実行、CI で毎回実行

2. **IntegrationTest**（本計画の対象）
   - アプリケーション全体の統合動作検証
   - ISetupApiWrapper 等のスタブ使用
   - CI で安全に実行可能
   - 実OS環境を変更しない

3. **OS環境テスト**
   - 実際のOS環境での動作検証
   - 専用テスト環境でのみ実行
   - docs/os-integration-test-spec.md に準拠

## テスト対象シナリオ

### 主要ユーザーストーリー検証

#### シナリオ1: 正常なINFインストール実行
**目的**: spec.mdの受け入れシナリオ1を検証  
**テストケース**: UnifiedInstallationScenarioTests.InstallWithValidInfFile  
**実行条件**:
- 有効なINFファイルが存在する
- DefaultInstallセクションを指定してインストール実行

**検証ポイント**:
- INFファイルの正常な読み込み
- ファイルキュー操作の実行（SetupInstallFilesFromInfSection）
- レジストリ操作の実行（SetupInstallFromInfSectionW）
- サービス登録の実行（SetupInstallServicesFromInfSectionW）
- 正常完了メッセージの日本語表示
- JSON出力形式での結果構造化

**スタブ動作**:
- SetupApiStub.OpenInfFile: ハンドル返却
- SetupApiStub.InstallFilesFromInfSection: 成功返却
- SetupApiStub.InstallFromInfSection: 成功返却
- SetupApiStub.InstallServicesFromInfSection: 成功返却

#### シナリオ2: 無効・破損INFファイルのエラーハンドリング
**目的**: spec.mdの受け入れシナリオ2を検証  
**テストケース**: ErrorHandlingScenarioTests.HandleInvalidInfFile  
**実行条件**:
- 破損または無効なINFファイルを指定してインストール実行

**検証ポイント**:
- INFファイル解析エラーの検出
- 明確な日本語エラーメッセージの表示
- 適切な終了コード（非0）の返却
- 詳細エラー情報のログ記録

**スタブ動作**:
- SetupApiStub.OpenInfFile: INVALID_HANDLE_VALUE返却
- WindowsApiErrorHandler: 日本語エラーメッセージ生成

#### シナリオ3: 既存セクション再適用の処理
**目的**: spec.mdの受け入れシナリオ4を検証  
**テストケース**: VerificationScenarioTests.HandleExistingInstallation  
**実行条件**:
- 同一セクションが既に適用済みの状態でインストール実行

**検証ポイント**:
- 既存状態の検出
- 適切な選択肢の提供（更新、再適用、スキップ）
- ユーザー選択に基づく処理実行
- セッション管理と相関ID追跡

**スタブ動作**:
- SetupApiStub.InstallFromInfSection: 既存状態を示すエラーコード返却
- ユーザー選択のシミュレーション

#### シナリオ4: 完了後の状況表示と検証ガイダンス
**目的**: spec.mdの受け入れシナリオ5を検証  
**テストケース**: VerificationScenarioTests.DisplayCompletionGuidance  
**実行条件**:
- インストール処理完了後

**検証ポイント**:
- インストール状況の表示
- 検証のためのガイダンス提供
- 再起動要求の検出と通知
- インストール詳細レポートの出力

## エッジケース検証

### ケース1: 不足ファイル参照エラー
**テストケース**: ErrorHandlingScenarioTests.HandleMissingReferencedFiles  
**検証ポイント**:
- CopyFiles セクションで参照されるファイルが存在しない場合のエラー処理
- ファイルキュー操作でのエラー検出
- 具体的な不足ファイル名の報告

### ケース2: システム再起動要求の処理
**テストケース**: VerificationScenarioTests.HandleRebootRequirement  
**検証ポイント**:
- SetupAPI が再起動要求を返した場合の処理
- 再起動要求フラグの適切な検出
- ユーザーへの再起動通知メッセージ

### ケース3: 署名検証エラーの処理
**テストケース**: ErrorHandlingScenarioTests.HandleDriverSignatureError  
**検証ポイント**:
- ドライバ署名ポリシー違反時のエラー処理
- テストモード推奨メッセージの表示
- セキュリティ関連の適切な警告

### ケース4: 同時実行制御
**テストケース**: VerificationScenarioTests.HandleConcurrentInstallation  
**検証ポイント**:
- 複数インスタンスの同時実行制御
- ファイルロックやリソース競合の適切な処理
- エラー発生時の適切なクリーンアップ

## コマンドラインオプション検証

### 基本オプション組み合わせテスト
**テストケース**: QuickstartScenarioTests.ValidateCommandLineOptions

#### パターン1: 最小構成
```cmd
--install --inf sample.inf
```
- デフォルトセクション（DefaultInstall）の使用
- 標準出力での結果表示

#### パターン2: 詳細モード
```cmd
--install --inf sample.inf --section CustomInstall --verbose
```
- カスタムセクションの指定
- 詳細ログ出力の有効化

#### パターン3: ドライラン + JSON出力
```cmd
--install --inf sample.inf --dry-run --output json
```
- 事前チェックのみ実行
- 構造化された結果出力

#### パターン4: ヘルプ表示
```cmd
--help
```
- ヘルプメッセージの日本語表示
- 全オプションの説明表示

### 異常系オプション検証
**テストケース**: ErrorHandlingScenarioTests.ValidateInvalidOptions

- 必須オプション未指定（--inf 未指定）
- 存在しないファイルパス指定
- 不正な出力形式指定
- 矛盾するオプション組み合わせ

## 出力形式検証

### 標準テキスト出力
**テストケース**: QuickstartScenarioTests.ValidateTextOutput
- 日本語メッセージの適切な表示
- 進行状況インジケータの動作
- エラー時の詳細情報表示

### JSON出力
**テストケース**: QuickstartScenarioTests.ValidateJsonOutput
- 有効なJSON形式での出力
- 必要な情報フィールドの含有
- エラー時の構造化されたエラー情報

### ログ出力
**テストケース**: VerificationScenarioTests.ValidateLogOutput
- 詳細モードでの適切なログレベル出力
- 技術情報の英語ログ出力
- ユーザー向け情報の日本語出力の分離

## パフォーマンス要件検証

### 実行時間制限
**テストケース**: QuickstartScenarioTests.ValidatePerformanceRequirements
- 基本的なINFインストールが30秒以内で完了
- 大容量ドライバパッケージでの合理的な実行時間
- ドライランモードでの高速実行（5秒以内）

### リソース使用量
**テストケース**: VerificationScenarioTests.ValidateResourceUsage
- メモリ使用量の合理的な範囲内維持
- ファイルハンドルの適切な管理
- 一時ファイルのクリーンアップ

## ローカライゼーション検証

### 日本語メッセージ表示
**テストケース**: QuickstartScenarioTests.ValidateJapaneseLocalization
- 全ユーザー向けメッセージの日本語表示
- 文字エンコーディングの適切な処理
- 日本語フォント非対応環境での代替表示

### 技術ログの英語出力
**テストケース**: VerificationScenarioTests.ValidateTechnicalLogEnglish
- 開発者向けログの英語出力
- API呼び出し詳細の英語記録
- 国際的な技術サポートでの利用可能性

## テスト実行環境

### CI環境での実行
- **前提条件**: 管理者権限不要
- **依存関係**: スタブによる外部依存の完全置換
- **実行時間**: 全テスト5分以内で完了
- **並列実行**: 可能（リソース競合なし）

### 開発環境での実行
- **前提条件**: .NET 10 SDK
- **IDE統合**: Visual Studio Test Explorer対応
- **デバッグ**: ブレークポイント設定可能
- **テストカバレッジ**: コードカバレッジ計測対応

## 成功基準

### 機能要件充足
- [ ] spec.mdの全受け入れシナリオがテストで検証済み
- [ ] 全エッジケースで適切なエラーハンドリングが動作
- [ ] 全コマンドラインオプションが仕様通りに動作

### 品質要件充足
- [ ] 全テストケースがCI環境で安定実行
- [ ] テストカバレッジ90%以上を達成
- [ ] パフォーマンス要件を満たすことを確認

### 保守性要件充足
- [ ] テストコードが理解しやすく、保守可能
- [ ] 新機能追加時のテスト拡張が容易
- [ ] 実環境テストとの明確な役割分離

## 制限事項

### CI環境の制約
- 実OS環境の変更は行わない
- 管理者権限は使用しない
- ネットワーク通信は実施しない
- 永続化されるファイルは作成しない

### スタブの制約
- SetupAPI の全機能を完全再現するものではない
- 特定のエラーパターンのみをシミュレート
- パフォーマンス特性は実API と異なる

### テスト範囲の制約
- ハードウェア固有の動作は検証対象外
- 特定のWindowsバージョン固有の問題は限定的
- 実際のドライバ動作は検証対象外（OS環境テストで対応）