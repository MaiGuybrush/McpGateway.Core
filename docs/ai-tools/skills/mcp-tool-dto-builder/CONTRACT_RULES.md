# MCP Tool DTO 結構化契約設計規範 (Structured DTO Contract Rules)

本文件定義 MCP Tool Input 與 Output DTO 的 C# 實作標準、型別慣例與 Roslyn 分析器合規性要求。

---

## 1. 核心設計原則

1. **強型別不可變性 (Immutability)**：
   - 所有的 Request Input 與 Response Output DTO 一律宣告為 `public sealed record`。
   - 使用 Positional Record 語法（主要建構子語法），確保執行期不可變且語法簡潔。

2. **完整語意透明度 (Description on Every Property)**：
   - 每個公開屬性皆必須標註 `[property: Description("繁體中文語意說明與格式範例")]`。
   - 工具主體 DTO 類別上方亦應標註 `[Description("工具功能摘要與適用場景")]`。
   - 巢狀物件、清單明細項目（`ItemDto`）的所有屬性同樣必須逐一標註 Description。

3. **結構化回傳啟用 (Structured Content)**：
   - MCP Tool 實作方法必須標註 `[McpServerTool(UseStructuredContent = true)]`。
   - 確保 ModelContextProtocol SDK 能夠將 DTO 轉換為標準 JSON Schema 並在 `tools/list` 暴露 `outputSchema`。

4. **大小寫與格式約定 (Naming Conventions)**：
   - **C# Property 名稱**：一律使用標準 `PascalCase`（例如 `ProductId`, `QuantityInProcess`）。
   - **C# Parameter / JSON 欄位**：一律使用標準 `camelCase`（例如 `productId`, `workOrderId`），確保跨 Gateway 命名風格一致無底線。

---

## 2. Roslyn 分析器規範對照

| 診斷代碼 | 嚴重性 | 觸發條件 | 修復方式 |
| :--- | :--- | :--- | :--- |
| **`MCP0010`** | Warning | 使用了已知歷史別名（如 `ProductCode`, `prodId`, `wipQty`） | 先核對警告中附帶的詞條語意邊界與欄位實際值；語意相符才更名為標準詞（如 `ProductId`, `QuantityInProcess`），語意不符（如時刻欄位被建議改為時數詞）應忽略警告 |
| **`MCP0011`** | Warning | DTO 公開屬性遺漏 `[Description]` 特性 | 補齊繁體中文業務描述與格式範例 |
| **`MCP0012`** | Info | 屬性或參數包含底線（如 `PROD_ID`, `work_order`） | 調整為標準 CamelCase / PascalCase |
| **`MCP0013`** | Info（預設 suggestion，不擋建置） | 輸出契約（tool 回傳可達閉包內的 public 型別）屬性標註 `[JsonPropertyName]` | 移除 `JsonPropertyName`，輸出鍵由 C# 屬性名 camelCase 推導；鏡射契約改標 `internal` 並止於 Service 層（見 §2.2） |

> `MCP0010` 警告訊息自 Core `0.2.4-preview` 起會完整附帶詞條 `Description`，並以繁體中文呈現；`MCP0011`/`MCP0012` 訊息亦已中文化。診斷 ID 不變，`#pragma warning disable` 與 `.editorconfig` 設定不受影響。
> `MCP0013` 自 Core `0.2.5-preview` 起提供（[ADR-016](../../architecture/adr/ADR-016-agent-layer-output-contract-standardization.md)）。過渡期預設 severity 為 suggestion；各下游專案在遷移完成的 PR 中以 `.editorconfig` 將 `MCP0013` 收為 `warning`，遷移一批收一批。

---

## 2.2 鏡射/透傳契約規則（ADR-016 / Issue #15）

**問題**：分析器檢查的是 C# 符號名，但 Agent 實際讀到的 JSON 鍵由 `[JsonPropertyName]` 決定。若 tool 直接回傳帶 `JsonPropertyName` 的下游鏡射型別（單層透傳），即使 C# 屬性已改為標準詞，Agent 讀到的仍是下游方言——編譯期綠燈，目標層卻沒有標準化。

**規則 R1（絕對，無 tool 級豁免）**：

1. **輸出契約**（tool 回傳型別及其可達閉包內的 public 型別）**不得標註 `[JsonPropertyName]`**。輸出 JSON 鍵一律由 C# 屬性名的 camelCase 推導 → 詞庫標準詞直接成為 Agent 可見鍵名。
2. **鏡射契約**（跟隨下游 JSON 鍵的型別）必須標 `internal`，只准出現在 Service 層內部（`[JsonPropertyName]` 只准出現在這裡）。
3. 下游方言 → 詞庫標準詞的轉換只在 **契約映射**（Service 層 `Map*()`）發生一次。
4. 鏡射契約的 C# 屬性名仍受 `MCP0010`/`MCP0012` 約束（命名紀律對所有代碼有效，含鏡射契約本身）。
5. 下游來源欄名寫進輸出契約的 `[Description]`（如「下游欄位 `dpiqcrrxo.STAY_HRS`」），對照能力由描述承載。

**三層形態範本**（以 Qsys Umsapi 為範本）：

```csharp
// ① 鏡射契約：internal、跟隨下游 JSON 鍵，止於 Service 層（放 Contracts/）
internal sealed class UpstreamGurobiInfeasibleResult
{
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("createTime")] public string CreateTime { get; set; } = string.Empty;
}

// ② 契約映射（唯一允許出現「鏡射 → 輸出」轉換的地方，在 Service 層）
private static GurobiInfeasibleResponse MapResult(UpstreamGurobiInfeasibleResult u) => new(
    TotalItems: u.TotalCount,      // 下游方言 → 詞庫標準詞
    GeneratedAt: u.CreateTime
);

// ③ 輸出契約：public sealed record、無 JsonPropertyName、欄位 = 詞庫標準詞（放 Dtos/）
public sealed record GurobiInfeasibleResponse(
    [property: Description("符合查詢條件的項目總筆數…")] int TotalItems,
    [property: Description("報告/資料產出之 UTC 時間戳記…下游欄位 createTime")] DateTimeOffset? GeneratedAt
);
```

**目錄佈局約定**（導航輔助，非判定權威——判定權威僅有 tool 回傳可達性追蹤）：

```
Contracts/   ← ① 鏡射契約：全部 internal（[JsonPropertyName] 只准出現在這裡）
Dtos/        ← ③ 輸出契約：public，欄位 = 詞庫標準詞（禁止 [JsonPropertyName]）
Services/    ← ② 映射發生地（Map*()）
Tools/
```

**結構性豁免**（MCP0013 不觸發）：`internal` 型別；從 tool 回傳線路不可達的型別（設定鏡射如 Consul KV、Service 內部型別）；Core / Analyzers 組件定義的型別（外部系統契約）。

**單層透傳＝反模式**：「忠實鏡射」的忠實指資料完整性（欄全可空、值不加工），與鍵名無關；兩層化可完整保留資料完整性，同時讓 Agent 讀到標準詞。欄位裁剪允許逐 tool 有理由進行（token 收益），但決定必須記錄於遷移 PR 的欄位對齊表。

完整背景與決議見 [Issue #15](../../issue/issue-15-output-contract-agent-layer-standardization.md) 與 [ADR-016](../../architecture/adr/ADR-016-agent-layer-output-contract-standardization.md)。

---

## 2.1 詞條描述撰寫規範（Tier 1 詞庫貢獻時必讀）

`Tier1Vocabulary.cs` 詞條的 `Description` 是 AI Skill 產生 DTO `[Description]` 與開發者判斷改名的**唯一語意依據**，撰寫規範為三段式（定義＋邊界＋範例）：

```
<定義>。<邊界：與哪個近義詞不同、如何判斷>。<範例>
```

- **必須寫邊界**的易混淆群（新增/修改這些詞條時，邊界段不可省略）：
  時間（時數/時刻/日期/時間戳記互斥）、計數主體（`lotCnt`/`sheetCnt`/`panelCnt`/`totalItems`）、數量與良率（絕對數量 vs 比率）、設備階層（`eqptId`→`subEqptId`→`chamberId`/`portId`）、載具與生產對象（`carrierId`/`lotId`/`glassId`/`panelId`/`waferId`）、AMHS 位置階層（`zoneId`→`shelfId`→`patiId`）、站點與產線（`processId` vs `stepId`）。
- **無近義詞**的詞條（如 `productId`）維持一句話定義＋範例即可，不硬湊三段。
- 邊界句應採用「若欄位為 X 語意請用 Y」的可操作形式，讓讀者能據以自行判斷，而非只描述本詞。
- 詞條必須歸入檔內對應主題段落（分段順序即 AI 比對順序，近義詞須同段）。
- 名稱唯一性（別名不得撞標準名、不得兩條共享）由 `Tier1VocabularyConsistencyTests` 於 CI 強制，提 PR 前請先本地跑 `dotnet test`。

完整背景與決議見 [Issue #14](../../issue/issue-14-vocabulary-semantic-boundary.md)。

---

## 3. 標準 DTO 實作範本

### (1) Input DTO 範本

```csharp
using System.ComponentModel;

namespace McpGateway.Report.Tools.Report.Models;

/// <summary>
/// 查詢在製品 (WIP) 報表輸入參數
/// </summary>
[Description("查詢在製品 (WIP) 報表輸入條件，支援依工作中心、線別或產品料號過濾。")]
public sealed record QueryWipInput(
    [property: Description("生產工作中心代碼 (Work Center)，例如 WC-01")]
    string? WorkCenter = null,

    [property: Description("生產線別代碼 (Product Line)，例如 LINE-A")]
    string? ProductLine = null,

    [property: Description("產品料號或唯一代碼 (Product ID)，例如 P-1002")]
    string? ProductId = null,

    [property: Description("每頁最大筆數，預設 50，最大 200")]
    int Limit = 50
);
```

### (2) Output DTO 範本（含巢狀清單與摘要）

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace McpGateway.Report.Tools.Report.Models;

/// <summary>
/// 在製品 (WIP) 報表結構化回傳結果
/// </summary>
[Description("在製品 (WIP) 報表結構化回傳結果，包含工單明細清單與整體統計摘要。")]
public sealed record WipReportResponse(
    [property: Description("符合查詢條件的項目總筆數")]
    int TotalItems,

    [property: Description("在製品工單明細清單")]
    IReadOnlyList<WipItemDto> Items,

    [property: Description("在製品數量統計摘要")]
    WipSummaryDto Summary,

    [property: Description("報告/資料產出之 UTC 時間戳記")]
    DateTime GeneratedAt
);

/// <summary>
/// 在製品工單明細項目
/// </summary>
[Description("單筆在製品工單明細資料")]
public sealed record WipItemDto(
    [property: Description("工單編號 (Work Order ID)")]
    string WorkOrderId,

    [property: Description("產品料號或唯一代碼 (Product ID)")]
    string ProductId,

    [property: Description("產品品名或規格名稱")]
    string ProductName,

    [property: Description("生產工作中心代碼 (Work Center)")]
    string WorkCenter,

    [property: Description("目前在製中數量（尚未完工）")]
    int QuantityInProcess,

    [property: Description("目前狀態代碼或說明（如: In Progress, Pending）")]
    string Status,

    [property: Description("預計完工日期時間 (UTC)")]
    DateTime? ScheduledCompletion
);

/// <summary>
/// 在製品統計摘要
/// </summary>
[Description("在製品數量與批數統計摘要")]
public sealed record WipSummaryDto(
    [property: Description("總在製數量加總")]
    int TotalQuantityInProcess,

    [property: Description("在製工單總批數")]
    int TotalWorkOrders
);
```

### (3) Tool 類別宣告範本

```csharp
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.NET.Server.Context;
using ModelContextProtocol.NET.Server.Features.Tools;
using McpGateway.Report.Tools.Report.Models;

namespace McpGateway.Report.Tools.Report;

[McpServerToolType]
public class QueryWipTool
{
    [McpServerTool(UseStructuredContent = true)]
    [Description("查詢在製品 (WIP) 報表，取得特定工作中心或線別的在製工單明細與數量統計。")]
    public async Task<WipReportResponse> ExecuteAsync(
        QueryWipInput input,
        McpServerRequestContext context,
        CancellationToken cancellationToken = default)
    {
        // 業務邏輯實作...
    }
}
```
