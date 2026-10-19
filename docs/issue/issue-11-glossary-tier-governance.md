# Issue #11 - 欄位命名詞彙治理機制（Tier 1 / Tier 2）名實不符與重複定義

> **狀態**：已決議採用方案 A（見第 4 節），具體落實計畫與驗證方式見第 6 節；修復工作進行中。
> **追蹤票**：[#11](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/11)
> **建立日期**：2026-10-19
> **類型**：Architecture / Tech Debt

## 1. 問題描述

[ADR-010](../architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 設計了「雙層詞庫治理體系」：Tier 1（中央核心字典）供跨系統共通欄位命名對齊，Tier 2（各專案 `mcp-glossary.json`）供子系統自行擴充業務詞彙，兩者合併後由 Roslyn 分析器於編譯期提示 `MCP0010`（偵測到歷史別名）。

實際盤點 `McpGateway.Core` 與其下游子系統（`McpGateway.Mes`、`McpGateway.Qsys`、`McpGateway.Report`）後發現，這套治理機制在實作上與文件描述的設計**名實不符**，存在三個互相關聯的子問題：

1. **Tier 2 完全是死碼**：各子系統的 `mcp-glossary.json` 從未被任何分析器讀取。
2. **Tier 1 存在兩份互不同步的重複定義**：`Tier1Vocabulary.cs`（實際生效）與 `CoreGlossary.cs`（內容較完整但從未被呼叫）。
3. **Scaffold 模板與 AI Skill 文件持續再生產上述死碼**：即使清掉現有檔案，下一個用腳手架或 add-subsystem skill 建立的子系統仍會自動長出新的無效 `mcp-glossary.json`。

三者是同一根因——「沒有單一權威來源、也沒有驗證機制確認宣稱的架構是否真的生效」——的三種症狀，故合併記錄於同一份文件。

## 2. 根本原因分析

### 2.1 Tier 2（`mcp-glossary.json`）從未被讀取

- `McpNamingAndDescriptionAnalyzer.cs` 的 `Initialize()` 只註冊 `RegisterSymbolAction`，比對邏輯只呼叫 `Tier1Vocabulary.FindByAlias(...)`，完全不處理 Roslyn 的 `AdditionalFiles`/`AdditionalText`。
- `McpGateway.Core.Glossary.GlossaryLoader`（含 `LoadFromFile`、`MergeGlossaries`）是目前唯一具備「讀取/合併 Tier 2」能力的程式碼，但全專案搜尋後**沒有任何呼叫點**——它是孤兒程式碼。
- 各子系統 `.csproj` 內雖然都有：
  ```xml
  <AdditionalFiles Include="mcp-glossary.json" Condition="Exists('mcp-glossary.json')" />
  ```
  但沒有任何 Analyzer 把它當作 `AdditionalText` 解析使用，所以這行設定目前無實質作用。
- 現存 4 份子系統 `mcp-glossary.json` 中，`McpGateway.Qsys.Issapi` 另外重複定義了 Tier 1 已有的 `productId`，但未帶 `aliases`；若 Tier 2 真的被啟用並依「同名覆蓋」規則合併，會讓該專案的 `productId` 悄悄遺失 Tier 1 原本豐富的別名清單，違反 ADR-010「新增詞彙為純擴充（Non-breaking）」原則——這是一個休眠中、只待 Tier 2 被啟用就會立刻爆發的資料正確性風險。

### 2.2 中央 Tier 1 存在兩份重複、互不同步的定義

| 檔案 | 位置 | 是否被分析器呼叫 | 內容完整度 |
|---|---|---|---|
| `Tier1Vocabulary.cs` | `McpGateway.Analyzers` 專案 | ✅ 是，`McpNamingAndDescriptionAnalyzer` 直接呼叫 `FindByAlias` | 較精簡（約 20 條詞彙，含 `startHours` 等） |
| `CoreGlossary.cs` | `McpGateway.Core.Glossary` 命名空間 | ❌ 否，無任何呼叫點 | 較完整（多了 `workCenter`、`productLine`、`stepId`、`waferId`、`yieldRate`、`quantityInProcess`、`status` 等，對應 ADR-010 文中舉例的詞彙） |

推測成因：依 [ADR-015](../architecture/adr/ADR-015-single-package-analyzers-merge.md)，`McpGateway.Analyzers` 刻意**不透過 `ProjectReference` 依賴 `McpGateway.Core`**（避免 NuGet 打包與 Jenkins `dotnetPkgPipeline` 的 ProjectReference 限制），這很可能是當初有人在 Analyzers 專案內手動複製一份精簡版 `Tier1Vocabulary.cs`、而非直接引用 `CoreGlossary` 的技術原因。但後續沒有任何同步機制或單一測試驗證兩者一致，導致兩份定義逐漸分裂。

這個分裂甚至擴散到 AI Skill 文件本身：[mcp-tool-dto-builder/SKILL.md](../ai-tools/skills/mcp-tool-dto-builder/SKILL.md) 與 [WORKFLOW.md](../ai-tools/skills/mcp-tool-dto-builder/WORKFLOW.md) 的「Step 0 探測策略」P1 階段，同時把 `**/Tier1Vocabulary.cs` 與 `**/CoreGlossary.cs` 都列為搜尋目標——顯示撰寫該文件時，作者本人也無法判斷哪一份才是正本，只好兩個都列進探測清單讓 AI 碰運氣。

### 2.3 Scaffold 模板與 Skill 文件持續再生產死碼

- [templates/template-module/mcp-glossary.json](../../templates/template-module/mcp-glossary.json) 是 `scaffold.ps1`（見 [spec-department-scaffold.md](../specs/spec-department-scaffold.md)）建立新部門/子系統專案時會複製的範本檔，內容只有佔位用的 `exampleField`。
- `McpGateway.Qsys` 的 add-subsystem skill 檢查清單 `.github/skills/add-subsystem/references/integration-checklist.md` 第 37 行明確把 `src/McpGateway.Qsys.<System>/mcp-glossary.json`（詞彙表）列為**必須交付**項目之一。
- 結果：只要不處理這兩個源頭，即使清掉現有的死檔案，下一次有人用腳手架或 add-subsystem skill 建立新子系統時，又會自動長出一份新的無效 `mcp-glossary.json`。

## 3. 影響範圍

| 類別 | 項目 |
|---|---|
| 現存 Tier 2 死碼 | `McpGateway.Mes/src/McpGateway.Mes.Arymes/mcp-glossary.json`、`McpGateway.Qsys/src/McpGateway.Qsys.Umsapi/mcp-glossary.json`、`McpGateway.Qsys/src/McpGateway.Qsys.Issapi/mcp-glossary.json`、`McpGateway.Report/src/McpGateway.Report.Mfg/mcp-glossary.json`，及對應 4 份 `docs/mcp-glossary.schema.json` |
| 中央重複定義 | `McpGateway.Core/src/McpGateway.Analyzers/Tier1Vocabulary.cs`、`McpGateway.Core/src/McpGateway.Core/Glossary/CoreGlossary.cs`、`GlossaryLoader.cs`、`GlossaryTerm.cs`（孤兒程式碼） |
| 持續再生產源頭 | `McpGateway.Core/templates/template-module/`（含其 `.csproj` 的 `AdditionalFiles` 參照）、`McpGateway.Qsys/.github/skills/add-subsystem/references/integration-checklist.md`、`McpGateway.Core/docs/ai-tools/skills/mcp-tool-dto-builder/SKILL.md` 與 `WORKFLOW.md` |
| 受影響文件 | [ADR-010](../architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md)（描述的雙層機制與實作不符，需要澄清或修訂） |

## 4. 候選修復方案（並列優缺點）

> **分析建議（非最終決策，待負責人確認）**：建議採用 **方案 A**。理由：(1) `CoreGlossary.cs`/`GlossaryLoader.cs` 目前零呼叫點、零執行期用例，保留等於維護一份純推測性能力，違反 YAGNI；(2) 不需碰 ADR-015 已拍板、且是為解決真實 CI/打包痛點才做的架構限制，改動風險最小；(3) 現行機制（`Tier1Vocabulary.cs`）本來就是實際生效的那份，選它等於承認現狀、把散落的好詞彙收進來即可。若未來真的出現執行期查詢詞彙的具體需求，屆時應另外設計「從 `Tier1Vocabulary.cs` 單向產生執行期視圖」的機制（例如 source generator），而非恢復兩份手動同步的清單。

### 方案 A：以 `Tier1Vocabulary.cs` 為唯一權威來源

將 `CoreGlossary.cs` 多出的有價值詞彙（`workCenter`、`productLine`、`stepId`、`waferId`、`yieldRate` 等）合併進 `Tier1Vocabulary.cs`，然後刪除 `CoreGlossary.cs`、`GlossaryLoader.cs`、`GlossaryTerm.cs` 及全部 Tier 2 相關檔案與文件引用。

- **優點**：改動範圍小，不需處理 `McpGateway.Analyzers` 對 `McpGateway.Core` 的跨專案依賴問題；維持 ADR-015 已拍板的「Analyzers 不依賴 Core」技術限制。
- **缺點**：`McpGateway.Core.Glossary` 命名空間功能完全移除，若日後有執行期（而非編譯期）需要查詢標準詞彙的情境（例如 API 回傳 schema 說明自動產生），需要重新設計；且等於承認 ADR-010 當初設想的「執行期可查詢的 Core 詞庫」从未真正落地。

### 方案 B：以 `CoreGlossary.cs` 為唯一權威來源，改造 Analyzer 的引用方式

保留較完整的 `CoreGlossary.cs` 作為正本，想辦法讓 `McpGateway.Analyzers` 改用它。同樣刪除全部 Tier 2 相關死碼。

- **優點**：保留內容較完整的詞庫，且名稱（`CoreGlossary`）更符合 ADR-010 文件描述的「中央核心字典」定位。
- **缺點**：需要重新處理 ADR-015 刻意避開的跨專案依賴/打包限制，改動範圍較大，可能牽動 Jenkins pipeline 設定。
- **關鍵限制（編譯期檢查是否仍然生效，取決於實作方式，並非自動成立）**：
  - ✅ **原始碼共用連結**（例如 `<Compile Include="..\McpGateway.Core\Glossary\CoreGlossary.cs" Link="..." />`，讓該原始碼同時被編譯進 `McpGateway.Analyzers.dll` 本身）：可維持編譯期檢查，效果等同方案 A，只是正本檔案位置改變；需額外驗證不會被 `dotnet pack` 誤收錄進一般函式庫輸出。
  - ❌ **組件參考**（`ProjectReference`/`PackageReference` 直接依賴 `McpGateway.Core.dll` 本體）：這正是 ADR-015 刻意避免的做法。Roslyn Analyzer 在編譯器（`csc`/VBCSCompiler）的隔離 Load Context 中執行，而 `McpGateway.Core.dll` 牽連 ASP.NET Core、Redis 等執行期相依套件，這些依賴不會、也不應該被編譯器載入——實務上很可能載入失敗或被忽略，導致 MCP0010 編譯期檢查**失效**，等於繞一圈又回到「Tier 2 式死碼」的同一類問題。
  - 因此若採方案 B，**必須限定用「原始碼連結」而非「組件參考」**，才能保留編譯期檢查這個核心價值；若誤用組件參考，需新增自動化測試或 CI 檢查以確認 `MCP0010` 診斷仍實際觸發，而非僅憑程式碼能編譯通過就視為成功。

> 兩方案共通前提：無論選哪一個，Tier 2（`mcp-glossary.json` 機制）與其再生產源頭（scaffold 模板、add-subsystem checklist、mcp-tool-dto-builder skill 的雙檔名探測）都需要一併清除或修訂，否則問題會再度發生。

## 5. 待決事項

- [ ] 確認是否採用方案 A（分析建議，見上方第 4 節），或選擇方案 B／其他替代方案。
- [ ] 是否需要同步修訂 [ADR-010](../architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 本文，使其描述與實際實作一致（或記錄為「雙層機制已收斂為單層」的架構異動）。
- [ ] 是否需要為「中央詞彙與下游實作一致性」建立自動化測試或 CI 檢查，避免未來再次分裂。
- [ ] **若收斂為單層（僅保留 `Tier1Vocabulary.cs`），子系統真正侷限於單一業務（不具跨系統共通性）的詞彙該歸屬何處？** 例如 Issapi 的 `driftScore`、`rootCause`，或 Umsapi 的 `gpuHours`、`storageGb`。移除 Tier 2 後，這類詞彙將不再有「本地詞庫」可登記，初步選項：
  - (a) 直接寫在該 DTO 屬性自身的 `[Description]` 字面文字裡，不要求比對任何詞庫，僅受 MCP0011（缺 Description 必填）約束，不受 MCP0010（別名提示）約束；
  - (b) 仍放進中央 `Tier1Vocabulary.cs`，但額外標註「侷限範圍」（例如詞彙名稱加上子系統前綴，或在 `Description` 註明適用子系統），放寬「跨系統共通」的收錄門檻；
  - (c) 其他方案。

  **分析建議（非最終決策）**：建議採用 **(a)**。因為 (b) 會讓「子系統可自主擴充、不需等中央發版」這個 Tier 2 原本想解決的真實需求，重新捲土重來變成「什麼詞彙都得經中央 Core repo 發版」的瓶頸；而 MCP0010 別名提示機制本質上是為了解決「同一概念跨系統有多個歷史別名」的問題，盤點到的子系統詞彙（如 `driftScore`、`gpuHours`）大多是單一團隊獨有、沒有跨系統別名分歧，不屬於該機制要解決的範疇，同團隊的 code review 已足以把關內部命名一致性。
- [x] 正式於 Gitea 開立追蹤票 [#11](http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/11)，本文件已更名並補上票號連結。

## 6. 實作計畫與驗證（已採用方案 A）

本節為方案 A 的具體落實清單，依 repo 分組；每個 repo 是獨立 Git 專案，將各自產生獨立 commit/PR。每一組異動完成後，以「驗證」欄列出的方式確認無遺漏、無破壞既有行為。

### 6.1 McpGateway.Core（治理機制本體）

| # | 待辦事項 | 驗證方式 |
|---|---|---|
| 1 | 比對 `CoreGlossary.cs` 與 `Tier1Vocabulary.cs`，將前者獨有的詞彙（`workCenter`、`productLine`、`stepId`、`stepName`、`waferId`、`quantityInProcess`、`quantityCompleted`、`totalItems`、`yieldRate`、`defectCount`、`status`、`priority`、`scheduledCompletion`、`generatedAt`）合併進 `Tier1Vocabulary.cs`；`processId` 與 `stepId` 的別名歸屬衝突需人工判斷取捨，而非直接複製 | 逐條核對合併後的 `Tier1Vocabulary.cs` 不漏詞、`processId` 衝突已有明確取捨並在程式碼註解說明原因 |
| 2 | 刪除 `src/McpGateway.Core/Glossary/CoreGlossary.cs`、`GlossaryLoader.cs`、`GlossaryTerm.cs` | 全文搜尋 `CoreGlossary`、`GlossaryLoader`、`GlossaryTerm` 於整個 Core repo（含測試、文件）應為 0 筆殘留引用 |
| 3 | 刪除 `templates/template-module/mcp-glossary.json`、`templates/template-module/docs/mcp-glossary.schema.json`，並移除對應 `.csproj` 的 `<AdditionalFiles Include="mcp-glossary.json" .../>` | 以 `scaffold.ps1` 建立一個測試用新子系統，確認產出目錄不再含 `mcp-glossary.json` |
| 4 | 編輯 `mcp-tool-dto-builder/SKILL.md`、`WORKFLOW.md`：Step 0 探測清單移除 `CoreGlossary.cs`，只保留 `Tier1Vocabulary.cs`；移除提及 Tier 2 `mcp-glossary.json` 的段落 | 人工檢閱兩份文件不再出現 `CoreGlossary`／Tier 2 字樣 |
| 5 | `dotnet build` 整個 Core 方案（含 `McpGateway.Analyzers`、`McpGateway.Core`） | 建置成功、零警告/錯誤新增 |
| 6 | 執行 `dotnet test`（`McpGateway.Analyzers.Tests` 等現有測試） | 全數通過；若現有測試未涵蓋 MCP0010（別名提示），建議新增至少一條回歸測試，對合併後新加入的詞彙（如 `workCenter`）故意使用舊別名觸發 `MCP0010`，確認診斷仍正確觸發 |
| 7（選擇性） | 視待決事項結論，更新 [ADR-010](../architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 文字，標註雙層機制已收斂為單層 | 人工審閱 ADR 內容與實作一致 |

### 6.2 McpGateway.Mes

| # | 待辦事項 | 驗證方式 |
|---|---|---|
| 1 | 刪除 `src/McpGateway.Mes.Arymes/mcp-glossary.json`（僅佔位 `exampleField`，無資訊損失）與對應 `docs/mcp-glossary.schema.json`，移除 `.csproj` 的 `AdditionalFiles` 參照 | `dotnet build` Mes 方案成功；全文搜尋確認無殘留引用 |

### 6.3 McpGateway.Qsys

| # | 待辦事項 | 驗證方式 |
|---|---|---|
| 1 | 核對 `Issapi` 的 `mcp-glossary.json`（`shop`、`planId`、`driftScore`、`rootCause`、`erpId`、`mtrlProductId`、`usageQty`、`uomCd` 等）是否已在對應 DTO（`DriftSummaryDto.cs`、`RootCauseDetailDto.cs` 等）有相同說明的 `[Description]`；若有缺漏，先把缺漏的說明文字補進 DTO 屬性 | 逐一欄位比對 JSON 內容與 DTO `[Description]`，確認無資訊遺失 |
| 2 | 刪除 `Umsapi/mcp-glossary.json`（已確認 `ReportDtos.cs` 已有對應 `[Description]`）與 `Issapi/mcp-glossary.json`（待 #1 補齊後），含兩者的 `docs/mcp-glossary.schema.json`，並移除各自 `.csproj` 的 `AdditionalFiles` 參照 | `dotnet build` Qsys 方案成功；全文搜尋確認無殘留引用 |
| 3 | 編輯 `.github/skills/add-subsystem/references/integration-checklist.md`（及 `.gemini` 鏡像版本）：移除「`mcp-glossary.json` 為必須交付項目」該列 | 人工檢閱兩份 checklist 不再要求 `mcp-glossary.json` |

### 6.4 McpGateway.Report

| # | 待辦事項 | 驗證方式 |
|---|---|---|
| 1 | 刪除 `src/McpGateway.Report.Mfg/mcp-glossary.json`（僅佔位 `exampleField`）與對應 schema，移除 `.csproj` 的 `AdditionalFiles` 參照 | `dotnet build` Report 方案成功；全文搜尋確認無殘留引用 |

### 6.5 跨 repo 共通驗證（全部異動完成後）

- 對 4 個 repo 分別跑一次 `dotnet build`（必要時含 `dotnet test`），確認皆無錯誤。
- 以 `git grep -n "mcp-glossary"`／`CoreGlossary`／`GlossaryLoader` 掃過 4 個 repo 的工作目錄，確認僅剩歷史文件（如本文件、ADR-010）中的敘述性提及，原始碼與設定檔不再有殘留。

### 6.6 後續待辦（2026-10-19 發現，暫緩）

McpGateway.Core 升級至 `0.2.2-preview`（含本次詞彙合併）後，`McpGateway.Qsys.Issapi` 重新編譯浮現 **10 個新的 MCP0010 警告**：因為 `totalItems`、`generatedAt`（原 Tier 2 死碼內的詞彙，這次合併進 Tier1Vocabulary.cs 才首次被分析器實際掃到）成為標準詞彙，回溯標記出既有欄位命名不符規範：

| 檔案 | 欄位 | 建議標準名稱 |
|---|---|---|
| `Issapi/Models/BplanJobFullDto.cs:41` | `StartTime` | `startHours` |
| `Issapi/Models/GurobiUsageDto.cs:21` | `StartTime` | `startHours` |
| `Issapi/Models/GurobiInfeasibleOutDto.cs:13` | `TotalCount` | `totalItems` |
| `Issapi/Models/QueryMaterialDtos.cs:39` | `TotalCount` | `totalItems` |
| `Issapi/Models/GurobiInfeasibleItemDto.cs:57` | `CreateTime` | `generatedAt` |

**暫緩原因**：改欄位名稱可能影響下游呼叫端（需另外評估相容性/棄用期）；決定先不處理，待本次單層詞彙治理（Issue #11）主線完成後另案評估。
- 在一個現有或臨時建立的工具上，刻意使用一個已知的歷史別名（例如 `prodId`）觸發 `MCP0010`，確認合併後的 `Tier1Vocabulary.cs` 仍正確提示，證明本次收斂未削弱既有編譯期檢查能力。

### 6.7 新增搬送/倉儲位置領域詞彙（2026-10-19，前瞻性收錄）

處理 `McpGateway.Mes` 的 §6.2 時發現 AryMes 子系統使用的 `zone_id`／`shelf_id`／`pati_id`／`position` 等欄位，描述的是「載具在自動倉儲 (AMHS) 中的實體位置階層」，屬於搬送領域的共通概念，而非 AryMes 專屬的傳輸封包欄位（如 `trx_id`／`type_id`／`rtn_code`，這些維持 Tier 2 本地描述，不收錄）。

鑑於後續規劃中的 `CFMES`、`LcdMes` 子系統預期會管理同一套搬送領域概念（僅各自命名可能不同），經負責人確認後決定提前將下列標準名稱收錄進 `Tier1Vocabulary.cs`，避免日後各子系統各自發明命名、產生需要回溯清理的別名：

| 下游欄位 (AryMes) | 標準名稱 (Tier 1) | 說明 |
|---|---|---|
| `zone_id` | `zoneId` / `ZoneId` | 自動倉儲區域代碼 (AMHS Zone ID) |
| `shelf_id` | `shelfId` / `ShelfId` | 貨架層位代碼 (Shelf/Rack Position ID) |
| `pati_id` | `patiId` / `PatiId` | 放置位置代碼 (Placement/Slot Address ID) |
| `position` | `position` / `Position` | 載具/面板目前位置代碼 (Current Item Position) |

`port_id`（裝卸埠）已有既有詞彙 `portId`，不重複新增。

## 7. 相關文件

- [ADR-010: MCP Tool 欄位命名標準化、結構化 DTO 回傳與編譯期分析器治理](../architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md)
- [ADR-015: Core 單一套件合併發布 Analyzers](../architecture/adr/ADR-015-single-package-analyzers-merge.md)
- [mcp-tool-dto-builder SKILL.md](../ai-tools/skills/mcp-tool-dto-builder/SKILL.md)
- [mcp-tool-dto-builder WORKFLOW.md](../ai-tools/skills/mcp-tool-dto-builder/WORKFLOW.md)
- [GLOSSARY.md](../../GLOSSARY.md)（本次分析新增之詞彙：Tier 1 詞彙、Tier 2 詞彙、詞彙條目、別名、詞彙權威來源）
