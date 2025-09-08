# クイックスタートガイド: Windows デバイスドライバ INF インストーラ

## 概要
このクイックスタートガイドは、Windows デバイスドライバ INF インストーラコンソールアプリケーションの基本的な使用方法とテスト手順を説明します。

## 前提条件

### システム要件
- Windows 10 または Windows 11
- .NET 10 ランタイム
- 管理者権限（ドライバインストールのため）
- テスト用の有効な INF ファイル

### テスト環境セットアップ
1. **管理者権限でコマンドプロンプトを開く**
   ```cmd
   # Windows + X → "Windows PowerShell (管理者)" を選択
   ```

2. **アプリケーションビルド**
   ```cmd
   dotnet build --configuration Release
   ```

3. **テストドライバパッケージの準備**
   - サンプル INF ファイルをテストフォルダに配置
   - 関連ドライバファイル（.sys, .dll）を同じフォルダに配置

## 基本使用方法

### 2. ドライバインストール（サイレントモード）
```cmd
sample_win_devicedriver_inf_install.exe --install "C:\DriverTest\sample.inf"
```

**期待結果**:
- INF ファイル検証
- ユーザー対話なしでインストール実行
- 成功/失敗の日本語メッセージ
- ログファイルへの詳細記録
- 終了コードによる結果報告

### 3. インストール状況確認
```cmd
sample_win_devicedriver_inf_install.exe --status "C:\DriverTest\sample.inf"
```

**期待結果**:
- ドライバのインストール状況表示
- デバイスマネージャーでの確認方法案内
- 関連デバイスの一覧表示

## テストシナリオ

### シナリオ 1: 有効な INF ファイルでのインストール

**前提条件**:
- テスト用ソフトウェアデバイスドライバ INF ファイル
- 管理者権限でアプリケーション実行

**実行手順**:
1. `sample_win_devicedriver_inf_install.exe --install test-driver.inf`
2. インストール完了を確認

**期待結果**:
- インストール成功メッセージ（日本語）
- デバイスマネージャーにデバイス表示
- イベントログにインストール記録

### シナリオ 2: インストール後の検証

**前提条件**:
- ドライバインストール完了後

**実行手順**:
1. `sample_win_devicedriver_inf_install.exe --verify test-driver.inf`
2. デバイスマネージャーで確認

**期待結果**:
- インストール済みドライバの確認
- デバイス動作状態の表示
- 次のステップのガイダンス

## ログとトラブルシューティング

### ログファイル場所
```
# 実行時ログ
%TEMP%\DriverInstaller\Logs\installation-{SessionId}.log

# エラーログ
%TEMP%\DriverInstaller\Logs\error-{Date}.log
```

### よくある問題と解決策

1. **「INF ファイルが見つかりません」エラー**
   - 解決策: ファイルパスの確認、絶対パスの使用

2. **「ハードウェア依存ドライバは対象外です」エラー**
   - 解決策: ソフトウェアデバイス、フィルタドライバのみ使用

3. **Windows API エラー**
   - 解決策: 適切な前提条件（管理者権限、署名、競合なし）の確認

### デバッグオプション

```cmd
# 詳細ログ出力
sample_win_devicedriver_inf_install.exe --install driver.inf --verbose

# 実行前の事前チェックのみ
sample_win_devicedriver_inf_install.exe --dry-run driver.inf

# JSON 形式での結果出力
sample_win_devicedriver_inf_install.exe --install driver.inf --output json
```

## パフォーマンス検証

### インストール時間の測定
```cmd
# PowerShell での時間測定
Measure-Command { .\sample_win_devicedriver_inf_install.exe --install driver.inf }
```

**期待値**:
- 小規模ドライバ: 30秒以内
- 中規模ドライバ: 2分以内
- タイムアウト設定: 5分

### メモリ使用量の確認
```cmd
# プロセス監視（別ウィンドウで実行）
Get-Process sample_win_devicedriver_inf_install | Select-Object WorkingSet,VirtualMemorySize
```

## HLK 互換性テスト準備

### テスト環境設定
1. Windows Hardware Lab Kit のインストール
2. テスト署名モードの有効化
3. テストドライバパッケージの準備

### HLK テスト実行
```cmd
# HLK テスト用のドライバインストール
sample_win_devicedriver_inf_install.exe --install hlk-test-driver.inf --hlk-mode
```

## 次のステップ

### 本番環境での使用
1. 本番署名されたドライバの準備
2. インストールスクリプトの作成
3. エラーハンドリングとロールバック戦略の策定

### 統合開発
1. インストールロジックの他のアプリケーションへの統合
2. カスタムUI の開発
3. バッチインストールシステムの構築

## サポートとリソース

### ドキュメント
- [Windows ドライバ開発ガイド](https://docs.microsoft.com/windows-hardware/drivers/)
- [INF ファイル仕様](https://docs.microsoft.com/windows-hardware/drivers/install/inf-files)
- [HLK テストガイド](https://docs.microsoft.com/windows-hardware/test/hlk/)

### コミュニティ
- Windows Driver Kit コミュニティフォーラム
- GitHub Issues でのバグ報告
- 機能リクエストの提出