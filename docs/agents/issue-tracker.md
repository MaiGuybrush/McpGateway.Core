# Issue Tracker

本 repo 採 **Gitea issues ＋ repo 內設計文件** 雙軌追蹤。不使用 `gh` / `glab`。

## 正式追蹤票 — Gitea issues

- 位置：`http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues`
- 每張正式 issue（bug / feature / 架構議題）在 Gitea 開票，取得編號 N
- 建立/操作方式：`tea` CLI 或 Gitea API（`tea` 未在 PATH 時用 API；參照
  `docs/ai-tools/skills/mcp-tool-dto-builder/SKILL.md` Step 5 的 `tea pr create` 用法）

## 設計文件 — `docs/issue/issue-N-<slug>.md`

- 需要分析/方案評估/決議記錄的 issue，設計文件放 `docs/issue/`，編號與 Gitea 票一致
- 文件頭格式比照既有慣例（issue-11 / issue-14 / issue-15）：狀態、追蹤票連結、
  建立日期、類型、前置 issue
- 設計文件與實作代碼走同一 repo、同一 PR 審查

## 衝程執行票 — `.scratch/sprint-N/`

- 衝程（sprint）內的執行拆解票放 `.scratch/sprint-N/NN-<slug>.md`，屬一次性工作紀錄，
  不具版本治理效力，完成後不遷移

## 慣例

- 代碼提交引用 issue：commit message 帶 `（Issue #N）`
- 版本進板（`chore(release)`）時，ReleaseNote 章節連結對應 issue 設計文件
