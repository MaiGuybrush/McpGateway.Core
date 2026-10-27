# ADR-010: MCP Tool 欄位命名標準化、結構化 DTO 回傳與編譯期分析器治理

## 狀態
**✅ 已採用 (Accepted)**（2026-08-17）

> **修訂紀錄（2026-10-19）**：原第 2 支柱「雙層詞庫治理體系」已依 [Issue #11](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/11) 收斂為單層架構。實際盤點發現 Tier 2（`mcp-glossary.json`）機制從未被任何分析器讀取，屬於未落地的死碼；下方第 2 支柱內容已改寫為反映此現狀，原始設計構想保留於 Issue #11 的分析文件中。

> **修訂紀錄（2026-10-22）**：依 [Issue #14](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/14) 於第 2、3 支柱新增兩條治理原則：「詞條描述須含語意邊界」與「診斷訊息須送達詞條描述」。成因：詞條 `startHours`（持續時數）曾把 `startTime`（時刻）列為別名，`MCP0010` 據此對下游時刻欄位提出錯誤改名建議，且警告訊息不含任何語意資訊，開發者無從判斷是否套用。

> **修訂紀錄（2026-10-22）**：本 ADR 的標準化範圍原止於 C# 原始碼層；Agent 實際讀到的輸出 JSON 鍵由 `[JsonPropertyName]` 決定，該層的契約分層（鏡射契約止於 Service、輸出契約禁 `JsonPropertyName`）與新診斷 `MCP0013` 由 [ADR-016](ADR-016-agent-layer-output-contract-standardization.md)（[Issue #15](../../issue/issue-15-output-contract-agent-layer-standardization.md)）決定，為本 ADR 支柱 1/3 的補完，無牴觸。

---

## 背景 (Context)

隨著企業內部多個業務系統與部門（如製造執行系統 MES、設備自動化 EAP、報表系統 Report、統計製程管制 SPC、品管 QC 等）陸續建置各自的 MCP Gateway，我們觀察到以下嚴重阻礙 AI Agent 生態發展的痛點：

1. **命名歧異增加 LLM 幻覺與 Token 浪費**：
   * 各系統對於相同的業務概念使用相異的欄位名稱（例如：產品 ID 在 Gateway A 叫 `ProductCode`、Gateway B 叫 `prod_id`、Gateway C 叫 `materialNo`、Gateway D 叫 `PROD_ID`）。
   * 當 AI Agent 執行跨系統複合任務（Tool Chaining，例如「*查詢 WIP 異常工單並比對該機台最近的警報紀錄*」）時，LLM 必須耗費大量 Context Token 進行欄位推論與猜測，大幅增加對齊失敗或推理錯誤的機率。
2. **純文字/Markdown 回傳限制了下游程式與 Agent 運算能力**：
   * 既有 MCP Tool（如 WIP 在製品查詢）以 Markdown 字串回傳資料，雖然便於人類直接檢視，但 **LLM 無法進行精確過濾、排序、聚合計算或交由自動化程式進一步處理**。
3. **缺乏欄位說明標準**：
   * 開發人員容易遺漏輸出欄位的語意註解（Description），導致 MCP Tool 生成的 Output Schema 缺乏明確業務語意。
4. **硬性阻斷（Hard Error）推行阻力過大**：
   * 各業務單位後端資料庫具有歷史命名包袱，若在編譯時期採強制錯誤阻斷，將造成開發者強烈抗拒與相容性斷裂。

---

## 決策 (Decision)

我們決定**推行跨 Gateway 統一欄位命名標準化、結構化 DTO 回傳，並引入「提示型（Warning/Info）+ 一鍵修復（CodeFix）」的 Roslyn 分析器與啟動驗證防禦機制**。

> **核心架構定位**：  
> **這項工作是 MCP Gateway 從「各自為政的 API 包裝器」升級為「企業級 Agent 工具生態系」的關鍵分水嶺。**  
> 沒有統一的語意規範與結構化契約，多個 MCP Gateway 只是散落的資料孤島；有了統一的資料治理，AI Agent 才能具備自主跨工具協同（Multi-Agent Collaboration）與精確推論的能力。

具體決策包含以下四大支柱：

### 1. 全面淘汰 Markdown，回傳強型別不可變 DTO (Structured Content)
* 淘汰純文字 Markdown 拼接字串回傳，所有 Tool 統一回傳不可變強型別 Record DTO。
* 標註 `[McpServerTool(UseStructuredContent = true)]`，使 ModelContextProtocol SDK 自動掃描 DTO 屬性上的 `[property: Description("...")]` 並在 `tools/list` 暴露標準 JSON Schema（`outputSchema`）。

### 2. 建立單層核心詞庫治理體系 (Core Domain Vocabulary)
* 由中央 `McpGateway.Core`（實作於 `McpGateway.Analyzers/Tier1Vocabulary.cs`）集中維護製造業/半導體/面板共通概念（如 `productId`, `eqptId`, `subEqptId`, `workCenter`, `productLine`, `stepId`, `lotId`, `workOrderId`, `glassId`, `panelId`, `waferId`, `quantityInProcess`, `yieldRate`, `status` 等）及其歷史同義別名（Aliases），為唯一權威來源。
* 子系統專屬、不具跨系統共通性的業務詞彙（如 `driftScore`、`gpuHours`），不再透過獨立詞庫宣告，而是直接寫在對應 DTO 屬性的 `[property: Description("...")]` 字面文字中，僅受 `MCP0011`（缺 Description 必填）約束。
* **詞庫演進原則**：新增詞彙為純擴充（Non-breaking）；舊詞淘汰採 Deprecation 提示，不阻斷既有專案編譯。
* **詞條描述須含語意邊界（Issue #14）**：詞條 `Description` 是 AI Skill 產生 DTO `[Description]` 與開發者判斷改名的唯一語意依據，不能只定義「一個詞是什麼」。易混淆群（時間類：持續時數/時刻/日期/時間戳記互斥；計數主體；數量 vs 比率；設備/載具/位置階層；`processId` vs `stepId`）的描述必須採三段式——定義＋辨析（「若欄位為 X 語意請用 Y」）＋格式範例；無近義詞的詞條維持一句話定義即可。名稱佔用的唯一性（別名不得撞標準名、不得由兩條詞條共享）由 `Tier1VocabularyConsistencyTests` 於 CI 強制，不靠人工註解。撰寫規範詳見 [CONTRACT_RULES.md §2.1](../../ai-tools/skills/mcp-tool-dto-builder/CONTRACT_RULES.md)。
* > 原規劃中曾設計「Tier 2 專案本地字典」（各專案自建 `mcp-glossary.json` 自主擴充、不需等待中央發版），但該機制從未被任何分析器實際讀取，屬於未落地的設計；詳見 [Issue #11](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/11) 之分析與收斂決議。

### 3. 開發期 Roslyn 分析器與一鍵修復 (`McpGateway.Analyzers`)
* 建立獨立的 Roslyn DiagnosticAnalyzer 套件，於 Visual Studio / VS Code 即時提示：
  * **`MCP0010` (Warning)**：偵測到已知別名（如 `ProductCode`），提示「建議更換為標準名稱 `productId`」。
  * **`MCP0011` (Warning)**：偵測到 DTO 公開屬性遺漏 `[Description]`，提醒為 LLM 補齊中文語意說明。
  * **`MCP0012` (Info)**：檢查是否符合 CamelCase 命名約定。
* **診斷訊息須送達詞條描述（Issue #14）**：`MCP0010` 本質上是需要人判斷的語意提議（分析器只看名稱，無法驗證欄位實際語意），因此警告與 Code Fix 標題必須完整附帶詞條 `Description`（含辨析句），讓開發者/Agent 在判斷現場就有依據自行否決錯誤建議（例：時刻欄位被建議改為時數詞時應忽略），而非只能照抄改名。三條診斷與 Code Fix 標題以繁體中文呈現，與詞條描述語言一致；診斷 ID 不變，`#pragma warning disable` 與 `[SuppressMessage]` 壓制機制不受影響。
* 提供 `CodeFixProvider` 支援 IDE 燈泡一鍵自動更名與補齊 `[property: Description]` 範本。
* 支援標準 `#pragma warning disable` 與 `[SuppressMessage]` 壓制特定業務例外。

### 4. 啟動期驗證器遞迴防禦 (`ToolStartupValidator`)
* 擴充 `ToolStartupValidator`（Rule #7），於 Gateway 啟動時反射掃描註冊 Tool 的 DTO 物件圖譜（遞迴至清單項目與巢狀物件）。
* 若發現遺漏 Description 僅在日誌輸出 Warning Log，不阻擋伺服器啟動。

---

## 影響與後果 (Consequences)

### 正面影響 (Positive)
1. **跨工具鏈接能力（Tool Chaining）大幅躍升**：跨 Gateway 的實體識別碼一致，LLM 能夠以零推理成本直接進行工具間的參數傳遞與串接。
2. **結構化二次運算支援**：前端、Agent 與下游自動化流程可以直接針對 DTO 欄位進行過濾、排序、統計運算與圖表生成。
3. **平衡規範與開發彈性**：採 Warning + CodeFix 模式，為開發者鋪平道路（Paved Road），既能導正歷史壞味道，又不會阻礙新業務的快速迭代。
4. **全樹狀結構語意透明度**：深層巢狀物件皆具備 Description，確保 LLM 在解析明細清單（`items`）時具備完整的語意理解。

### 負面影響 / 代價 (Trade-offs & Mitigation)
1. **專案建置依賴增加**：各 Gateway 專案需引用 `McpGateway.Analyzers`。
   * *緩解*：Analyzer 設定為 `OutputItemType="Analyzer"` 且 `ReferenceOutputAssembly="false"`，不會增加產出二進位體積或 Runtime 依賴。
2. **DTO 撰寫門檻略微提升**：開發者需要為每個公開屬性撰寫 `[Description]`。
   * *緩解*：透過 CodeFix 燈泡一鍵插入範本，且要求寫中文說明比記住英文命名容易得多。

---

## 相關參考文件 (References)
* [Issue #14: 詞彙庫缺乏語意邊界，且詞條描述未送達命名判斷現場](../../issue/issue-14-vocabulary-semantic-boundary.md)（2026-10-22 修訂的完整分析與決議）
* [ADR-003: 描述文字提供機制 (內聯屬性模式)](ADR-003-config-driven-descriptions.md)
* [ADR-004: 啟動時驗證機制](ADR-004-startup-validation.md)
* [ADR-009: 多部門 Gateway 拆分架構](ADR-009-department-gateway-split.md)
* 規格需求書：`McpGateway.Report/docs/SPEC-MCP-TOOL-FIELD-STANDARDIZATION.md`
* 開發者手冊：`McpGateway.Report/docs/DEVELOPER-GUIDE-FIELD-STANDARDIZATION.md`
