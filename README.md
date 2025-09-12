# sample_win_devicedriver_inf_install
Windows向けデバイスドライバのinfを使用したインストーラのサンプル。インストール処理だけを簡易にテストする用途でも使うため、コンソールアプリとする。

## 共通必須ルール（spec-kit 全体）
- UnitTest と（CIで走る）IntegrationTest は、実OS環境（レジストリ、SetupAPI、サービス、ドライバ、デバイス等）を変更しない。
  - OS依存処理は必ず抽象化（例: ISetupApiWrapper）し、テストではスタブ／モックを注入する。
  - CIで実行される統合テストもスタブを使用し、管理者権限や実OS変更を要求しない。
- 実OS環境を変更する検証は docs/os-integration-test-spec.md に従い、専用テスト環境（仮想マシン等）でのみ実行する（CIでは実行しない）。

詳細:
- docs/test-strategy.md
- docs/os-integration-test-spec.md
- docs/test-environment-setup.md
