using System.Text.Json.Serialization;

namespace McpGateway.__Department__.__System__.Contracts;

// ① 鏡射契約（ADR-016）：一律 internal、跟隨下游 JSON 鍵，止於 Service 層內部。
// [JsonPropertyName] 只准出現在 Contracts/；不得作為 MCP tool 的回傳型別。
// 實際應用時欄位應鏡射下游 API 的真實回應 schema（含下游方言鍵名）。
internal sealed class __ToolClass__UpstreamResult
{
    [JsonPropertyName("query_id")]
    public string QueryId { get; set; } = string.Empty;

    [JsonPropertyName("shop")]
    public string Shop { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
