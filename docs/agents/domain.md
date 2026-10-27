# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`GLOSSARY.md`**（repo 根）：領域語言定義
- **`src/McpGateway.Analyzers/Tier1Vocabulary.cs`**：標準詞彙與別名的權威來源
  （欄位命名的最終依據，GLOSSARY.md 指向它）
- **`docs/architecture/adr/`**：工作前閱讀涉及區域的 ADR（注意：本 repo ADR 在
  `docs/architecture/adr/`，非 `docs/adr/`）
- **`docs/issue/`**：既有 issue 設計文件含決議記錄，動工前確認相關議題的狀態

缺檔時逕行，不必提示。

## File structure

單脈絡 repo：

```
/
├── GLOSSARY.md
├── docs/architecture/adr/     ← 所有 ADR（本 repo 慣例路徑）
├── docs/issue/                ← issue 設計文件（含決議）
└── src/
```

## Use the glossary's vocabulary

輸出（issue 標題、重構提案、測試名）涉及領域概念時，使用 GLOSSARY.md /
Tier1Vocabulary.cs 定義的詞，不漂移到被 Avoid 的別名。詞需要新增而詞庫沒有時，
視為真實缺口，走 `/domain-modeling` 或詞庫貢獻流程（mcp-tool-dto-builder Step 5）。

## Flag ADR conflicts

與既有 ADR 矛盾時明確標出而非默默覆寫：

> 與 ADR-00XX（\<title\>）矛盾，但值得重啟因為…
