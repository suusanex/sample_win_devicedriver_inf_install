# クイックスタートガイド: Windows デバイスドライバ INF インストーラ（宣言的インストール専用）

## 概要
このクイックスタートは、INF ファイルに従った宣言的インストール（全デバイス対象フィルタ等）の基本的な使用方法とテスト手順を説明します。PnP デバイス個体へのドライバ適用は対象外です。

## 前提条件

### システム要件
- Windows 10 または Windows 11
- .NET 10 ランタイム
- 管理者権限（ドライバインストールのため）
- テスト用の有効な INF ファイル

### テスト環境セットアップ
1. 管理者権限でコマンドプロンプトを開く
2. アプリケーションビルド
   ```cmd
   dotnet build --configuration Release
   ```
3. テストドライバパッケージの準備（INF と関連ファイルを同一フォルダへ）

## 基本使用方法

### INF ファイルに従ったインストール実行
```cmd
sample_win_devicedriver_inf_install.exe --install --inf "C:\DriverTest\sample.inf" --section DefaultInstall
```

**実行される処理**:
- 指定された INF セクション（例: DefaultInstall）の適用（CopyFiles, AddReg 等）
- 関連する .Services セクション（例: DefaultInstall.Services）の自動検出と適用
- 必要に応じた再起動要求の検出と通知

**期待結果**:
- ユーザー対話なしで実行
- 成功/失敗の日本語メッセージ
- 詳細ログが出力

### 追加オプション
```cmd
# 特定のセクションを指定してインストール
sample_win_devicedriver_inf_install.exe --install --inf driver.inf --section CustomInstall

# 詳細ログ出力
sample_win_devicedriver_inf_install.exe --install --inf driver.inf --verbose

# JSON 形式での結果出力
sample_win_devicedriver_inf_install.exe --install --inf driver.inf --output json

# ドライラン（事前チェックのみ）
sample_win_devicedriver_inf_install.exe --dry-run --inf driver.inf
```

## テストシナリオ

### シナリオ 1: 標準的な INF インストール
1. `--install --inf sample.inf` を実行
2. ログで以下の処理成功を確認：
   - DefaultInstall セクションの適用（CopyFiles, AddReg 等）
   - DefaultInstall.Services セクションの自動検出と適用（存在する場合）
   - インストール完了通知

### シナリオ 2: カスタムセクションでのインストール
1. `--install --inf sample.inf --section MyCustomInstall` を実行
2. ログでカスタムセクションの処理成功を確認

## ログとトラブルシューティング

### ログファイル場所
```
%TEMP%\DriverInstaller\Logs\installation-{SessionId}.log
%TEMP%\DriverInstaller\Logs\error-{Date}.log
```

### よくある問題と解決策
1. 「INF ファイルが見つかりません」
   - 絶対パスで指定、権限を確認
2. 「署名/ポリシー関連で失敗」
   - 署名やポリシー設定、テスト署名モードを確認
3. 「セクションが見つかりません」
   - INF ファイル内のセクション名を確認
4. SetupAPI エラー
   - 管理者権限で再実行、詳細はログ参照

### 内部処理の確認
アプリケーションは自動的に以下を実行します：
- SetupInstallFromInfSectionW による指定セクションの適用
- .Services セクションの存在確認
- SetupInstallServicesFromInfSectionW による Services セクションの適用（存在する場合）
- エラー発生時の適切なロールバック処理

## パフォーマンス検証（目安）
- 小規模: 30秒以内 / 中規模: 2分以内 / タイムアウト: 5分

## HLK 互換性テスト準備（参考）
- Windows Hardware Lab Kit のセットアップ
- テスト署名モードの有効化
- INF/カタログの準備

## 次のステップ
- スクリプト組み込み（CI 等）
- ログの収集/解析フロー整備

## ドキュメント/リソース
- Windows ドライバ開発ガイド
- INF ファイル仕様
- HLK テストガイド