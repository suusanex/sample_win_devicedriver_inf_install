# [PROJECT_NAME] Constitution
<!-- Example: Spec Constitution, TaskFlow Constitution, etc. -->

## Core Principles

### 憲法: テストの原則（UnitTest は外部環境を変更しない）

#### 目的

- 本プロジェクトの**単体テスト（UnitTest）は外部環境を一切変更せず**、\*\*完全に密閉（hermetic）\*\*な状態で実行する。
- **実環境（OSや外部資源）を使用する必要がある検証は、結合テスト（Integration Test）として扱い、仕様・計画に明記して管理**する。

#### 用語定義

- **外部環境**：OS環境変数／レジストリ／ファイルシステム（テンポラリ以外）／ネットワーク／プロセス実行／時刻・タイムゾーン・ロケール等、**非決定性や副作用を生む系**を指す。
- **Test Double**：実装の代役（Stub／Mock／Fake／Spy／Dummy など）を統称。ユニットから**外部環境への依存を置き換えるために必ず使用**する。

#### 不変原則（MUST）

1. **UnitTest は外部環境を変更・依存しない（hermetic MUST）。**

    - 例：`Environment.SetEnvironmentVariable`、レジストリ操作、`File.*`（テンポラリ以外）、`HttpClient` 等の外部通信、`Process.Start`、システム時刻・タイムゾーンの変更 **禁止**。
2. **外部環境が必要な検証は Integration Test としてのみ実施（MUST）。**

    - 実施前に**目的／対象範囲／隔離手順／ロールバック**をドキュメント化して合意する。
3. **外部依存はポート化（抽象化）して差し替える（SHOULD）。**

    - 例：`IEnvironment` / `IClock` / `IFileSystem` / `IProcessRunner` / `INetwork` などの**ポート**を定義し、

        - UnitTest では **Fake/Stub/Mock** を注入、
        - 結合テスト／本番では **Real Adapter** を注入。
4. **UnitTest 実行は並列・順序非依存で安定（SHOULD）。**

    - グローバル状態・静的単一体の書き換え／キャッシュ汚染を避ける。
5. **実害防止の二重化（SHOULD）。**

    - 単体用アサーション（テスト中に危険APIへ到達したら失敗）、
    - CI ではコンテナ／サンドボックス等の隔離実行を推奨。

### [PRINCIPLE_1_NAME]
<!-- Example: I. Library-First -->
[PRINCIPLE_1_DESCRIPTION]
<!-- Example: Every feature starts as a standalone library; Libraries must be self-contained, independently testable, documented; Clear purpose required - no organizational-only libraries -->

### [PRINCIPLE_2_NAME]
<!-- Example: II. CLI Interface -->
[PRINCIPLE_2_DESCRIPTION]
<!-- Example: Every library exposes functionality via CLI; Text in/out protocol: stdin/args → stdout, errors → stderr; Support JSON + human-readable formats -->

### [PRINCIPLE_3_NAME]
<!-- Example: III. Test-First (NON-NEGOTIABLE) -->
[PRINCIPLE_3_DESCRIPTION]
<!-- Example: TDD mandatory: Tests written → User approved → Tests fail → Then implement; Red-Green-Refactor cycle strictly enforced -->

### [PRINCIPLE_4_NAME]
<!-- Example: IV. Integration Testing -->
[PRINCIPLE_4_DESCRIPTION]
<!-- Example: Focus areas requiring integration tests: New library contract tests, Contract changes, Inter-service communication, Shared schemas -->

### [PRINCIPLE_5_NAME]
<!-- Example: V. Observability, VI. Versioning & Breaking Changes, VII. Simplicity -->
[PRINCIPLE_5_DESCRIPTION]
<!-- Example: Text I/O ensures debuggability; Structured logging required; Or: MAJOR.MINOR.BUILD format; Or: Start simple, YAGNI principles -->

## [SECTION_2_NAME]
<!-- Example: Additional Constraints, Security Requirements, Performance Standards, etc. -->

[SECTION_2_CONTENT]
<!-- Example: Technology stack requirements, compliance standards, deployment policies, etc. -->

## [SECTION_3_NAME]
<!-- Example: Development Workflow, Review Process, Quality Gates, etc. -->

[SECTION_3_CONTENT]
<!-- Example: Code review requirements, testing gates, deployment approval process, etc. -->

## Governance
<!-- Example: Constitution supersedes all other practices; Amendments require documentation, approval, migration plan -->

[GOVERNANCE_RULES]
<!-- Example: All PRs/reviews must verify compliance; Complexity must be justified; Use [GUIDANCE_FILE] for runtime development guidance -->

**Version**: [CONSTITUTION_VERSION] | **Ratified**: [RATIFICATION_DATE] | **Last Amended**: [LAST_AMENDED_DATE]
<!-- Example: Version: 2.1.1 | Ratified: 2025-06-13 | Last Amended: 2025-07-16 -->