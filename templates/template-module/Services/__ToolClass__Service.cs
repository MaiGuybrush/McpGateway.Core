using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.__Department__.__System__.Configuration;
using McpGateway.__Department__.__System__.Contracts;
using McpGateway.__Department__.__System__.Dtos;

namespace McpGateway.__Department__.__System__.Services;

public class __ToolClass__Service : I__ToolClass__Service
{
    private readonly HttpClient _httpClient;
    private readonly IDownstreamUrlResolver _downstreamResolver;
    private readonly IOptions<__System__Options> _options;
    private readonly ILogger<__ToolClass__Service> _logger;

    public __ToolClass__Service(
        HttpClient httpClient,
        IDownstreamUrlResolver downstreamResolver,
        IOptions<__System__Options> options,
        ILogger<__ToolClass__Service> logger)
    {
        _httpClient = httpClient;
        _downstreamResolver = downstreamResolver;
        _options = options;
        _logger = logger;
    }

    public async Task<__ToolClass__ResultDto> ExecuteAsync(__ToolClass__Input input, CancellationToken cancellationToken = default)
    {
        // 透過 Resolver 動態解析 BaseUrl（支援 Consul KV 或 Consul 服務發現）
        var baseUrl = await _downstreamResolver.ResolveBaseUrlAsync(cancellationToken);

        _logger.LogInformation("Executing {ToolClass} for Subsystem __System__, Shop: {Shop}, QueryId: {QueryId}, Target BaseUrl: {BaseUrl}",
            nameof(__ToolClass__Service), input.Shop, input.QueryId, baseUrl);

        // 預設模擬下游回傳（鏡射契約），實際應用可透過 _httpClient 呼叫 Downstream API，例如：
        // var upstream = await _httpClient.GetFromJsonAsync<__ToolClass__UpstreamResult>(
        //     $"{baseUrl}/api/query?shop={input.Shop}&queryId={input.QueryId}", cancellationToken);
        var upstream = new __ToolClass__UpstreamResult
        {
            QueryId = input.QueryId,
            Shop = input.Shop,
            Status = "SUCCESS",
            Message = $"Subsystem '__system__' query executed successfully against downstream '{baseUrl}' at {DateTime.UtcNow:O}"
        };
        await Task.CompletedTask;

        return MapResult(upstream);
    }

    // ② 契約映射（ADR-016）：唯一允許出現「鏡射契約 → 輸出契約」轉換的地方。
    // 下游方言鍵名在此翻譯為詞庫標準詞；鏡射型別（internal）不得越過 Service 層。
    private static __ToolClass__ResultDto MapResult(__ToolClass__UpstreamResult u) => new(
        QueryId: u.QueryId,
        Shop: u.Shop,
        Status: u.Status,
        Message: u.Message
    );
}
