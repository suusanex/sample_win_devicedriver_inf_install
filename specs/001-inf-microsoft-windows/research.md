# Windows デバイスドライバインストール調査

## API 選択再検討（T003 リセット）

### 決定: SetupAPI による宣言的 INF セクション実行に集約
本ソフトは「全デバイスに対するフィルタとして宣言的にインストール」する用途のみを対象とする。よって PnP デバイス個体へのバインド処理は不要であり、INF のセクション（例: DefaultInstall とその .Services）を安全に適用する API 構成に限定する。

- 宣言的インストール（デバイス非依存・DefaultInstall 等のセクション実行）
  - SetupOpenInfFileW / SetupInstallFromInfSectionW / SetupInstallServicesFromInfSectionW / SetupCloseInfFile
  - 目的: 「rundll32 SETUPAPI.DLL,InstallHinfSection DefaultInstall 132 …」と同等のセクション適用を、rundll32 ではなく API 直接呼び出しで実現（推奨）。サービス登録は .Services を SetupInstallServicesFromInfSectionW で明示的に適用する。

この構成により、旧来の InstallHinfSection を rundll32 経由で呼び出す方法を置き換え、プログラムから安全に制御可能な形で等価機能を提供しつつ、DPInst/DIFx などの非推奨技術は使用しない。PnP 個体へのドライバ適用（UpdateDriverForPlugAndPlayDevicesW 等）は本用途のスコープ外とする。

### 根拠
- Microsoft は DIFx/DPInst を非推奨とし、アプリ側では SetupAPI の直接呼び出しを推奨
- INF セクション適用は SetupInstallFromInfSectionW が公式かつ直接的な手段
- サービス登録は SetupInstallServicesFromInfSectionW が公式にサポートする手段であり、[SectionName.Services] を明示的に適用できる
- HLK/監査観点: 「ファイル/レジストリ適用」と「サービス登録」を明確に区別した実装は説明性が高い

### 使用する主要 API（最終）
- INF セクション実行（宣言的インストール）
  - SetupOpenInfFileW
  - SetupInstallFromInfSectionW
  - SetupInstallServicesFromInfSectionW
  - SetupCloseInfFile
- エラー取得
  - GetLastError、SetupAPI の返却コード

### 検討した代替案と棄却理由
- rundll32 + InstallHinfSection: スクリプト用途向けで制御性/可観測性が低い。推奨は API 直接呼び出し。
- DIFx（DPInst/DIFxAPI）: 非推奨。HLK/最新ベストプラクティスに反する。
- PnPUtil/DevCon 実行: 外部ツール依存。統合/ログ/エラーハンドリングが困難。
- UpdateDriverForPlugAndPlayDevicesW によるデバイス適用: 本ソフトの用途（宣言的・全デバイスフィルタ）では不要。スコープ外。

### 実装方針（T003 に反映）
- 単一パスを提供（宣言的 INF セクション適用）
  1) 指定 INF の任意セクション（既定: DefaultInstall）を SetupInstallFromInfSectionW で実行
  2) 同名の .Services セクション適用（例: DefaultInstall.Services）を SetupInstallServicesFromInfSectionW で明示実行
- いずれも事前検証は行わず、API 実行時エラーのみを処理（FR-006/FR-014 整合）
- 実行結果/詳細は Microsoft.Extensions.Logging による構造化ログで記録（英語技術ログ）、UI メッセージは日本語

## Windows API エラーハンドリング

### 決定: SetupAPI エラーコード + GetLastError の組合せ
- SetupAPI の戻り値と GetLastError を取得
- エラーコード → 日本語メッセージのリソースマッピング（既存方針を継承）
- 失敗 API 名と実行コンテキスト（セクション名、INF パス等）を付加

### 実装注意事項
- 代表的な SetupAPI エラーコードの日本語化を網羅
- 権限不足、署名/ポリシー違反、競合、再起動要求などの区別
- 未知コードは英語技術詳細でフォールバック

## 構造化ログ

- Microsoft.Extensions.Logging を継続使用
- コンソール: 日本語 UI メッセージ
- ファイル: 英語技術ログ（API 名、セクション、戻り値、LastError）
- JSON/テキスト併用、セッション相関 ID

## エラーハンドリングとローカライゼーション

- .resx に日本語メッセージを保持
- 技術詳細（英語）とユーザーメッセージ（日本語）を分離
- コンテキスト依存メッセージ（セクション名/INF パス）を埋め込み

## テスト戦略（更新）

- 単体テスト（API モック）
  - SetupInstallFromInfSectionW の失敗/成功分岐
  - SetupInstallServicesFromInfSectionW の失敗/成功分岐
- 統合テスト
  - 正当な INF の DefaultInstall セクション適用（宣言的インストール）
  - .Services セクション適用の検証（サービス作成/スタートアップ種別/依存関係）
- ネガティブテスト
  - 権限不足、署名/ポリシー違反、存在しないセクション/Services セクション
- 前提条件は従来通り（管理者権限/署名/競合なし）

## 参考 API と注意点

- SetupInstallFromInfSectionW は CopyFiles / AddReg 等のセクション指示を適用するが、サービス登録は原則 SetupInstallServicesFromInfSectionW で明示的に行うのが安全
- 再起動要求の検出/伝播に留意（エラーコードや戻り値、必要時のガイダンス表示）
- DPInst/DIFx は使用しない。rundll32 呼び出しは避け、アプリ内から直接 API を P/Invoke で呼ぶ。