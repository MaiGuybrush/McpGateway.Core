# Release notes

## 0.2.5-preview

**功能更新 (Features)**
1. **【分析器】新增 `McpOutputContractAnalyzer`（`MCP0013`，[ADR-016](docs/architecture/adr/ADR-016-agent-layer-output-contract-standardization.md)／[Issue #15](docs/issue/issue-15-output-contract-agent-layer-standardization.md)）**：修復標準化鏈在輸出層的斷點——先前分析器只檢查 C# 符號名，而 Agent 實際讀到的 JSON 鍵由 `[JsonPropertyName]` 決定；鏡射型別直接作為 tool 回傳型別時，即使 C# 屬性已改為標準詞，Agent 讀到的仍是下游方言（編譯期綠燈、目標層未標準化）。
   - 規則：MCP tool 回傳可達閉包內的 public 輸出契約不得標註 `[JsonPropertyName]`，輸出 JSON 鍵一律由 C# 屬性名的 camelCase 推導，詞庫標準詞直接送達 Agent。
   - 判定：以「從 tool 簽名追符號可達性」為唯一權威（`CompilationStartAction` 建一次閉包並快取），不靠類別名字後綴或目錄位置；`internal` 型別、從 tool 回傳線路不可達的型別（設定鏡射、Service 內部型別）、Core／Analyzers 組件型別（外部系統契約）三類結構性豁免，**無 tool 級豁免 attribute**。
   - 過渡：預設 severity 為 `suggestion`（Info，不擋建置）；下游各專案在遷移完成的 PR 中以 `.editorconfig` 將 `MCP0013` 收為 `warning`，遷移一批收一批。本版無新增公開 API。
2. **文件與 Skill**：`mcp-tool-dto-builder` 的 `CONTRACT_RULES.md` 新增 §2.2「鏡射/透傳契約規則」（三層形態：鏡射契約標 `internal` 止於 Service 層／契約映射 `Map*()`／輸出契約禁 `JsonPropertyName`、來源欄名寫進 `[Description]`），補上既有 Skill 對鏡射場景的空白；`SKILL.md`／`WORKFLOW.md` 診斷對照表補入 `MCP0013`。
3. **腳手架（`templates/template-module/` 改為 ADR-016 契約佈局）**：新增 `Contracts/`（鏡射契約範例，`[JsonPropertyName]` 只准出現在這裡）與 `Dtos/`（輸入／輸出契約，詞庫標準詞、禁 `JsonPropertyName`），Service 範例展示契約映射 `Map*()`，Tool 只引用 `Dtos/` 的輸出契約；生成專案 README 附目錄佈局說明。全新 scaffold＋add-module 專案編譯與測試通過。

> 下游遷移（Qsys Issapi 18 tool 兩層化、Mes Arymes 兩層化、Umsapi／Report 目錄對齊）於本版發佈後進行，屬 Agent 可見鍵名的 BREAKING 變更（硬切不保留舊鍵），各 repo 在遷移 PR 中公告。

## 0.2.4-preview

**功能更新與錯誤修復 (Features & Bug Fixes)**
1. **詞庫語意邊界治理（[Issue #14](docs/issue/issue-14-vocabulary-semantic-boundary.md) 方案 A-min）**：修復下游 MCP0010 曾把時刻欄位 `StartTime` 誤建議改名為時數詞 `startHours` 的問題——詞庫只定義「一個詞是什麼」而未定義「與近義詞如何區分」，且警告訊息不含語意資訊。
   - 【詞彙】修正 `startHours` 語意：移除 `startTime` 別名；`startTime`（時刻）/`startDate`（日期）/`endDate`（日期）收錄為獨立標準詞彙，時數/時刻/日期/時間戳記語意互斥。下游同名 `StartTime` 欄位不再被 MCP0010 標記。
   - 【詞彙】6 個易混淆群（時間/計數/數量與良率/設備階層/載具階層/位置階層/站點）共 35 條詞條描述補上語意邊界（定義＋辨析＋範例）；詞庫重排為 8 個主題段，使近義詞同段相鄰（Skill 讀入順序即 LLM 比對順序）。
   - 【分析器】新增 `Tier1VocabularyValidator` 詞庫名稱一致性檢查（標準名重複／別名撞標準名／別名被兩條詞條共享三規則），由 `Tier1VocabularyConsistencyTests` 於 CI 強制，撞名衝突直接測試失敗（此前僅靠註解人工把關，`FindByAlias` 會靜默吞掉衝突）。
2. **【分析器·破壞性】診斷訊息全面中文化並送達詞條描述**：`MCP0010` 警告完整附帶詞條 `Description` 與「若語意不符請忽略」提示；`MCP0010`/`MCP0011`/`MCP0012` 診斷與 Code Fix 標題改為繁體中文呈現。診斷 ID 不變，`#pragma warning disable` 與 `.editorconfig` 設定不受影響；下游若有依英文訊息文字做的 CI 比對需調整。
3. **文件與 Skill**：新增 [Issue #14](docs/issue/issue-14-vocabulary-semantic-boundary.md) 詞彙語意邊界分析文件；修訂 [ADR-010](docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 加入兩條治理原則（詞條描述須含語意邊界、診斷訊息須送達詞條描述）；`mcp-tool-dto-builder` Skill 增訂別名命中僅為候選須核對邊界、決策理由須引用邊界依據、詞條三段式撰寫規範與 PR 詞條品質自查段。
4. **腳手架工具鏈修復（`templates/scaffold.ps1`、`add-module.ps1` 與範本）**：
   - 修復 PowerShell 5.1 編碼問題：`scaffold.ps1` 補上 UTF-8 BOM（原缺 BOM 導致整檔解析失敗）、全部 `Get-Content` 讀檔加 `-Encoding UTF8`、含中文的範本檔補齊 BOM，避免中文被以 Big5 誤解碼導致產生專案的中文損毀與建置失敗；複製範本時排除 `obj`/`bin` 殘留建置產物。
   - 修復 `add-module.ps1` 產生的 appsettings 片段結構錯誤（巢狀 `Downstream` 結構改為實際的扁平 `ConsulKey`/`FallbackBaseUrls`/`TimeoutSeconds`）、範本命名漂移（`ConsulKvKey` 改名 `ConsulKey`）、以及嚴重過時的 `__System__ModuleExtensionsTests.cs` 測試範本（原引用不存在的設定物件與解析器類別，導致新子系統測試專案必定編譯失敗）。
   - 移除 `scaffold.ps1` 已失效的 `ConsulUrls` 正規表達式死碼。

## 0.2.3-preview

**功能更新與錯誤修復 (Features & Bug Fixes)**
1. **新增搬送/倉儲位置領域共通詞彙（[Issue #11](docs/issue/issue-11-glossary-tier-governance.md) §6.7）**：因應後續 CFMES、LcdMes 子系統預期沿用同一套 AMHS 搬送位置模型，提前將 `zoneId`/`shelfId`/`patiId`/`position` 收錄為 Tier 1 共通詞彙，避免各子系統日後各自命名造成別名清理成本。
2. **修正子系統腳手架範本（`templates/template-module/`）殘留的舊版 Consul/Downstream 結構**：拉平巢狀 `DownstreamOptions`/`ConsulOptions`，刪除未使用的「範例2」服務探索式解析器，Consul KV 解析改為比照 ADR-013 `UacApiEndpointResolver` 直接以 `HttpClient` 存取，移除 `Consul` NuGet 套件參照。
3. **修正 Consul Key 未設定時的不對稱回退邏輯**：先前邏輯為 Consul Key 未設定時仍會以寫死的猜測 Key 查詢 Consul，而非直接回退至本地 Fallback 設定；此行為與「Consul 節點清單未設定時直接回退」不一致，且有查到語意不符資料的風險。修正後範本統一為 Key 未設定時直接略過 Consul 查詢並回退。

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
