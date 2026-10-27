# McpGateway.Core

McpGateway.Core 提供跨部門 MCP Gateway（Report、MES、EAP、SPC、QC 等）共用的執行期基礎設施、Roslyn 分析器，以及企業級欄位命名詞彙治理機制（ADR-010）。

## Language

**Tier 1 詞彙（Core Domain Vocabulary）**:
跨系統共通的標準欄位命名與其歷史別名定義，由中央團隊於 McpGateway.Core 集中維護，供所有子系統對齊（如 `productId`、`eqptId`、`lotId`）。
_Avoid_: 全域字典、Core 詞彙庫、中央字典

**Tier 2 詞彙（已淘汰，Deprecated as of Issue #11）**:
原設計為各子系統專案自行擴充的業務特有詞彙，透過專案根目錄的 `mcp-glossary.json` 宣告。經 [issue-11-glossary-tier-governance.md](docs/issue/issue-11-glossary-tier-governance.md) 分析確認此機制從未被任何分析器實際讀取，已於方案 A 收斂中移除；子系統專屬詞彙改為直接寫在對應 DTO 屬性的 `[Description]` 字面文字。
_Avoid_: 本地字典、專案詞彙庫

**詞彙條目（Glossary Entry / Term）**:
單一標準欄位命名的定義單位，包含標準名稱（camelCase/PascalCase）、中文說明與歷史別名清單。
_Avoid_: 詞彙項目

**別名（Alias）**:
已知但不建議繼續使用的歷史欄位命名；當分析器於程式碼中偵測到別名時，會提示開發者改用對應詞彙條目的標準名稱（MCP0010）。

**詞彙權威來源（Vocabulary Authority）**:
詞彙治理機制中，實際被下游工具（如 Roslyn 分析器）讀取並生效的詞彙定義來源。多份文件或程式碼宣稱提供同一層級的詞彙時，必須明確指定其中一份為權威來源，其餘應視為衍生文件或刪除。

## Tool 契約（Tool Contracts）

**鏡射契約（Mirror Contract）**:
跟隨上游系統 JSON 鍵名、僅存在於 Service 層內部的資料契約；對下游負責，不對 Agent 暴露。
_Avoid_: 鏡射 DTO、上游模型、raw DTO

**輸出契約（Output Contract）**:
Gateway 對 Agent 承諾的欄位契約；其欄位名即 Agent 讀到的鍵名，一律使用 Tier 1 詞彙的標準詞。
_Avoid_: 輸出 DTO、回應模型

**契約映射（Contract Mapping）**:
Service 層將鏡射契約的欄位逐項轉換為輸出契約欄位的轉換步驟；是下游方言成為標準詞的唯一發生點。
_Avoid_: 欄位轉換、DTO 轉換（與「別名」概念區隔）
