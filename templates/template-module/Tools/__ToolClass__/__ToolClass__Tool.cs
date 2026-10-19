using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.__System__.Services;
using ModelContextProtocol.Server;

namespace McpGateway.__Department__.__System__.Tools.__ToolClass__;

[Description("查詢 __Department__ __System__ __ToolClass__ 輸入參數")]
public sealed record __ToolClass__Input(
    [property: Description("廠別代碼 (Shop ID)，如 TFT1")] string Shop,
    [property: Description("查詢代碼 (Query ID)")] string QueryId
);

[Description("__Department__ __System__ __ToolClass__ 查詢結果")]
public sealed record __ToolClass__ResultDto(
    [property: Description("查詢代碼 (Query ID)")] string QueryId,
    [property: Description("廠別代碼 (Shop ID)")] string Shop,
    [property: Description("處理狀態 (Status)")] string Status,
    [property: Description("詳細訊息 (Message)")] string Message
);

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
