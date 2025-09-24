# Windows デバイスドライバインストール調査

## API 選択再検討（T003 リセット・CopyFiles 対応修正）

### 決定: SetupAPI による宣言的 INF セクション実行に集約（ファイルキュー操作を含む）
本ソフトは「全デバイスに対するフィルタとして宣言的にインストール」する用途のみを対象とする。よって PnP デバイス個体へのバインド処理は不要であり、INF のセクション（例: DefaultInstall とその .Services）を安全に適用する API 構成に限定する。

**重要な修正**: INF の CopyFiles 指示を正しく実行するには、SetupInstallFromInfSection だけでは不十分であり、**ファイルキュー操作**が必要。

- 宣言的インストール（デバイス非依存・DefaultInstall 等のセクション実行）
  - SetupOpenInfFileW / SetupCloseInfFile
  - **ファイル操作（CopyFiles等）**: SetupOpenFileQueue / SetupInstallFilesFromInfSection / SetupCommitFileQueue / SetupCloseFileQueue
  - **レジストリ操作**: SetupInstallFromInfSectionW（SPINST_REGISTRY フラグのみ）
  - **サービス登録**: SetupInstallServicesFromInfSectionW（.Services セクションの明示適用）

### CopyFiles が実行されない理由
- SetupInstallFromInfSectionW は主にレジストリ操作とサービス登録に使用される
- CopyFiles 指示を実行するには、専用のファイルキュー API が必要
- これらの API を組み合わせることで、rundll32 InstallHinfSection と同等の完全な宣言的インストールが実現される

### 使用する主要 API（修正版・完全）
- INF ファイル操作
  - SetupOpenInfFileW
  - SetupCloseInfFile
- **ファイル操作（CopyFiles等）**
  - SetupOpenFileQueue
  - SetupInstallFilesFromInfSection
  - SetupCommitFileQueue
  - SetupCloseFileQueue
- **レジストリ操作**
  - SetupInstallFromInfSectionW（SPINST_REGISTRY等の非ファイル操作フラグ）
- **サービス登録**
  - SetupInstallServicesFromInfSectionW
- エラー取得
  - GetLastError、SetupAPI の返却コード

### 実装方針（修正版）
宣言的 INF セクション適用を**3段階**で実行:
1. **ファイル操作**: 指定 INF セクションの CopyFiles 等をファイルキュー経由で実行
2. **レジストリ操作**: 同セクションの AddReg 等を SetupInstallFromInfSectionW で実行
3. **サービス登録**: 同名の .Services セクション適用（例: DefaultInstall.Services）を SetupInstallServicesFromInfSectionW で明示実行

この3段階実行により、rundll32 InstallHinfSection と同等の完全な宣言的インストールが実現される。

### 根拠（修正・追加）
- Microsoft は DIFx/DPInst を非推奨とし、アプリ側では SetupAPI の直接呼び出しを推奨
- **CopyFiles 実行には専用のファイルキュー API が必要**（SetupInstallFromInfSection では不十分）
- 各操作を明確に分離することで HLK/監査での説明性が向上
- ファイル操作、レジストリ操作、サービス登録の分離により、エラー発生時の原因特定が容易

### 検討した代替案と棄却理由（修正）
- rundll32 + InstallHinfSection: スクリプト用途向けで制御性/可観測性が低い。推奨は API 直接呼び出し。
- **SetupInstallFromInfSection のみでの実装**: CopyFiles が実行されない不完全なインストールとなる。
- DIFx（DPInst/DIFxAPI）: 非推奨。HLK/最新ベストプラクティスに反する。
- PnPUtil/DevCon 実行: 外部ツール依存。統合/ログ/エラーハンドリングが困難。
- UpdateDriverForPlugAndPlayDevicesW によるデバイス適用: 本ソフトの用途（宣言的・全デバイスフィルタ）では不要。スコープ外。

## Windows API エラーハンドリング（修正）

### 決定: SetupAPI エラーコード + GetLastError の組合せ（ファイルキュー操作を含む）
- SetupAPI の戻り値と GetLastError を取得
- **ファイルキュー操作**でのエラーも含めた包括的エラー処理
- エラーコード → 日本語メッセージのリソースマッピング（既存方針を継承）
- 失敗 API 名と実行コンテキスト（セクション名、INF パス、操作種別等）を付加

### 実装注意事項（修正）
- 代表的な SetupAPI エラーコードの日本語化を網羅
- **ファイル操作関連エラー**（アクセス権限、ディスク容量、ファイル競合等）の処理を追加
- 権限不足、署名/ポリシー違反、競合、再起動要求などの区別
- 未知コードは英語技術詳細でフォールバック

## 構造化ログ（修正）

- Microsoft.Extensions.Logging を継続使用
- コンソール: 日本語 UI メッセージ
- ファイル: 英語技術ログ（API 名、セクション、戻り値、LastError、**操作種別**）
- JSON/テキスト併用、セッション相関 ID
- **3段階実行の各ステップ**（ファイル・レジストリ・サービス）を個別にログ記録

## エラーハンドリングとローカライゼーション（修正）

- .resx に日本語メッセージを保持
- 技術詳細（英語）とユーザーメッセージ（日本語）を分離
- コンテキスト依存メッセージ（セクション名/INF パス/**操作種別**）を埋め込み

## テスト戦略（修正・ファイルキュー操作を含む）

- 単体テスト（API モック）
  - **SetupInstallFilesFromInfSection** の失敗/成功分岐
  - **SetupCommitFileQueue** の失敗/成功分岐  
  - SetupInstallFromInfSectionW の失敗/成功分岐（レジストリ操作）
  - SetupInstallServicesFromInfSectionW の失敗/成功分岐
- 統合テスト
  - 正当な INF の DefaultInstall セクション適用（**CopyFiles を含む**宣言的インストール）
  - .Services セクション適用の検証（サービス作成/スタートアップ種別/依存関係）
- ネガティブテスト
  - 権限不足、署名/ポリシー違反、存在しないセクション/Services セクション
  - **ファイル操作エラー**（ディスク容量不足、ファイル競合、アクセス権限等）
- 前提条件は従来通り（管理者権限/署名/競合なし）

## 参考 API と注意点（修正・重要）

- **SetupInstallFromInfSectionW は CopyFiles を実行しない** - ファイル操作には専用のファイルキューAPI が必要
- SetupInstallFilesFromInfSection + SetupCommitFileQueue によりCopyFiles指示が実行される
- サービス登録は原則 SetupInstallServicesFromInfSectionW で明示的に行うのが安全
- 再起動要求の検出/伝播に留意（エラーコードや戻り値、必要時のガイダンス表示）
- **3段階実行の順序**: ファイル操作 → レジストリ操作 → サービス登録（依存関係を考慮）
- DPInst/DIFx は使用しない。rundll32 呼び出しは避け、アプリ内から直接 API を P/Invoke で呼ぶ。

## 実装上の重要な変更点

### DriverInstallationService の修正が必要
1. **ファイルキュー操作の追加**
   - SetupOpenFileQueue
   - SetupInstallFilesFromInfSection  
   - SetupCommitFileQueue
   - SetupCloseFileQueue

2. **実行順序の変更**
   ```
   従来: セクション適用 → サービス登録
   修正後: ファイル操作 → レジストリ操作 → サービス登録
   ```

3. **SetupInstallFromInfSection の使用方法変更**
   - フラグを SPINST_ALL から非ファイル操作のみ（SPINST_REGISTRY | SPINST_INIFILES | SPINST_INI2REG 等）に変更
   - CopyFiles は別途ファイルキュー操作で実行

### ISetupApiWrapper インターフェースの拡張が必要
ファイルキュー操作用のメソッドを追加:
- SetupOpenFileQueue
- SetupInstallFilesFromInfSection
- SetupCommitFileQueue  
- SetupCloseFileQueue

この修正により、spec.md で期待されている「CopyFiles, AddReg 等」の完全な宣言的インストールが実現される。

# Windows ドライバインストール API 調査結果（実機検証完了）

## 調査目的
INF ファイルに基づく宣言的インストールの完全実装のため、Windows SetupAPI の適切な使用方法を調査・実機検証した。

## 重要な発見：3段階実行による完全な宣言的インストール

### 1. SetupInstallFromInfSection の制限（実機検証済み）
**問題**: SetupInstallFromInfSection は CopyFiles セクションを処理しない
- SPINST_FILES フラグを指定してもファイルコピーが実行されない
- レジストリ操作（AddReg）やサービス登録は正常に動作
- **結論**: ファイル操作には専用のファイルキューAPIが必須

### 2. 完全な宣言的インストールのアーキテクチャ（実機で動作確認済み）

#### **段階1: ファイル操作（CopyFiles 等）**
```csharp
// 1. ファイルキューを開く
IntPtr fileQueue = SetupOpenFileQueue();

// 2. INFセクションからファイル操作をキューに追加
bool success = SetupInstallFilesFromInfSection(
    infHandle, 
    IntPtr.Zero,    // layoutInfHandle
    fileQueue, 
    sectionName, 
    sourceRootPath, // 重要：INFディレクトリを明示指定
    0               // copyStyle
);

// 3. サイレントコールバックでファイルキューを実行
bool committed = SetupCommitFileQueue(
    IntPtr.Zero,     // owner
    fileQueue, 
    silentCallback,  // カスタムコールバック
    IntPtr.Zero      // context
);

// 4. ファイルキューを閉じる
SetupCloseFileQueue(fileQueue);
```

#### **段階2: レジストリ操作（AddReg 等）**
```csharp
bool success = SetupInstallFromInfSection(
    IntPtr.Zero,  // owner
    infHandle,
    sectionName,
    SPINST_REGISTRY | SPINST_INIFILES | SPINST_INI2REG | SPINST_BITREG, // ファイル操作除外
    IntPtr.Zero,  // relativeKeyRoot
    sourceRootPath, // ソースルートパス
    0,            // copyFlags
    IntPtr.Zero,  // msgHandler
    IntPtr.Zero,  // context
    IntPtr.Zero,  // deviceInfoSet
    IntPtr.Zero   // deviceInfoData
);
```

#### **段階3: サービス登録（.Services セクション）**
```csharp
string servicesSectionName = $"{baseSectionName}.Services";
bool success = SetupInstallServicesFromInfSection(
    infHandle,
    servicesSectionName,
    0 // flags
);
```

## 重要な実機検証結果

### NEEDMEDIA 通知の適切な処理（実機で発生・解決済み）

**問題**: SourceRootPath未指定時にNEEDMEDIA通知が発生し無限ループ
**解決策**: サイレントコールバックでのNEWPATHINFO構造体設定

```csharp
private uint HandleNeedMedia(IntPtr param1, IntPtr param2)
{
    if (param1 != IntPtr.Zero && param2 != IntPtr.Zero)
    {
        // SOURCE_MEDIA_W構造体から要求情報を取得
        var sourceMedia = Marshal.PtrToStructure<SOURCE_MEDIA_W>(param1);
        
        // NEWPATHINFO構造体に新しいパスを設定
        var newPathInfo = new NEWPATHINFO { NewPath = sourceRootPath };
        Marshal.StructureToPtr(newPathInfo, param2, false);
        
        // 新しいパスを提供して再試行
        return FILEOP_NEWPATH; // 4
    }
    
    return FILEOP_SKIP; // 2 (継続)
}
```

### ソースルートパスの重要性（実機で確認済み）

**必須要件**: SetupInstallFilesFromInfSection の sourceRootPath パラメータにINFファイルのディレクトリを明示指定
- 指定しない場合：NEEDMEDIA通知が発生
- 正しく指定：ファイルコピーが正常実行

```csharp
string infDirectory = Path.GetDirectoryName(infPath);
bool success = SetupInstallFilesFromInfSection(
    infHandle,
    IntPtr.Zero,
    fileQueue,
    sectionName,
    infDirectory, // 重要：INFと同じディレクトリを指定
    0
);
```

## サイレントモード実装（FR-012準拠・実機検証済み）

### コールバック戻り値の重要性
- `FILEOP_ABORT` (0): 処理を中断（ERROR_OPERATION_ABORTED = 995で失敗）
- `FILEOP_DOIT` (1): 処理を続行
- `FILEOP_SKIP` (2): 現在の操作をスキップして継続
- `FILEOP_RETRY` (3): 操作を再試行
- `FILEOP_NEWPATH` (4): 新しいパスを提供して再試行

### 実機で確認されたコールバック通知シーケンス
```
SPFILENOTIFY_STARTQUEUE (0x1)     → FILEOP_DOIT
SPFILENOTIFY_STARTSUBQUEUE (0x3)  → FILEOP_DOIT
SPFILENOTIFY_NEEDMEDIA (0xE)      → FILEOP_NEWPATH (パス提供後)
SPFILENOTIFY_STARTCOPY (0xB)      → FILEOP_DOIT
SPFILENOTIFY_ENDCOPY (0xC)        → FILEOP_DOIT
SPFILENOTIFY_ENDSUBQUEUE (0x4)    → FILEOP_DOIT
SPFILENOTIFY_ENDQUEUE (0x2)       → FILEOP_DOIT
```

## エラーハンドリング（実機で検証済み）

### タイムアウト制御の実装
- 各段階30秒のタイムアウト設定
- Task.WaitAsync()を使用した非同期タイムアウト制御
- タイムアウト時はOperationCancelledExceptionで中断

### 主要なエラーパターン
1. **ERROR_OPERATION_ABORTED (995)**: コールバックがFILEOP_ABORTを返した
2. **ERROR_FILE_NOT_FOUND (2)**: ソースファイルが見つからない
3. **ERROR_ACCESS_DENIED (5)**: 管理者権限不足
4. **ERROR_INVALID_HANDLE (6)**: INFハンドルまたはファイルキューが無効

## 推奨される実装パターン（実機で動作確認済み）

### 1. INFディレクトリの事前設定
```csharp
private async Task InstallFilesFromInfAsync(IntPtr infHandle, string sectionName, string correlationId, string infPath, CancellationToken cancellationToken)
{
    var infDirectory = Path.GetDirectoryName(infPath);
    // infDirectoryをsourceRootPathとして使用
}
```

### 2. サイレントコールバックの適切な初期化
```csharp
var silentCallback = new SilentFileQueueCallback(
    logger, 
    installationLogger, 
    correlationId, 
    infDirectory  // ソースルートパスを渡す
);
```

### 3. 包括的なログ記録
- SetupAPI呼び出しの入力・出力パラメータ
- コールバック通知の詳細
- エラー発生時の詳細情報

## API使用順序の確認（実機検証済み）
1. SetupOpenInfFile → INFファイルを開く
2. SetupFindFirstLine → セクション存在確認
3. **段階1**: ファイル操作（SetupOpenFileQueue → SetupInstallFilesFromInfSection → SetupCommitFileQueue → SetupCloseFileQueue）
4. **段階2**: レジストリ操作（SetupInstallFromInfSection with non-file flags）
5. **段階3**: サービス登録（SetupInstallServicesFromInfSection）
6. SetupCloseInfFile → INFファイルを閉じる

## 結論
**完全な宣言的インストール**には3段階実行が必須であり、特にファイル操作にはファイルキューAPIとサイレントコールバックの適切な実装が不可欠である。実機検証により、SourceRootPathの明示指定とNEEDMEDIA通知の適切な処理が成功の鍵となることが確認された。

---
*実機検証日: 2025年9月12日*
*検証環境: Windows 11, .NET 10, 管理者権限*