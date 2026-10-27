using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.__System__.Dtos;

namespace McpGateway.__Department__.__System__.Services;

public interface I__ToolClass__Service
{
    Task<__ToolClass__ResultDto> ExecuteAsync(__ToolClass__Input input, CancellationToken cancellationToken = default);
}
