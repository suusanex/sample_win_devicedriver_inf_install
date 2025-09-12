# テスト環境構築ガイド

## 概要
このドキュメントは、ドライバインストーラーのOS環境統合テスト用の専用テスト環境を構築するための手順を説明します。

## 環境構築オプション

### オプション1: Hyper-V仮想マシン（推奨）

#### 必要なリソース
- Windows 10/11 Pro 以上（Hyper-V機能有効）
- 最低8GB RAM（仮想マシンに4GB割り当て推奨）
- 60GB以上の空きディスク容量
- 管理者権限アカウント

#### 構築手順
1. **Hyper-Vの有効化**
   ```powershell
   # PowerShell（管理者権限）で実行
   Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All
   ```

2. **仮想マシンの作成**
   ```powershell
   # 仮想マシン作成スクリプト例
   New-VM -Name "DriverTestVM" -MemoryStartupBytes 4GB -NewVHDPath "C:\VMs\DriverTestVM.vhdx" -NewVHDSizeBytes 60GB -Generation 2
   ```

3. **Windows 10/11のインストール**
   - ISOファイルをマウント
   - 標準的なWindowsセットアップを実行
   - テスト専用アカウントの作成（例：TestUser）

4. **テスト環境の初期設定**
   ```cmd
   # テスト署名モードの有効化
   bcdedit /set testsigning on
   
   # UACレベルの調整（テスト用）
   reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" /v ConsentPromptBehaviorAdmin /t REG_DWORD /d 0 /f
   ```

### オプション2: VMware Workstation Pro

#### 必要なリソース
- VMware Workstation Pro ライセンス
- 同等のハードウェア要件

#### 構築手順
1. 新規仮想マシンの作成
2. Windows 10/11のインストール
3. VMware Toolsのインストール
4. スナップショット機能の活用設定

### オプション3: 物理テストマシン

#### 推奨構成
- 専用のテスト用PC（古いマシンでも可）
- Windows 10/11 Professional
- SSD推奨（復旧速度向上）
- ネットワーク分離（ドメイン非参加推奨）

## 共通初期設定

### 1. .NET 10 Runtimeのインストール
```powershell
# Wingetを使用した場合
winget install Microsoft.DotNet.Runtime.10

# または手動でMicrosoftサイトからダウンロード
```

### 2. 開発ツールのインストール
```powershell
# Visual Studio Build Tools（必要に応じて）
winget install Microsoft.VisualStudio.2022.BuildTools

# Windows SDK（ドライバ開発用）
winget install Microsoft.WindowsSDK.10.0.22621
```

### 3. テスト用証明書の設定
```cmd
# テスト用ルート証明書の作成
makecert -n "CN=Test Root CA" -r -sv TestRootCA.pvk TestRootCA.cer
certmgr -add TestRootCA.cer -s -r localMachine root

# コードサイニング証明書の作成
makecert -n "CN=Test Code Signing" -iv TestRootCA.pvk -ic TestRootCA.cer -sv TestCodeSign.pvk TestCodeSign.cer
pvk2pfx -pvk TestCodeSign.pvk -spc TestCodeSign.cer -pfx TestCodeSign.pfx
```

### 4. システム復元ポイントの設定
```powershell
# システム復元の有効化
Enable-ComputerRestore -Drive "C:\"

# 復元ポイントの作成
Checkpoint-Computer -Description "DriverTest-Baseline" -RestorePointType "MODIFY_SETTINGS"
```

### 5. テストディレクトリの準備
```cmd
# テスト用ディレクトリ構造の作成
mkdir C:\DriverTests
mkdir C:\DriverTests\INFFiles
mkdir C:\DriverTests\Drivers
mkdir C:\DriverTests\Logs
mkdir C:\DriverTests\Backup
```

## テストデータの準備

### サンプルINFファイルの作成

#### 1. basic_test.inf（基本テスト用）
```ini
[Version]
Signature="$WINDOWS NT$"
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
CopyFiles=TestFiles

[TestFiles]
test.txt

[SourceDisksNames]
1 = "Test Installation Disk",,,""

[SourceDisksFiles]
test.txt = 1,,

[DestinationDirs]
TestFiles = 10  ; %SystemRoot%
```

#### 2. registry_test.inf（レジストリテスト用）
```ini
[Version]
Signature="$WINDOWS NT$"
Class=Sample
ClassGUID={12345678-1234-1234-1234-123456789012}
Provider=TestProvider
DriverVer=01/01/2024,1.0.0.0

[DefaultInstall]
AddReg=TestRegistry

[TestRegistry]
HKLM,SOFTWARE\TestDriver,TestValue,0x00000000,"TestData"
HKLM,SOFTWARE\TestDriver,TestDWORD,0x00010001,12345
```

### テスト用ドライバファイルの準備

```cmd
# 空のテストファイル作成（実際のテストではダミーファイル）
echo. > C:\DriverTests\Drivers\test.txt
echo. > C:\DriverTests\Drivers\dummy.sys
```

## 自動バックアップスクリプト

### レジストリバックアップスクリプト（backup_registry.bat）
```batch
@echo off
set BACKUP_DIR=C:\DriverTests\Backup
set TIMESTAMP=%date:~-4,4%%date:~-10,2%%date:~-7,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%

echo Creating registry backup...
reg export HKLM\SOFTWARE "%BACKUP_DIR%\HKLM_SOFTWARE_%TIMESTAMP%.reg"
reg export HKLM\SYSTEM "%BACKUP_DIR%\HKLM_SYSTEM_%TIMESTAMP%.reg"
echo Registry backup completed: %BACKUP_DIR%
```

### システム状態確認スクリプト（check_system.ps1）
```powershell
param(
    [string]$OutputFile = "C:\DriverTests\Logs\system_state.txt"
)

# システム情報の収集
$systemInfo = @()
$systemInfo += "=== System Check Report ==="
$systemInfo += "Timestamp: $(Get-Date)"
$systemInfo += ""

# インストールされたドライバの一覧
$systemInfo += "=== Installed Drivers ==="
Get-WmiObject Win32_PnPSignedDriver | Where-Object {$_.DeviceName -like "*Test*"} | 
    ForEach-Object { $systemInfo += "$($_.DeviceName) - $($_.DriverVersion)" }
$systemInfo += ""

# レジストリキーの確認
$systemInfo += "=== Test Registry Keys ==="
if (Test-Path "HKLM:\SOFTWARE\TestDriver") {
    Get-ItemProperty "HKLM:\SOFTWARE\TestDriver" | 
        ForEach-Object { $systemInfo += "$($_.PSChildName)" }
} else {
    $systemInfo += "No test registry keys found"
}
$systemInfo += ""

# サービスの確認
$systemInfo += "=== Test Services ==="
Get-Service | Where-Object {$_.Name -like "*Test*"} | 
    ForEach-Object { $systemInfo += "$($_.Name) - $($_.Status)" }

# ファイルに出力
$systemInfo | Out-File -FilePath $OutputFile -Encoding UTF8
Write-Host "System state saved to: $OutputFile"
```

## テスト実行環境の検証

### 検証チェックリスト
- [ ] テスト署名モードが有効（`bcdedit /enum` で確認）
- [ ] 管理者権限でのコマンド実行が可能
- [ ] .NET 10 Runtimeがインストール済み
- [ ] システム復元ポイントが作成済み
- [ ] テスト用ディレクトリが準備済み
- [ ] バックアップスクリプトが動作する
- [ ] ネットワークが本番環境から分離されている（推奨）

### 動作確認テスト
```cmd
# 基本的な動作確認
sample_win_devicedriver_inf_install.exe install --inf "C:\DriverTests\INFFiles\basic_test.inf" --verbose

# ログの確認
type "C:\DriverTests\Logs\*.log"

# システム状態の確認
powershell -ExecutionPolicy Bypass -File "C:\DriverTests\check_system.ps1"
```

## トラブルシューティング

### よくある問題と解決方法

#### 1. テスト署名モードが有効にならない
**原因**: Secure Bootが有効  
**解決**: UEFI設定でSecure Bootを無効化

#### 2. 管理者権限が取得できない
**原因**: UAC設定、ドメインポリシー  
**解決**: ローカル管理者アカウントの使用、グループポリシーの確認

#### 3. 仮想マシンのパフォーマンス問題
**原因**: リソース不足  
**解決**: メモリ・CPU割り当ての増加、SSD使用

### 緊急時復旧手順

#### システム復元による復旧
```cmd
# 利用可能な復元ポイントの確認
rstrui.exe

# コマンドラインからの復元（Windows PE環境等）
wmic.exe /Namespace:\\root\default Path SystemRestore Call CreateRestorePoint "Before Recovery", 100, 12
```

#### レジストリ復旧
```cmd
# バックアップからの復元
reg import "C:\DriverTests\Backup\HKLM_SOFTWARE_YYYYMMDD_HHMMSS.reg"
```

#### 手動クリーンアップ
```cmd
# テスト用レジストリキーの削除
reg delete "HKLM\SOFTWARE\TestDriver" /f

# テスト用ファイルの削除
del /f /q "C:\Windows\test.txt"
del /f /q "C:\Windows\System32\drivers\dummy.sys"
```

## セキュリティ考慮事項

### 最小権限の原則
- テスト用アカウントは必要最小限の権限のみ付与
- テスト完了後は速やかに環境をクリーンアップ
- 本番ネットワークからの分離

### データ保護
- テスト用証明書の適切な管理
- 機密情報を含まないテストデータの使用
- テスト環境の物理的セキュリティ確保

### 法的コンプライアンス
- 企業ポリシーの遵守
- ライセンス条項の確認
- テスト用ソフトウェアの使用許諾範囲の理解