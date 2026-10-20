using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpGateway.__Department__.Host.Tools;

/// <summary>
/// 宿主層級驗證用 Smoke Tool，供啟動時進行連線與健康驗證。
/// </summary>
[McpServerToolType]
public class HelloTool
{
    [McpServerTool(Name = "__department___hello", UseStructuredContent = true)]
    [Description("簡單的問候與健康檢查工具，驗證 __Department__ MCP 宿主服務運作狀態。")]
    public static string Hello(
        [Description("呼叫者名稱")] string name = "MCP Gateway")
    {
        return $"Hello, {name}! __Department__ MCP Gateway Host is running successfully.";
    }
}
