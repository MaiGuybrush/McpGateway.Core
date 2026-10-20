# McpGateway.__Department__.Host

部門 MCP Gateway 薄宿主服務 (Thin Host)。

## 架構說明

本專案遵循 ADR-014《部門內部多系統模組化與端點分流架構標準》：
- 作為部門 Composite Root，負責載入 `McpGateway.Core` 核心基礎設施（認證、健康檢查、指標遙測、Session 隔離）。
- 各子系統（如 MES, WMS 等）以獨立 Class Library 模組實作，並透過 `Add<System>Subsystem` 組裝至本宿主。
- 端點自動支援：
  - 部門通用 Ingress 端點：`/__department__/mcp`
  - 子系統隔離端點：`/__department__/{system}/mcp`

## 新增子系統模組

請使用腳手架腳本新增子系統：

```powershell
pwsh ..\..\templates\add-module.ps1 -Department __department__ -System <system> -ToolName <system_tool>
```
