using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Cache;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;
using McpGateway.Core.Validation;

namespace McpGateway.Core.Auth;

/// <summary>
/// Response model for UAC API identity endpoint.
/// </summary>
public class UacApiKeyIdentityResponse
{
    [JsonPropertyName("empId")]
    public string? EmpId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Validates API-KEY tokens against enterprise UAC API with multi-node failover and SHA-256 caching.
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly HttpClient _httpClient;
    private readonly IUacApiEndpointResolver _endpointResolver;
    private readonly ITokenCacheService _cacheService;
    private readonly ILogger<ApiKeyValidator> _logger;
    private readonly McpGatewayOptions _gatewayOptions;
    private readonly AuthOptions _authOptions;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiKeyValidator(
        HttpClient httpClient,
        IUacApiEndpointResolver endpointResolver,
        ITokenCacheService cacheService,
        IOptions<McpGatewayOptions> options,
        ILogger<ApiKeyValidator> logger)
    {
        _httpClient = httpClient;
        _endpointResolver = endpointResolver;
        _cacheService = cacheService;
        _logger = logger;
        _gatewayOptions = options.Value;
        _authOptions = options.Value.Auth ?? new AuthOptions();
    }

    /// <inheritdoc />
    public async Task<ToolContext?> ValidateAsync(string apiKey, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var keyHash = ComputeSha256Hex(apiKey);
        var cacheKey = $"apikey:{keyHash}";

        // 1. Check cache
        try
        {
            var cached = await _cacheService.GetAsync("API-KEY", cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("CorrelationId: {CorrelationId} - API-KEY cache hit for {HashPrefix}...", correlationId, keyHash[..Math.Min(8, keyHash.Length)]);
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CorrelationId: {CorrelationId} - Error accessing token cache, continuing with direct validation", correlationId);
        }

        // 2. Resolve UAC API endpoints
        var endpoints = await _endpointResolver.ResolveEndpointsAsync();
        if (endpoints == null || endpoints.Count == 0)
        {
            _logger.LogError("CorrelationId: {CorrelationId} - No UAC API endpoints available for validation", correlationId);
            throw new HttpRequestException("No UAC API endpoints available for validation", null, HttpStatusCode.ServiceUnavailable);
        }

        var systemName = !string.IsNullOrWhiteSpace(_authOptions.SystemName)
            ? _authOptions.SystemName
            : _gatewayOptions.Department;

        var timeoutSeconds = _authOptions.ApiKeyTimeoutSeconds > 0
            ? _authOptions.ApiKeyTimeoutSeconds
            : 3;

        // 3. Try each endpoint with failover
        foreach (var endpoint in endpoints)
        {
            var baseUrl = endpoint.TrimEnd('/');
            var requestUrl = $"{baseUrl}/api-keys/identity?system={Uri.EscapeDataString(systemName ?? string.Empty)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("X-Api-Key", apiKey);
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    request.Headers.Add("X-Correlation-ID", correlationId);
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var response = await _httpClient.SendAsync(request, cts.Token);

                // 401/403: Explicitly unauthorized, immediately return null without retrying other nodes
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("CorrelationId: {CorrelationId} - UAC API rejected API-KEY at {Endpoint} (Status: {StatusCode})", correlationId, baseUrl, response.StatusCode);
                    return null;
                }

                if (response.IsSuccessStatusCode)
                {
                    var identity = await response.Content.ReadFromJsonAsync<UacApiKeyIdentityResponse>(JsonOptions, cts.Token);
                    if (identity != null && !string.IsNullOrWhiteSpace(identity.EmpId))
                    {
                        var toolContext = new ToolContext(
                            identity.EmpId,
                            _gatewayOptions.Department ?? "unknown",
                            identity.Name ?? identity.EmpId,
                            "API-KEY",
                            string.Empty,
                            correlationId
                        );

                        var ttlMinutes = _authOptions.CacheTtlMinutes > 0 ? _authOptions.CacheTtlMinutes : 30;
                        try
                        {
                            await _cacheService.SetAsync("API-KEY", cacheKey, toolContext, ttlMinutes);
                        }
                        catch (Exception cacheEx)
                        {
                            _logger.LogWarning(cacheEx, "Failed to cache validated API-KEY tool context");
                        }

                        _logger.LogInformation("CorrelationId: {CorrelationId} - API-KEY validation successful for user {UserId} ({UserName})", correlationId, toolContext.UserId, toolContext.Role);
                        return toolContext;
                    }
                }

                _logger.LogWarning("CorrelationId: {CorrelationId} - UAC API node {Endpoint} returned status {StatusCode}, attempting failover...", correlationId, baseUrl, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CorrelationId: {CorrelationId} - Error calling UAC API node {Endpoint}, attempting failover...", correlationId, baseUrl);
            }
        }

        _logger.LogError("CorrelationId: {CorrelationId} - All UAC API endpoints failed to validate API-KEY", correlationId);
        throw new HttpRequestException("All UAC API endpoints failed during validation", null, HttpStatusCode.ServiceUnavailable);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateSystemRegisteredAsync(CancellationToken cancellationToken = default)
    {
        var systemName = !string.IsNullOrWhiteSpace(_authOptions.SystemName)
            ? _authOptions.SystemName
            : _gatewayOptions.Department;

        if (string.IsNullOrWhiteSpace(systemName))
        {
            _logger.LogWarning("No SystemName configured for UAC system validation");
            return false;
        }

        var endpoints = await _endpointResolver.ResolveEndpointsAsync(cancellationToken);
        if (endpoints == null || endpoints.Count == 0)
        {
            _logger.LogWarning("No UAC API endpoints available to validate system registration");
            return false;
        }

        foreach (var endpoint in endpoints)
        {
            var baseUrl = endpoint.TrimEnd('/');
            var requestUrl = $"{baseUrl}/api-keys/systems";

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var response = await _httpClient.GetAsync(requestUrl, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var systems = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, cts.Token);
                    if (systems != null && systems.Contains(systemName, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("System '{SystemName}' is successfully verified in UAC API system list", systemName);
                        return true;
                    }

                    _logger.LogError("System '{SystemName}' is NOT registered in UAC API systems: [{Systems}]", systemName, string.Join(", ", systems ?? new List<string>()));
                    return false;
                }
                else
                {
                    _logger.LogWarning("UAC API node {Endpoint} returned HTTP {StatusCode} when querying systems", baseUrl, (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query systems from UAC API node {Endpoint}", baseUrl);
            }
        }

        _logger.LogWarning("All UAC API nodes failed when checking system registration for '{SystemName}'", systemName);
        return false;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}