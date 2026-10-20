# McpGateway.__Department__

__Department__ 部門 MCP Gateway Modular Monorepo 方案。

## 專案結構

- `src/McpGateway.__Department__.Host/`：部門薄宿主 Web 服務（Composite Root、Ingress 分流與認證）。
- `src/McpGateway.__Department__.<System>/`：各業務子系統 Class Library 模組（由 add-module.ps1 建立）。

## 增量新增子系統模組

請使用腳手架指令新增子系統：

```powershell
pwsh ..\templates\add-module.ps1 -Department __department__ -System <system> -ToolName <system_action>
```

## 本地開發與編譯

```powershell
dotnet restore
dotnet build
dotnet run --project src/McpGateway.__Department__.Host
```
