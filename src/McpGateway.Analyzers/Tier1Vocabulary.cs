using System;
using System.Collections.Generic;
using System.Linq;

namespace McpGateway.Analyzers;

internal class GlossaryEntry
{
    public string StandardCamel { get; }
    public string StandardPascal { get; }
    public string Description { get; }
    public string[] Aliases { get; }

    public GlossaryEntry(string standardCamel, string standardPascal, string description, string[] aliases)
    {
        StandardCamel = standardCamel;
        StandardPascal = standardPascal;
        Description = description;
        Aliases = aliases;
    }
}

internal static class Tier1Vocabulary
{
    public static readonly GlossaryEntry[] Entries = new[]
    {
        // 實體識別碼
        new GlossaryEntry("productId", "ProductId", "產品料號或唯一代碼 (Product ID)", new[] { "productCode", "prodId", "itemNo", "partNo", "materialNo", "PROD_ID" }),
        new GlossaryEntry("productName", "ProductName", "產品品名或規格名稱", new[] { "prodName", "itemDesc", "PROD_DESC", "PROD_NAME" }),
        new GlossaryEntry("eqptId", "EqptId", "主設備/機台識別碼 (Equipment ID)", new[] { "equipmentId", "machineId", "toolId", "machNo", "eqpId", "EQP_ID" }),
        new GlossaryEntry("subEqptId", "SubEqptId", "子設備/Unit/Chamber機台 ID", new[] { "subEquipmentId", "subMachineId", "unitId", "subToolId" }),
        new GlossaryEntry("chamberId", "ChamberId", "子機台/Chamber", new[] { "chId", "chamberNo", "slotId" }),
        new GlossaryEntry("portId", "PortId", "裝卸埠/傳載埠代碼", new[] { "port_id", "portNo", "lpId" }),
        new GlossaryEntry("recipeId", "RecipeId", "設備配方/製程程式代碼", new[] { "recipeName", "ppId", "recipeCode" }),
        new GlossaryEntry("lotId", "LotId", "生產批號", new[] { "lotNo", "batchId", "batchNo", "LOT_ID" }),
        new GlossaryEntry("workOrderId", "WorkOrderId", "工單編號", new[] { "wo", "woId", "moId", "orderNo", "shopOrder", "WO_ID" }),
        new GlossaryEntry("carrierId", "CarrierId", "載具/晶舟/卡匣編號/CST ID/cassette ID", new[] { "foupId", "cassetteId", "cstId", "carrierNo" }),
        new GlossaryEntry("glassId", "GlassId", "面板玻璃基板 ID", new[] { "sheetId", "substrateId", "panelGlassId", "GLASS_ID" }),
        new GlossaryEntry("panelId", "PanelId", "切割後面板/合板 ID", new[] { "pnlId", "subPanelId", "unitPanelId", "PANEL_ID" }),
        // 註：CoreGlossary.cs 將 processId 列為 stepId 的別名，但此處維持 processId 為獨立詞彙（實際生效版本），
        // 因此 stepId 的別名清單刻意不含 processId，避免 FindByAlias 對同一別名產生兩個詞彙的歧義對應。
        new GlossaryEntry("processId", "ProcessId", "製程代碼/工藝代碼", new[] { "procId", "processCode", "processName" }),
        new GlossaryEntry("ownerId", "OwnerId", "批貨擁有者類別 (Owner ID)", new[] { "ownerType", "ownerCode" }),
        new GlossaryEntry("ecCode", "EcCode", "產品版本/工程變更代碼 (Engineering Change Code)", new[] { "engineeringChangeCode", "ecNo" }),
        new GlossaryEntry("lotCnt", "LotCnt", "LOT 數量 (Lot Count)", new[] { "lotCount", "batchCount" }),
        new GlossaryEntry("sheetCnt", "SheetCnt", "玻璃數量 (Sheet Count)", new[] { "sheetCount", "substrateCount" }),
        new GlossaryEntry("panelCnt", "PanelCnt", "Panel 數量 (Panel Count)", new[] { "panelCount", "unitPanelCount" }),
        new GlossaryEntry("stayHours", "StayHours", "當站停留時間 (Stay Duration in Hours)", new[] { "stayTime", "dwellTime", "stationStayHours" }),
        new GlossaryEntry("startHours", "StartHours", "下線時間 (Start Duration in Hours)", new[] { "startTime", "downTime", "startDurationHours" }),
        new GlossaryEntry("workCenter", "WorkCenter", "生產工作中心/工作站代碼 (Work Center)", new[] { "wc", "workCenterCode", "stationGroup" }),
        new GlossaryEntry("productLine", "ProductLine", "生產線別代碼 (Product Line)", new[] { "line", "productionLine", "lineNo", "lineId" }),
        new GlossaryEntry("stepId", "StepId", "製程站點/工序代碼 (Step/Operation ID)", new[] { "stageId", "operationId", "operId", "STEP_ID" }),
        new GlossaryEntry("stepName", "StepName", "製程站點/工序名稱", new[] { "operationName", "stageName" }),
        new GlossaryEntry("waferId", "WaferId", "晶圓片 ID (Wafer ID)", new[] { "waferNo", "dieId", "WAFER_ID" }),

        // 搬送/倉儲位置 (Material Handling / AMHS Location)
        new GlossaryEntry("zoneId", "ZoneId", "自動倉儲區域代碼 (AMHS Zone ID)", new[] { "zoneNo", "zoneCode", "zone_id" }),
        new GlossaryEntry("shelfId", "ShelfId", "貨架層位代碼 (Shelf/Rack Position ID)", new[] { "shelfNo", "rackId", "shelf_id" }),
        new GlossaryEntry("patiId", "PatiId", "放置位置代碼 (Placement/Slot Address ID)", new[] { "placeId", "slotAddressId", "pati_id" }),
        new GlossaryEntry("position", "Position", "載具/面板目前位置代碼 (Current Item Position)", new[] { "posId", "locationCode" }),

        // 生產計量與良率指標
        new GlossaryEntry("quantityInProcess", "QuantityInProcess", "目前在製中數量（尚未完工）", new[] { "wipQty", "inProcessQty", "qtyInProcess" }),
        new GlossaryEntry("quantityCompleted", "QuantityCompleted", "該工單/項目已完工入庫數量", new[] { "completedQty", "doneQty", "finishQty" }),
        new GlossaryEntry("totalItems", "TotalItems", "符合查詢條件的項目總筆數", new[] { "totalCount", "itemCount", "total" }),
        new GlossaryEntry("yieldRate", "YieldRate", "生產/檢驗良率（範圍 0.0 ~ 1.0）", new[] { "yield", "yieldRatio", "passRate" }),
        new GlossaryEntry("defectCount", "DefectCount", "缺陷/不良品數量", new[] { "ngQty", "scrapQty", "failCount" }),

        // 狀態與時間戳記
        new GlossaryEntry("status", "Status", "目前狀態代碼或說明（如: In Progress, Completed）", new[] { "state", "statusCode" }),
        new GlossaryEntry("priority", "Priority", "生產優先等級（如: Normal, High, Urgent）", new[] { "priorityCode", "priLevel" }),
        new GlossaryEntry("scheduledCompletion", "ScheduledCompletion", "預計完工日期時間", new[] { "estimatedCompletion", "targetDate", "dueTime" }),
        new GlossaryEntry("generatedAt", "GeneratedAt", "報告/資料產出之 UTC 時間戳記", new[] { "createTime", "reportTime", "generatedTime" }),
    };

    public static GlossaryEntry? FindByAlias(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        return Entries.FirstOrDefault(e =>
            e.Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));
    }
}
