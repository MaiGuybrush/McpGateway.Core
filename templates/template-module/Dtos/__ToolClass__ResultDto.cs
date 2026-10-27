using System.ComponentModel;

namespace McpGateway.__Department__.__System__.Dtos;

// ③ 輸出契約（ADR-016）：public、欄位 = 詞庫標準詞、禁止 [JsonPropertyName]——
// Agent 可見 JSON 鍵一律由 C# 屬性名的 camelCase 推導；下游來源欄名寫進 Description。
[Description("__Department__ __System__ __ToolClass__ 查詢結果")]
public sealed record __ToolClass__ResultDto(
    [property: Description("查詢代碼 (Query ID)，下游欄位 query_id")] string QueryId,
    [property: Description("廠別代碼 (Shop ID)，下游欄位 shop")] string Shop,
    [property: Description("處理狀態 (Status)，下游欄位 status")] string Status,
    [property: Description("詳細訊息 (Message)，下游欄位 message")] string Message
);
