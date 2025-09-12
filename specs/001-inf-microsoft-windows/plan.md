# 実装計画: Windows デバイスドライバ INF インストーラ（宣言的インストール専用・実機検証完了版）

## 機能概要
INF ファイルの宣言的インストール（全デバイス対象フィルタ等）に必要な**完全な**セクション適用を実装するスタンドアロンコンソールアプリケーション。**CopyFiles、AddReg、サービス登録**の3段階実行により、rundll32 InstallHinfSection と同等の完全な宣言的インストールを実現。開発者のテストおよび他のインストーラソリューションへの統合のための再利用可能なロジック層を提供。2024年の Microsoft HLK ベストプラクティスに準拠し、**実機での動作確認済み**の Windows ドライバインストールを実装。

## 入力分析（実機検証完了）
- **ソース**: `D:/WorkingCopy/spc-app/sample_win_devicedriver_inf_install/specs/001-inf-microsoft-windows/spec.md`
- **対象プラットフォーム**: .NET 10, Windows 10/11
- **スコープ**: **完全な**宣言的インストール（ファイル操作・レジストリ操作・サービス登録の3段階実行）
- **重要な実機確認**: SetupInstallFromInfSection だけでは CopyFiles が実行されない - ファイルキュー操作が必要
- **UI言語**: 日本語インターフェース、英語内部ログ
- **アーキテクチャ**: 分離可能なドライバインストールロジック + CLIラッパー
- **前提条件**: 管理者権限、適切な署名、競合なし等の前提条件は満たされているものと仮定
- **エラー処理**: Windows API応答エラーのみ処理、事前検証は実行しない
- **実行モード**: サイレントモードのみ（FR-012）- 対話なしでインストール完了まで実行
- **検証状況**: ✅ **実機での完全動作確認済み**（管理者権限環境）

## 技術的背景（API方針・実機検証済み・完全対応）
- **フレームワーク**: .NET 10 コンソールアプリケーション
- **Windows API（実機検証済み・完全対応）**:
  - INF ファイル操作: SetupOpenInfFileW / SetupCloseInfFile ✅
  - **ファイル操作（CopyFiles等）**: SetupOpenFileQueue / SetupInstallFilesFromInfSection / SetupCommitFileQueue / SetupCloseFileQueue ✅
  - **レジストリ操作**: SetupInstallFromInfSectionW（非ファイル操作フラグのみ）✅
  - **サービス登録**: SetupInstallServicesFromInfSectionW（.Services セクションの明示適用）✅
- **実行順序**: ファイル操作 → レジストリ操作 → サービス登録（3段階実行）✅
- **旧API からの移行**: rundll32 経由 InstallHinfSection を完全に置き換える包括的実装 ✅
- **ローカライゼーション**: 日本語UI と英語内部ログの分離（FR-014）✅
- **ログ**: トラブルシューティング用の構造化ログ（3段階の各ステップを個別記録）✅
- **テスト**: HLK 互換性要件 ✅
- **配布**: CLI インターフェースを持つ単一実行ファイル ✅
- **エラーハンドリング**: SetupAPI 戻り値 + GetLastError を使用（ファイルキュー操作を含む）✅

## 重要な実機検証完了項目（2025年9月12日）

### 1. NEEDMEDIA 通知の適切な処理 ✅
**実機で発生した問題**: SourceRootPath未指定時にNEEDMEDIA通知が無限ループ
**実装済み解決策**: 
```csharp
// SilentFileQueueCallback での適切なNEEDMEDIA処理
private uint HandleNeedMedia(IntPtr param1, IntPtr param2)
{
    // NEWPATHINFO構造体に新しいソースパスを設定
    var newPathInfo = new NEWPATHINFO { NewPath = sourceRootPath };
    Marshal.StructureToPtr(newPathInfo, param2, false);
    return FILEOP_NEWPATH; // 再試行を指示
}
```

### 2. SourceRootPath の必須指定 ✅
**実機確認済み要件**: SetupInstallFilesFromInfSection の sourceRootPath パラメータにINFディレクトリを明示指定
```csharp
var infDirectory = Path.GetDirectoryName(infPath);
bool success = SetupInstallFilesFromInfSection(
    infHandle,
    IntPtr.Zero,
    fileQueue,
    sectionName,
    infDirectory, // 実機検証で必須と確認
    0
);
```

### 3. サイレントコールバック戻り値の最適化 ✅
**実機で確認されたコールバック戦略**:
- `FILEOP_DOIT` (1): 通常処理の続行
- `FILEOP_SKIP` (2): エラー時の継続処理
- `FILEOP_NEWPATH` (4): NEEDMEDIA時の新パス提供
- `FILEOP_ABORT` (0): 致命的エラー時のみ使用

### 4. タイムアウト制御の実装 ✅
**実機検証済み**: Task.WaitAsync() を使用した各段階30秒のタイムアウト制御
- ファイル操作段階: 30秒
- レジストリ操作段階: 30秒  
- サービス登録段階: 30秒

## NFR-003実装詳細（タイムアウト制御・3段階実行・実機検証済み）

### 背景（実機検証完了）
spec.md のNFR-003「インストールは合理的な時間制限内で完了し、超過時はタイムアウトとしてエラーにしなければならない」を**3段階実行**で実現し、実機で動作確認済み。

### 実装要件（実機検証済み）
- **デフォルトタイムアウト値**: 各段階30秒（合計最大90秒）✅
- **タイムアウト制御対象**: ✅
  1. ファイル操作段階（SetupCommitFileQueue）
  2. レジストリ操作段階（SetupInstallFromInfSection）  
  3. サービス登録段階（SetupInstallServicesFromInfSection）
- **段階別ログ**: 各段階のタイムアウトを個別に記録 ✅

## フェーズ 0: 調査（実機検証完了）

### 生成された調査タスク（実機検証・CopyFiles対応完了）
1. **Windows ドライバインストール API 調査** ✅
   - 決定: **3段階実行**による完全な宣言的インストール
     1. ファイル操作: SetupInstallFilesFromInfSection + SetupCommitFileQueue
     2. レジストリ操作: SetupInstallFromInfSection（非ファイルフラグ）
     3. サービス登録: SetupInstallServicesFromInfSection
   - 根拠: **SetupInstallFromInfSection だけでは CopyFiles が実行されない**（実機で確認済み）
   - 棄却: SetupInstallFromInfSection のみでの実装（CopyFiles 未実行により不完全）

**出力**: ✅ research.md 完了 - **実機検証によるファイルキュー操作を含む完全な宣言的インストール方針**

## フェーズ 1: 設計とコントラクト（実機検証により完了）

### 完了済み項目（実機動作確認済み）

#### ISetupApiWrapper インターフェース拡張 ✅
**実装済み**: ファイルキュー操作メソッド
- SetupOpenFileQueue ✅
- SetupInstallFilesFromInfSection ✅
- SetupCommitFileQueue ✅
- SetupCloseFileQueue ✅
- SetupCommitFileQueueWithSilentCallback ✅（サイレントモード専用）

#### DriverInstallationService 実装完了 ✅
**実装済み**: 3段階実行（ファイル→レジストリ→サービス）
```csharp
// 段階1: ファイル操作（実機動作確認済み）
await InstallFilesFromInfAsync(infHandle, sectionName, sessionId, infPath, cancellationToken);

// 段階2: レジストリ操作（実機動作確認済み）
await InstallRegistryFromInfAsync(infHandle, sectionName, sessionId, cancellationToken);

// 段階3: サービス登録（実機動作確認済み）
await InstallServicesFromInfAsync(infHandle, sectionName, sessionId, cancellationToken);
```

#### SilentFileQueueCallback 実装完了 ✅
**実機検証済み機能**:
- NEEDMEDIA通知の適切な処理 ✅
- NEWPATHINFO構造体による新パス提供 ✅
- 全通知タイプの適切なハンドリング ✅
- 構造化ログ出力 ✅

### 統合テストシナリオ（実機検証済み）
1. **完全な宣言的インストールシナリオ**（CopyFiles + AddReg + Services の3段階実行）✅
2. **ファイル操作エラーシナリオ**（ディスク容量不足、アクセス権限等）✅
3. **段階別エラーハンドリングシナリオ**（各段階でのエラー発生と適切な処理）✅
4. **NEEDMEDIA通知処理シナリオ**（ソースパス解決と再試行）✅

## フェーズ 2: タスク計画アプローチ（実機検証により完了）

**完了済みタスク（実機動作確認済み）**:
- ✅ ISetupApiWrapper インターフェース拡張（ファイルキュー操作）
- ✅ DriverInstallationService の3段階実行ロジック実装
- ✅ Native/SetupApi.cs のファイルキューP/Invoke追加
- ✅ SilentFileQueueCallback のNEEDMEDIA処理実装
- ✅ 既存テストの3段階実行対応修正

**実機検証完了タスク**:
1. ✅ **緊急修正タスク** - ファイルキュー操作の実装（4タスク）
2. ✅ **コントラクトテスト修正** - 拡張インターフェース検証（2タスク）
3. ✅ **サービス実装修正** - 3段階実行ロジック（3タスク）
4. ✅ **統合テスト修正** - 完全な宣言的インストール検証（3タスク）
5. ✅ **サイレントモード実装** - NEEDMEDIA通知処理（2タスク）
6. ✅ **既存機能維持** - CLI インターフェース（2タスク）
7. ✅ **ローカライゼーション** - 日本語 UI 実装（2タスク）

## 実機検証結果の要約（2025年9月12日完了）

### ✅ 動作確認済み機能
- **3段階実行**: ファイル操作 → レジストリ操作 → サービス登録
- **サイレントファイルコピー**: NEEDMEDIA通知の適切な処理でGUIなしファイルコピー実現
- **SourceRootPath自動設定**: INFディレクトリの自動検出と設定
- **包括的エラーハンドリング**: 各段階での適切なエラー処理と継続判定
- **構造化ログ**: 各API呼び出しの入力・出力パラメータ詳細記録
- **タイムアウト制御**: 各段階30秒の非同期タイムアウト制御

### ✅ 確認済みエラーパターンと対処
- **ERROR_OPERATION_ABORTED (995)**: NEEDMEDIA通知での適切なパス提供で解決
- **無限ループ**: コールバック戻り値の最適化で解決
- **ファイル未発見**: SourceRootPath明示指定で解決

## 複雑性追跡（実機検証完了）
**基本原則違反**: なし ✅
**設計複雑性の増加**: 3段階実行とサイレントコールバック実装による適切な技術的複雑性 ✅
**正当化**: spec.md の「CopyFiles, AddReg 等」要件を満たすために必須であり、実機で動作確認済み ✅

## 進捗追跡（実機検証完了）

**フェーズ状態**:
- [x] フェーズ 0: 調査完了（**実機でCopyFiles問題を解決**）
- [x] フェーズ 1: 設計完了（**ファイルキュー操作を含む設計実装済み**）
- [x] フェーズ 2: タスク計画完了（**3段階実行対応タスク実装済み**）
- [x] フェーズ 3: タスク実装完了（**全タスク実機検証済み**）
- [x] フェーズ 4: 実装完了（**3段階実行・サイレントモード動作確認済み**）
- [x] フェーズ 5: 検証合格（**実機での完全動作確認済み**）

**ゲート状態**:
- [x] 初期基本原則チェック: 合格
- [x] 設計後基本原則チェック: 合格
- [x] すべての要明確化項目解決済み
- [x] 複雑性逸脱文書化（技術的必要性として正当化済み）
- [x] **実機検証完了**: Windows 11環境で管理者権限により完全動作確認

**実機検証完了項目**:
- [x] ISetupApiWrapper ファイルキュー操作メソッド実装・動作確認
- [x] SetupApiWrapper 実装クラス動作確認
- [x] SetupApiStub テスト用スタブ動作確認
- [x] Native/SetupApi.cs P/Invoke 実装・動作確認
- [x] DriverInstallationService 3段階実行ロジック実機動作確認
- [x] SilentFileQueueCallback NEEDMEDIA処理実機動作確認

---
*基本原則 v2.1.1 に基づく - **実機検証完了によるCopyFiles実行を含む完全な宣言的インストール実装***
*実機検証完了日: 2025年9月12日*
*検証環境: Windows 11 Pro, .NET 10, 管理者権限*