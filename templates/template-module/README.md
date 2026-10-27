# McpGateway.__Department__.__System__

__Department__ 部門之 __System__ 子系統 MCP 模組。

## 模組說明

本專案為 Class Library，實作 __System__ 子系統的業務服務與 MCP 工具：
- 工具名稱遵循 `{department}_{system}_{action}` 三段式命名規範（如 `__full_tool_name__`）。
- 透過 `Add__System__Subsystem` 擴充方法向宿主註冊。
- 在 `McpGateway.Core` 的 Stateless Session 中依路徑 `/__department__/__system__/mcp` 動態分流與白名單隔離。

## 契約目錄佈局（ADR-016）

```
Contracts/   ← ① 鏡射契約：全部 internal，跟隨下游 JSON 鍵（[JsonPropertyName] 只准出現在這裡）
Dtos/        ← ③ 輸出契約：public，欄位 = 詞庫標準詞（禁止 [JsonPropertyName]）
Services/    ← ② 契約映射發生地（Map*()：下游方言 → 詞庫標準詞）
Tools/       ← MCP tool，只引用 Dtos/ 的輸出契約
```

三層分工與規則見中央 repo（McpGateway.Core）的 `docs/ai-tools/skills/mcp-tool-dto-builder/CONTRACT_RULES.md` §2.2 與 ADR-016。
