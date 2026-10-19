using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using McpGateway.Core.Configuration;
using McpGateway.__Department__.__System__.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpGateway.__Department__.__System__.Services;

/// <summary>
/// 透過 Consul HTTP API（<c>?raw</c>）動態解析 Downstream BaseUrl，實作方式比照
/// Core <c>UacApiEndpointResolver</c>（見 ADR-013）：不使用 Consul NuGet 套件的 <c>IConsulClient</c>，
/// 直接用 <see cref="HttpClient"/> 打 Consul KV 端點，支援多節點依序容錯、5 分鐘結果快取，以及本地 Fallback。
/// Consul 叢集節點位址讀取共用的 <see cref="McpGatewayOptions.Consul"/>（與其他子系統共用同一份清單）。
/// </summary>
public sealed class ConsulKvDownstreamResolver : IDownstreamUrlResolver
{
    private readonly HttpClient _httpClient;
    private readonly __System__Options _options;
    private readonly McpGatewayOptions _gatewayOptions;
    private readonly ILogger<ConsulKvDownstreamResolver> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedBaseUrl;
    private DateTime _lastResolvedUtc = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public ConsulKvDownstreamResolver(
        HttpClient httpClient,
        IOptions<__System__Options> options,
        IOptions<McpGatewayOptions> gatewayOptions,
        ILogger<ConsulKvDownstreamResolver> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _gatewayOptions = gatewayOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedBaseUrl != null && (DateTime.UtcNow - _lastResolvedUtc) < _cacheDuration)
        {
            return _cachedBaseUrl;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedBaseUrl != null && (DateTime.UtcNow - _lastResolvedUtc) < _cacheDuration)
            {
                return _cachedBaseUrl;
            }

            var baseUrl = await ResolveFromConsulOrFallbackAsync(cancellationToken).ConfigureAwait(false);
            _cachedBaseUrl = baseUrl;
            _lastResolvedUtc = DateTime.UtcNow;
            return baseUrl;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> ResolveFromConsulOrFallbackAsync(CancellationToken cancellationToken)
    {
        var fallbackUrl = _options.FallbackBaseUrls?.Find(u => !string.IsNullOrWhiteSpace(u))
            ?? "http://api.corp.local/__department__/__system__";

        if (string.IsNullOrWhiteSpace(_options.ConsulKvKey))
        {
            _logger.LogWarning("未設定 ConsulKvKey，略過 Consul 查詢並直接回退至 FallbackBaseUrl: {FallbackUrl}", fallbackUrl);
            return fallbackUrl;
        }

        var kvKey = _options.ConsulKvKey;
        var consulUrls = (_gatewayOptions.Consul?.Urls ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        foreach (var consulUrl in consulUrls)
        {
            try
            {
                var trimmedBase = consulUrl.TrimEnd('/');
                var requestUrl = $"{trimmedBase}/v1/kv/{kvKey}?raw";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var response = await _httpClient.GetAsync(requestUrl, cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    var resolved = content?.Trim();
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        _logger.LogInformation("成功自 Consul KV ({Url}/{Key}) 解析 Downstream BaseUrl: {BaseUrl}", trimmedBase, kvKey, resolved);
                        return resolved;
                    }
                }
                else
                {
                    _logger.LogWarning("Consul 節點 {Url} 回應狀態碼 {StatusCode}（key: {Key}）", trimmedBase, response.StatusCode, kvKey);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "向 Consul 節點 {Url} 讀取 Key '{Key}' 失敗，嘗試下一個節點...", consulUrl, kvKey);
            }
        }

        _logger.LogWarning("Consul KV 均無法解析或不可達，回退至 FallbackBaseUrl: {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}
