using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.__Department__.__System__.Services;

/// <summary>
/// 下游業務服務 BaseUrl 動態解析介面。
/// </summary>
public interface IDownstreamUrlResolver
{
    /// <summary>
    /// 解析下游服務之連線 BaseUrl。
    /// </summary>
    Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default);
}
