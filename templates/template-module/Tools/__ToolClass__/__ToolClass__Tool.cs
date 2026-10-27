using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.__System__.Dtos;
using McpGateway.__Department__.__System__.Services;
using ModelContextProtocol.Server;

namespace McpGateway.__Department__.__System__.Tools.__ToolClass__;

// 契約三層分工（ADR-016）：Contracts/＝鏡射契約（internal）、Services/＝契約映射（Map*()）、
// Dtos/＝輸出契約（public、詞庫標準詞、禁止 JsonPropertyName）。Tool 只引用 Dtos/ 的輸出契約。
[McpServerToolType]
public class __ToolClass__Tool
{
    private readonly I__ToolClass__Service _service;

    public __ToolClass__Tool(I__ToolClass__Service service)
    {
        _service = service;
    }

    [McpServerTool(Name = "__full_tool_name__", UseStructuredContent = true)]
    [Description("查詢 __Department__ __System__ 現場之 __ToolClass__ 處理狀態。")]
    public async Task<__ToolClass__ResultDto> ExecuteAsync(
        [Description("廠別代碼 (Shop ID)")] string shop,
        [Description("查詢代碼 (Query ID)")] string queryId,
        CancellationToken cancellationToken = default)
    {
        return await _service.ExecuteAsync(new __ToolClass__Input(shop, queryId), cancellationToken);
    }
}
