using System.Collections.Generic;

namespace McpGateway.__Department__.__System__.Configuration;

/// <summary>
/// __System__ 子系統專屬組態選項。
/// Consul 叢集節點位址統一讀取共用的 <c>McpGateway:Consul:Urls</c>（見 <see cref="McpGateway.Core.Configuration.McpGatewayOptions"/>），
/// 與其他子系統共用同一份節點清單，避免各子系統各自維護重複清單。
/// 本類別結構全部扁平化，不另包巢狀 Downstream 物件（比照 Qsys/Mes 子系統慣例）。
/// </summary>
public class __System__Options
{
    public const string SectionName = "McpGateway:Systems:__system__";

    /// <summary>
    /// Consul Key-Value 鍵值路徑（依據 ADR-014 規範），存放單一原始字串 BaseUrl。
    /// 各環境 appsettings 應明確宣告不同的 Key（例如 .../BaseUrl/TestOa、.../BaseUrl/ProductionOa），
    /// 避免 Dev/Test/Prod 三環境共用同一把 Consul Key。
    /// 尚未在 Consul 登記對應 KV 的環境應將此值留空字串，resolver 會直接回退 FallbackBaseUrls（見 pitfalls.md #11）。
    /// </summary>
    public string ConsulKey { get; set; } = "McpGateway/__department__/systems/__system__/Downstream/BaseUrl";

    /// <summary>
    /// Consul 全節點皆無法解析時的最終回退 BaseUrl 清單（取第一個非空值）。
    /// </summary>
    public List<string> FallbackBaseUrls { get; set; } = new();

    /// <summary>
    /// 呼叫下游之逾時秒數。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
