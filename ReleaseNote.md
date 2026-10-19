# Release notes

## 0.2.2-preview

**程式優化與錯誤修復 (Enhancements & Bug Fixes)**
1. **詞彙治理單層化（[Issue #11](docs/issue/issue-11-glossary-tier-governance.md)）**：合併 Tier1/Tier2 雙層詞庫為單一 `Tier1Vocabulary.cs` 中央字典；移除孤兒的 Tier 2 機制（`CoreGlossary.cs`、`GlossaryLoader.cs`、`GlossaryTerm.cs`、範本 `mcp-glossary.json`/schema）；子系統專屬詞彙改以 DTO `[Description]` 表達。同步更新 ADR-010 與其主管簡報 HTML 反映單層現狀。
2. 移除 `MockOcelotApi` `/api/orders` handler 多餘的 `async` 修飾詞，消除 CS1998 編譯警告。

## 0.2.1-preview

**程式優化與錯誤修復 (Enhancements & Bug Fixes)**
1. **修復 Redis 未配置時啟動崩潰（[Issue #9](docs/issue/issue-9-redis-healthcheck-validation-crash.md)）**：`TokenCache.ConnectionString` 為空字串或未設定時，`RedisHealthCheck` 建構子參數改為預設 `= null`，使 DI 容器將 `IConnectionMultiplexer` 視為可選依賴，避免 Development 環境 `ValidateOnBuild` 拋出 `AggregateException` 導致啟動崩潰。
2. 移除 `AddMcpGateway()` 中途呼叫 `services.BuildServiceProvider()` 的暫時容器反模式，改直接讀取既有 `IConfiguration` 實例解析 `TokenCache.ConnectionString`。

## 0.2.0-preview


**功能更新 (Features & Enhancements)**
1. **單一套件合併發布（[ADR-015](docs/architecture/adr/ADR-015-single-package-analyzers-merge.md)）**：`McpGateway.Analyzers` 不再獨立發布，改由 `McpGateway.Core` 套件經 `analyzers/dotnet/cs/` 一併帶出。部門專案安裝 `McpGateway.Core` 即同時取得執行期庫與 Roslyn 分析器，編譯時自動生效，無需另外安裝 `McpGateway.Analyzers`。
2. **子系統端點分流與工具隔離（[ADR-014](docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md)）**：實作 `IMcpSubsystemRegistry`，單一 Gateway 程序可依子系統分流 MCP 端點，工具註冊彼此隔離（Modular Monorepo）。
3. **UAC API Key 認證、Consul 動態服務發現與啟動驗證（[ADR-013](docs/architecture/adr/ADR-013-uac-api-key-auth-and-consul-integration.md)）**：整合公司 UAC API Key 驗證、Consul 註冊與探索，並於啟動階段執行設定完整性檢查。
4. **工具輸出 DTO 標準化分析器（[ADR-010](docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md)）**：新增 `McpGateway.Analyzers`、`DomainGlossary` 與啟動驗證，確保 MCP tool 輸出 DTO 欄位命名與 schema description 符合規範。
5. **子系統工具三段式命名診斷（MCP003）**：分析器新增診斷規則，檢查子系統工具命名是否符合三段式慣例。
6. **部門 Gateway 腳手架**：提供 `scaffold.ps1` / `add-module.ps1` 建立部門 Gateway 專案與增量新增子系統模組，範本樹重構為 Modular Monorepo 結構。

**程式優化與錯誤修復 (Enhancements & Bug Fixes)**
1. 修復 Subsystem `WithTools` 重載誤綁導致子系統註冊 0 個工具的問題。
2. 健全化啟動驗證錯誤日誌，並提供預設開箱即用的 Serilog 檔案輸出。
3. 部門模板移除獨立的 `McpGateway.Analyzers` 套件參考（改由 Core 單一套件帶出，見 ADR-015）。
4. 移除 `tests/CorePackageTest` 空殼測試專案：其對已發佈套件的引用測試功能已由 `McpGateway.Core.IntegrationTests` 與部門專案實際安裝涵蓋。
5. 測試專案補上 `IsPackable=false`，避免 `dotnet pack` 誤將測試專案打包推上內部 feed。

## 0.1.0-preview

**功能更新 (Features & Enhancements)**
1. 首版發布：`McpGateway.Core` 執行期庫，提供認證、稽核（PII 遮罩）、驗證、傳輸、欄位投影與健康檢查等功能。

> ⚠️ 本版本的 `McpGateway.Analyzers` 需另行安裝；自 0.2.0-preview 起已合併為單一套件，建議升級。
