# McpGateway.__Department__.__System__

__Department__ 部門之 __System__ 子系統 MCP 模組。

## 模組說明

本專案為 Class Library，實作 __System__ 子系統的業務服務與 MCP 工具：
- 工具名稱遵循 `{department}_{system}_{action}` 三段式命名規範（如 `__full_tool_name__`）。
- 透過 `Add__System__Subsystem` 擴充方法向宿主註冊。
- 在 `McpGateway.Core` 的 Stateless Session 中依路徑 `/__department__/__system__/mcp` 動態分流與白名單隔離。
