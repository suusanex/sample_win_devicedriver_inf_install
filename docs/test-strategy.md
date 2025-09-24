# テスト戦略とOS環境影響の分離

## 共通必須ルール（spec-kit 全体に適用）

本プロジェクト群では、以下のテスト・実行ルールを必ず守ること。

- UnitTest と（CIで走る）IntegrationTest は、実OS環境（レジストリ、SetupAPI、サービスマネージャー、ドライバ、デバイス等）を変更してはならない。
  - OS依存APIは必ずインターフェースで抽象化し、テスト時はスタブ／モックを使用する。
  - 本リポジトリでは ISetupApiWrapper を介し、実装は本番用 SetupApiWrapper、テスト用 SetupApiStub を用いる。
  - IntegrationTests でも SetupApiStub を注入して、実OS変更を回避する。
- 実OS環境を変更する検証（レジストリ・ファイルコピー・サービス登録・PnP 連携 等）は、「OS環境テスト」として専用テスト環境でのみ実施する。
  - CI 上では実行しない。開発者端末の通常テストでも実行しない。
  - 実行手順・観点は docs/os-integration-test-spec.md と docs/test-environment-setup.md に従う。
- 新規に OS を変更する機能を追加する場合は、必ず抽象化とDIを実装し、Unit/Integration ではスタブで置換できるようにする。
- テストは管理者権限を前提としない（OS環境テストを除く）。
- テストデータに機密情報や実運用証明書を含めない。ログにも機微情報を書き出さない。
- .NET 10 を前提とし、テストは開発者マシンとCIで再現性高く実行できること。

これらは spec-kit 全体での必須基準とし、逸脱が必要な場合は合意のうえドキュメントを更新すること。

## 概要
この三層のテスト戦略により、以下を実現しています：

1. 開発効率: Unit Test と Integration Test で高速な開発サイクル
2. 安全性: 実OS環境への影響を完全に排除
3. 品質保証: OS環境テストで実際の動作を検証
4. 保守性: 抽象化により将来の変更に対応

## テスト階層

### 1. Unit Test（単体テスト）
**場所**: `sample_win_devicedriver_inf_install.Tests`  
**目的**: 実際のOS環境に影響を与えずに、コンポーネントの動作を検証

#### 特徴
- **OS環境への影響**: なし（スタブ・モック使用）
- **実行環境**: 開発者マシン、CI/CDパイプライン
- **権限要件**: 一般ユーザー権限で実行可能
- **実行時間**: 高速（数秒〜数十秒）

#### 実装方針
```csharp
// SetupAPIの抽象化により、テスト時はスタブを使用
services.AddSingleton<ISetupApiWrapper, SetupApiStub>();

// 実際のWindowsAPIは呼び出されない
var result = await installationService.InstallFromInfAsync("test.inf");
```

#### 検証内容
- ビジネスロジックの正確性
- エラーハンドリングの動作
- ログ出力の内容
- APIの契約遵守

### 2. Integration Test（統合テスト）
**場所**: `sample_win_devicedriver_inf_install.IntegrationTests`  
**目的**: 複数コンポーネント間の連携を検証（ただしOS環境への影響は回避）

#### 特徴
- **OS環境への影響**: なし（Unit Testと同様にスタブ使用）
- **実行環境**: 開発者マシン、CI/CDパイプライン
- **権限要件**: 一般ユーザー権限で実行可能
- **実行時間**: 中程度（数十秒〜数分）

#### 実装方針
```csharp
// 統合テストでもスタブを使用してOS環境を保護
public class TestEnvironmentFixture : IDisposable
{
    private void SetupHost()
    {
        // SetupAPIスタブを使用（実OS環境に影響を与えない）
        var setupApiStub = new SetupApiStub();
        services.AddSingleton<ISetupApiWrapper>(setupApiStub);
    }
}
```

#### 検証内容
- サービス間の依存性注入
- エンドツーエンドのワークフロー
- 設定とロギングの統合
- パフォーマンス特性

### 3. OS Environment Test（OS環境テスト）
**場所**: 専用のテスト環境（ドキュメント化のみ）  
**目的**: 実際のWindowsOS環境での動作を検証

#### 特徴
- **OS環境への影響**: あり（実際のレジストリ・ファイルシステム変更）
- **実行環境**: 専用テスト環境（仮想マシン推奨）
- **権限要件**: 管理者権限必須
- **実行時間**: 長時間（数分〜数時間）

#### 実行方針
```csharp
// 本番環境では実際のSetupAPIを使用
services.AddSingleton<ISetupApiWrapper, SetupApiWrapper>();

// 実際のWindowsAPIが呼び出される
var result = await installationService.InstallFromInfAsync("real_driver.inf");
```

#### 検証内容
- 実際のINFファイル処理
- レジストリキー作成・削除
- ファイルシステムへの書き込み
- Windowsサービスの登録・起動
- デバイスマネージャーとの統合

## テスト戦略の利点

### 1. 安全性
- 開発者のマシンやCI環境を破損するリスクがない
- 誤って本番環境でテストを実行する危険性がない
- レジストリやシステムファイルの意図しない変更を防ぐ

### 2. 実行効率
- テスト実行が高速
- 開発サイクルの向上
- 継続的インテグレーションでの実行が容易

### 3. 再現性
- テスト結果が環境に依存しない
- 開発者間での一貫した結果
- デバッグが容易

### 4. 保守性
- テストの独立性が保たれる
- 環境セットアップが不要
- テストデータの管理が簡単

## 実装詳細

### 抽象化レイヤー

#### ISetupApiWrapper
```csharp
public interface ISetupApiWrapper
{
    IntPtr SetupOpenInfFile(string fileName, string? infClass, uint infStyle, out uint errorLine);
    bool SetupInstallFromInfSection(/* parameters */);
    // その他のSetupAPI関数
}
```

#### 実装クラス
```csharp
// 本番用（実際のWindows API呼び出し）
public class SetupApiWrapper : ISetupApiWrapper
{
    public IntPtr SetupOpenInfFile(/* parameters */)
    {
        return SetupApi.SetupOpenInfFile(/* parameters */);
    }
}

// テスト用（スタブ実装）
public class SetupApiStub : ISetupApiWrapper
{
    public IntPtr SetupOpenInfFile(/* parameters */)
    {
        // スタブロジック（実際のAPIは呼び出さない）
        return new IntPtr(1000); // 仮のハンドル
    }
}
```

### 依存性注入の設定

#### 本番環境
```csharp
// Program.cs
services.AddSingleton<ISetupApiWrapper, SetupApiWrapper>();
```

#### テスト環境
```csharp
// TestFixture
services.AddSingleton<ISetupApiWrapper, SetupApiStub>();
```

## OS環境テスト仕様

実際のOS環境での動作確認が必要な場合は、以下のドキュメントを参照してください：

- [OS環境統合テスト仕様書](./os-integration-test-spec.md)
- [テスト環境構築ガイド](./test-environment-setup.md)

これらのテストは以下の特徴を持ちます：

### 実行条件
- 専用のテスト環境（本番環境ではない）
- Windows 10/11 Professional以上
- 管理者権限での実行
- テスト署名モードの有効化

### テスト観点
- 実際のINFファイル処理
- レジストリ操作の正確性
- ファイルシステムへの影響
- サービス制御の動作
- エラー処理とロールバック

### 安全対策
- 仮想マシン環境の使用
- システム復元ポイントの事前作成
- テスト後のクリーンアップ手順
- 本番環境からの分離

## まとめ

この三層のテスト戦略により、以下を実現しています：

1. **開発効率**: Unit TestとIntegration Testで高速な開発サイクル
2. **安全性**: 実OS環境への影響を完全に排除
3. **品質保証**: OS環境テストで実際の動作を検証
4. **保守性**: 抽象化により将来の変更に対応

開発者は日常的にUnit TestとIntegration Testを実行し、リリース前に専用環境でOS環境テストを実施することで、高品質なソフトウェアを安全に開発できます。