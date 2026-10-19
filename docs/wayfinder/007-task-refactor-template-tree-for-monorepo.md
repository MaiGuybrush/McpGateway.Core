# [Task] 重構 templates/ 範本樹為 Modular Monorepo 結構

- **ID**: `007`
- **Label**: `wayfinder:task`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 004 resolved)
- **Resolved**: 2026-09-16

> **後續修訂（2026-10-19）**：下方「詞彙表」項目所述之 `mcp-glossary.json` / `docs/mcp-glossary.schema.json`（Tier 2 本地字典機制）已於 [Issue #11](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/11) 中淘汰並自範本移除，改為單層 `Tier1Vocabulary.cs` 中央字典 + DTO `[Description]` 註記。本文件保留原始決議內容作為歷史紀錄，現行範本結構請勿再參照此處的詞彙表說明。

## Question

依據 ADR-014 規範，將 `McpGateway.Core/templates/` 模板樹重構為分層結構：
1. `templates/template-host/`：包含薄宿主 Web 專案、包含 `// __SUBSYSTEM_REGISTRATION__` 錨點之 `Program.cs`、分層 `appsettings.json` 與 `McpGateway.__Department__.Host.csproj`。
2. `templates/template-module/`：包含子系統 Class Library 模板、`__System__ModuleExtensions.cs`、三段式 MCP Tool 類別與 `McpGateway.__Department__.__System__.csproj`。
3. 升級現有 `scaffold.ps1`，使其預設產出 `src/McpGateway.<Dept>.Host/` 與 Solution 檔案。

---

## Resolution

依據 ADR-014 規範與 Ticket 003 設計共識，完成 `templates/` 模板樹重構與 `scaffold.ps1` 升級：

### 1. 建立 `templates/template-host/`（薄宿主模板）
- **`Program.cs`**：極簡 Composite Root，註冊 `builder.Services.AddMcpGateway()`，預留 `// __SUBSYSTEM_REGISTRATION__` 注入錨點，並呼叫 `app.MapMcpGateway()` 與 `app.RunMcpGatewayAsync()`。
- **`McpGateway.__Department__.Host.csproj`**：Web SDK 專案檔，引用 `McpGateway.Core` 與 `McpGateway.Analyzers`。
- **`appsettings.json` / `appsettings.Development.json`**：分層結構，包含 `McpGateway.Systems` 區段、動態認證片段、Consul 與 Redis TokenCache 設定。
- **輔助與根檔案**：提供 `launchSettings.json`、`.vscode` 設定檔、`MCP_ENDPOINTS.md`、`nuget.config`、`.gitignore` 及 `README.root.md`。

### 2. 建立 `templates/template-module/`（子系統模組模板）
- **`McpGateway.__Department__.__System__.csproj`**：Class Library (net9.0) 專案檔，引用 `McpGateway.Core` 與 `McpGateway.Analyzers`。
- **`__System__ModuleExtensions.cs`**：提供 `Add__System__Subsystem(IServiceCollection, IConfiguration)` 擴充方法，綁定 `McpGateway:Systems:__system__` 組態並透過 `services.AddMcpSubsystem("__system__", subsystem => { subsystem.WithTools<__ToolClass__Tool>(); })` 註冊。
- **`Tools/__ToolClass__/__ToolClass__Tool.cs`**：MCP Tool 標註 `[McpServerTool(Name = "__full_tool_name__")]`，完整符合 `{dept}_{system}_{action}` 三段式規範並通過 Roslyn MCP003 診斷。
- **`Configuration` 與 `Services`**：包含 `__System__Options.cs`、`I__ToolClass__Service.cs` 與 `__ToolClass__Service.cs`。
- **詞彙表**：包含 `mcp-glossary.json` 與 `docs/mcp-glossary.schema.json`。*(已淘汰，詳見上方「後續修訂」)*

### 3. 升級 `templates/scaffold.ps1`
- 升級為預設產出 Modular Monorepo 方案結構：`$projectName.sln` 方案檔與 `src/$hostProjectName/` 薄宿主專案。
- 支援 `-Department`, `-Port`, `-AuthProvider`, `-UseConsul`, `-Solution` (預設 `$true`), `-OutDir`, `-DryRun`, `-Force`。
- 對傳入的 `-ToolName` 提供友善提示，引導使用者後續使用 `add-module.ps1` 增量擴充子系統。
- 移除過期單一專案平鋪模板 `templates/template/`。
- 經端到端測試：產出之方案檔、宿主與子系統模組編譯皆為 0 警告、0 錯誤，通過所有 MCP 分析器檢核。
