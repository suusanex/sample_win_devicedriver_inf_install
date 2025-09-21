---
applyTo:
  - "specs/**/spec.md"
---

# Spec Guardrails (Path-scoped)


## 要約（主要ポイント）

- 指定された機能を**どう実装するか計画する段階**（Spec-Driven Development の第2ステップ）。
- `scripts/setup-plan.sh --json` を実行し、**FEATURE\_SPEC・IMPL\_PLAN・SPECS\_DIR・BRANCH** を取得（絶対パス必須）。
- 仕様書を読み込み、機能要件・非機能要件・成功/受け入れ基準・技術的制約を理解する。
- `/memory/constitution.md` を参照し、憲法的要件も確認。
- `/templates/plan-template.md` に基づいて計画を実行：

    - 入力に仕様ファイルを設定
    - フローに従い **研究(research.md) → データモデル(data-model.md, contracts/, quickstart.md) → タスク(tasks.md)** を順に生成
    - 引数の実装詳細を Technical Context に反映
    - 進捗管理を更新しながら各フェーズを完了させる
- 最終確認として、全フェーズ完了・成果物生成済み・エラーなしをチェック。
- ブランチ名、ファイルパス、生成成果物を報告する。

## 詳細

[spec.prompt.md](.github/prompts/spec.prompt.md)
