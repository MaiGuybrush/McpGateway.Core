using System.ComponentModel;

namespace McpGateway.__Department__.__System__.Dtos;

// 輸入契約同為對 Agent 的承諾（ADR-016 R1 對稱適用）：public、標準詞、禁止 [JsonPropertyName]。
[Description("查詢 __Department__ __System__ __ToolClass__ 輸入參數")]
public sealed record __ToolClass__Input(
    [property: Description("廠別代碼 (Shop ID)，如 TFT1")] string Shop,
    [property: Description("查詢代碼 (Query ID)")] string QueryId
);
