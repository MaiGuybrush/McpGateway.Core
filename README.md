# McpGateway.Core

[![Status](https://img.shields.io/badge/status-Active%20Development-green)](#)
[![Spec](https://img.shields.io/badge/Spec-Core%20Spec-blue)](./docs/specs/mcp-gateway-core-spec.md)
[![Plan](https://img.shields.io/badge/Plan-Development%20Plan-blue)](./docs/specs/development-plan.md)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

`McpGateway.Core` 是建構企業級 Model Context Protocol (MCP) 閘道器的核心 .NET 9.0 基礎函式庫。它能將企業內部 REST APIs、微服務與資料庫轉化為對大型語言模型 (LLM) 友善的 MCP 工具契約，內建健全的跨廠認證容錯降級、稽核日誌、敏感個資 (PII) 遮罩脫敏、指標監控與編譯期分析器治理。

---

## 🎯 Purpose & Architecture

本專案架構奠基於 **[ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md)**（部門獨立閘道器拆分原則）與 **[ADR-014](./docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md)**（部門內部多子系統模組化與端點隔離規範）。

### 拓撲架構圖 (Multi-Department & Subsystem Topology)

```
 Agent (/fab2/mcp)         Agent (/fab2/mes/mcp)       Agent (/fab2/edc/mcp)
        │                           │                           │
        └───────────────────────────┼───────────────────────────┘
                                    │ Streamable HTTP
                                    ▼
                         Ingress — mcp.corp.local
                     ┌──────────────┴──────────────┐
              /fab2/*                             /spc/*
                     ▼                                   ▼
          McpGateway.Fab2 (Monorepo)              McpGateway.Spc
          ├── Host (Thin Web App)                 └── ...
          │     ├─ /fab2/mcp      (部門 Ingress 端點)
          │     ├─ /fab2/mes/mcp  (MES 工具隔離端點)
          │     └─ /fab2/edc/mcp  (EDC 工具隔離端點)
          ├── Subsystem: MES Module (Class Library)
          └── Subsystem: EDC Module (Class Library)
                     │
         [ IMcpSubsystemRegistry ]  ← 依端點動態白名單過濾工具
                     ▼
          Ocelot Gateway / Consul
          ┌──────────┴──────────┐
    Java Spring Boot      C#/.NET Services
```

### 核心設計理念
1. **部門級 Modular Monorepo**：依循 ADR-014，每個製造或業務部門維持單一獨立 Git 儲存庫，內部拆分為薄宿主 (`Host`) 與多個獨立子系統類別庫 (`Subsystem Modules`)。
2. **端點分流與工具隔離 (Endpoint Routing & Tool Isolation)**：
   - **部門 Ingress 端點 (`/{dept}/mcp`)**：對外彙整曝露該部門底下所有註冊之工具。
   - **子系統專屬端點 (`/{dept}/{system}/mcp`)**：透過 `IMcpSubsystemRegistry` 提供白名單過濾，不同業務系統僅能查詢並調用自身子系統所擁有的工具，防止跨系統工具混淆與權限穿透。
3. **零反射與 AOT 友善 (Zero-Reflection)**：全系統採用強型別註冊擴充方法 (`Add<System>Subsystem()`)，移除 Assembly 反射掃描，確保 Native AOT 相容性與啟動效能。
4. **編譯期命名治理 (MCP003 Analyzer)**：內建 Roslyn 分析器，強制子系統工具採用三段式命名規範 `{department}_{system}_{action}`，杜絕名稱衝突。

---

## 🏗️ Repository Structure

```
McpGateway.Core/
├── src/
│   ├── McpGateway.Core/                 # Core 核心 NuGet 套件原始碼 (.NET 9.0)
│   │   ├── Auth/                        # 跨廠認證降級代理、JWT/API-Key/NTLM 驗證
│   │   ├── Cache/                       # Memory & Redis 快取抽象
│   │   ├── Configuration/               # 選項驗證器與部門合約規範
│   │   ├── Downstream/                  # 下游服務 HTTP 調用與彈性恢復
│   │   ├── Hosting/                     # AddMcpGateway / RunMcpGatewayAsync 宿主整合
│   │   ├── Observability/               # Prometheus 指標與健康檢查
│   │   ├── Subsystems/                  # IMcpSubsystemRegistry 子系統隔離與端點分流引擎
│   │   ├── Tools/                       # ToolBase 基礎設施與啟動驗證
│   │   └── Validation/                  # 啟動時規範合規檢查
│   ├── McpGateway.Analyzers/            # Roslyn 靜態分析器與代碼修復 (.NET Standard 2.0)
│   │   ├── McpServerToolAnalyzer.cs     # MCP001: 工具類別必須標註 [McpServerToolType]
│   │   ├── McpToolContractAnalyzer.cs   # MCP002: 工具方法與 DTO 契約規範
│   │   └── McpSubsystemToolNamingAnalyzer.cs # MCP003: 子系統工具三段式命名規範 ({dept}_{system}_{action})
│   └── MockOcelotApi/                   # 整合測試專用之 Mock 下游 API 伺服器
├── templates/                           # 部門與子系統腳手架範本樹
│   ├── template-host/                   # 薄宿主 Web App 專案範本 (含 .vscode 除錯設定與 HelloTool)
│   ├── template-module/                 # 子系統模組類別庫範本 (含 Consul KV/服務發現雙解析器、mcp-glossary.json)
│   ├── template-module-tests/           # 子系統單元測試專案範本
│   ├── scaffold.ps1                     # 部門 Modular Monorepo 骨架一鍵產生腳本
│   └── add-module.ps1                   # 增量子系統模組與單元測試自動產生腳本
├── tests/
│   ├── McpGateway.Analyzers.Tests/      # Roslyn 分析器與 CodeFix 單元測試
│   ├── McpGateway.Core.IntegrationTests/# 核心隔離引擎與 WireMock / Mock API 整合測試
│   └── k6/                              # 效能與高承載壓力測試腳本
├── docs/                                # 架構 ADR、需求規範與決策地圖
└── McpGateway.Core.sln                  # 方案總檔
```

---

## 🚀 腳手架建立指南 (Scaffolding Guide)

依據 **[ADR-014](./docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md)** 規範，部門閘道器建置採用「**初次建立部門 Monorepo 骨架** $\rightarrow$ **後續增量擴充業務子系統模組**」兩段式工作流。

### 第一步：建立部門 Modular Monorepo 骨架 (`scaffold.ps1`)

執行 `templates/scaffold.ps1` 自動建立部門根目錄方案、薄宿主專案與 VS Code 除錯設定：

```powershell
# 1. (建議) 先使用 -DryRun 預覽預計產生的檔案清單與路徑映射
pwsh .\templates\scaffold.ps1 `
    -Department fab2 `
    -Port 5200 `
    -AuthProvider API-KEY `
    -UseConsul $true `
    -Solution $true `
    -OutDir .. `
    -DryRun

# 2. 正式產生部門方案骨架 (-UseConsul 與 -Solution 預設皆為 $true，亦可省略)
pwsh .\templates\scaffold.ps1 `
    -Department fab2 `
    -Port 5200 `
    -AuthProvider API-KEY `
    -UseConsul $true `
    -Solution $true `
    -OutDir ..
```

產出結構包含：
- 方案根目錄：`McpGateway.<Dept>.sln`、`.gitignore`、`nuget.config`、`README.md`
- VS Code 除錯組態：`.vscode/launch.json` 與 `.vscode/tasks.json`（已預先綁定 Host 專案路徑）
- 薄宿主專案：`src/McpGateway.<Dept>.Host/`，內建煙霧測試工具 `HelloTool`（端點：`http://localhost:<Port>/<dept>/mcp`）

---

### 第二步：增量建立子系統模組與測試專案 (`add-module.ps1`)

當部門內有新的業務系統（如 MES、EDC、SPC、EAP 等）需要納入 MCP 閘道器時，使用 `templates/add-module.ps1` 增量擴充：

```powershell
# 情境 A：於 McpGateway.Core 根目錄執行（指向部門方案目錄）
pwsh .\templates\add-module.ps1 `
    -Department fab2 `
    -System mes `
    -ToolName fab2_mes_query_lot `
    -OutDir ..\McpGateway.Fab2

# 亦支援別名 -RepoRoot：
# pwsh .\templates\add-module.ps1 -Department fab2 -System mes -ToolName fab2_mes_query_lot -RepoRoot ..\McpGateway.Fab2

# 情境 B：已切換至部門專案目錄 (cd ..\McpGateway.Fab2)
# pwsh <PathToCore>\templates\add-module.ps1 -Department fab2 -System mes -ToolName fab2_mes_query_lot
```

#### 💡 `add-module.ps1` 參數說明

| 參數 | 必填 | 預設值 | 說明 |
|---|---|---|---|
| `-Department` | **是** | - | 部門代號（如 `fab2`、`eap`），長度 2~20 字元，不可為保留字。 |
| `-System` | **是** | - | 子系統代號（如 `mes`、`edc`、`spc`），長度 2~20 字元，不可為保留字。 |
| `-ToolName` | **是** | - | 主要 MCP 工具名稱，建議遵循三段式 `{dept}_{system}_{action}`（例如 `fab2_mes_query_lot`）。 |
| `-OutDir` | 否 | `.\` | 部門方案目錄或其上層路徑（支援別名 `-RepoRoot`、`-TargetDir`）。預設會自動在目標路徑或當前目錄尋找 `src/McpGateway.<Dept>.Host`。 |
| `-DryRun` | 否 | `$false` | 預覽模式，僅輸出預計建立與修改之檔案清單，不寫入磁碟。 |
| `-Force` | 否 | `$false` | 若子系統目錄已存在，強制覆寫。 |

#### 腳本自動化完成事項：
1. **模組類別庫專案**：在 `src/McpGateway.Fab2.Mes/` 建立專案檔、依賴注入擴充方法 `AddMesSubsystem()`、DTO 與工具類別。
2. **單元測試專案**：在 `tests/McpGateway.Fab2.Mes.Tests/` 自動建立 xUnit 測試專案，包含工具方法測試與 DI 註冊測試。
3. **自動裝配與方案註冊**：
   - 自動透過 `dotnet sln add` 將模組專案與測試專案掛載至根目錄方案檔。
   - 自動透過 `dotnet add reference` 為 Host 專案加入子系統模組參考。
   - 自動在 Host 的 `Program.cs` 注入 `builder.Services.Add<System>Subsystem(builder.Configuration);`。
   - 自動在 Host 的 `appsettings.json` 加入 `Systems:<system>` 預設組態區段。
4. **Consul 下游位址解析雙模式**：範本內建 `IDownstreamUrlResolver` 介面，提供兩種企業級落地範例：
   - **範例 1 (`ConsulKvDownstreamResolver`)**：透過 Consul Key-Value 集中式設定動態讀取 Downstream Base URL。
   - **範例 2 (`ConsulServiceDiscoveryDownstreamResolver`)**：透過 Consul Service Discovery 查詢健康服務實例以解析位址。
5. **標準化詞庫**：包含符合 ADR-010 規範之 `mcp-glossary.json` 最簡詞庫陣列定義。

---

## 🛠️ 薄宿主端點掛載與子系統使用方式

在部門薄宿主專案 `McpGateway.<Department>.Host/Program.cs` 中，僅需引用子系統模組並啟用端點映射：

```csharp
using McpGateway.Core.Hosting;
using McpGateway.Core.Subsystems;
using McpGateway.Fab2.Mes;
using McpGateway.Fab2.Edc;

var builder = WebApplication.CreateBuilder(args);

// 1. 註冊 McpGateway 核心服務
builder.Services.AddMcpGateway(options =>
{
    options.Department = "fab2";
    options.RoutePrefix = "/fab2/mcp";
});

// 2. 註冊各業務子系統 (透過 Add<System>Subsystem 註冊工具與白名單隔離)
builder.Services.AddMesSubsystem(builder.Configuration);
builder.Services.AddEdcSubsystem(builder.Configuration);

var app = builder.Build();

// 3. 映射端點
app.MapMcp();             // 部門 Ingress 端點：/fab2/mcp (彙整曝露全部子系統工具)
app.MapMcpSubsystems();   // 子系統專屬隔離端點：/fab2/mes/mcp, /fab2/edc/mcp

await app.RunMcpGatewayAsync();
```

---

## 🛡️ 編譯期分析器治理 (Roslyn Analyzers)

專案包含 `McpGateway.Analyzers`，在開發與 CI 編譯階段即時防堵架構異味與命名違規：

| 規則代碼 | 嚴重性 | 診斷名稱 | 規範說明 |
|---|---|---|---|
| **MCP001** | Error | `McpServerToolTypeRequired` | MCP 工具類別必須標註 `[McpServerToolType]` 特性。 |
| **MCP002** | Warning | `McpToolContractConvention` | MCP 工具方法參數與回傳型別必須符合結構化 DTO 契約規範。 |
| **MCP003** | Warning | `McpSubsystemToolNaming` | **子系統工具三段式命名規範**：子系統專案中的 MCP 工具名稱強制遵循 `{department}_{system}_{action}`（全小寫 ASCII、英數字與底線）。Visual Studio / VS Code 支援一鍵重構修正 (CodeFix Provider)。 |

---

## 🤖 MCP Tool 開發指南 (AI Tool Skill 推薦)

在開發新的 MCP Tool 或將既有 Tool 回傳結構重構為結構化契約時，強烈建議使用專屬的 AI Agent Skill：

👉 **[`mcp-tool-dto-builder`](./docs/ai-tools/skills/mcp-tool-dto-builder/SKILL.md)**（目錄路徑：`docs/ai-tools/skills/mcp-tool-dto-builder`）

- **動態詞庫探測**：自動解析核心製造業字典 `Tier1Vocabulary.cs`。
- **欄位標準化與別名校正**：自動將歷史欄位（如 `prod_id`、`wip_qty`）校正為符合 [ADR-010](./docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 的標準詞彙。
- **不可變結構化契約**：一鍵產出 Positional Record DTO、繁體中文 `[Description]` 特性與工具骨架。

---

## 📝 Key Decision Summary (ADRs)

| ADR | Decision | Status |
|-----|----------|--------|
| [ADR-001](./docs/architecture/adr/ADR-001-use-mcp-protocol.md) | MCP protocol, Streamable HTTP | ✅ Approved |
| [ADR-002](./docs/architecture/adr/ADR-002-dotnet-mcp-sdk-choice.md) | Official .NET MCP SDK | ✅ Approved |
| [ADR-003](./docs/architecture/adr/ADR-003-config-driven-descriptions.md) | Code-inline tool descriptions | ✅ Approved |
| [ADR-004](./docs/architecture/adr/ADR-004-startup-validation.md) | Startup validation & department prefix check | ✅ Approved |
| [ADR-005](./docs/architecture/adr/ADR-005-tool-versioning.md) | Tool-level versioning (deferred to MVP+1) | ✅ Approved |
| [ADR-006](./docs/architecture/adr/ADR-006-security-model.md) | Authentication proxy pattern | ✅ Approved |
| [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) | Per-department gateway repository split | ✅ Approved |
| [ADR-010](./docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) | MCP Tool 欄位命名標準化、結構化 DTO 回傳與編譯期分析器治理 | ✅ Approved |
| [ADR-014](./docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md) | 部門內部多子系統模組化、端點分流隔離 (`/{dept}/{system}/mcp`) 與 MCP003 命名規範 | ✅ Approved |

---

## 🧪 Testing & Verification

```powershell
# 建置整個方案
dotnet build McpGateway.Core.sln

# 執行所有核心整合測試與 Roslyn 分析器測試
dotnet test McpGateway.Core.sln

# 啟動測試用 Mock Ocelot API 服務
dotnet run --project src/MockOcelotApi/MockOcelotApi.csproj
```

---

## 📄 License

MIT License — see [LICENSE](LICENSE) file for details.

