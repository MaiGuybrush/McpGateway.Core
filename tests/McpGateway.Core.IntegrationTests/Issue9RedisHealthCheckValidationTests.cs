using System.Threading;
using McpGateway.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace McpGateway.Core.IntegrationTests;

/// <summary>
/// Issue #9 重現測試：TokenCache.ConnectionString 為空字串（未配置 Redis）時，
/// IConnectionMultiplexer 不會被註冊，但 RedisHealthCheck / GatewayReadinessHealthCheck
/// 無條件註冊且建構子依賴 IConnectionMultiplexer。
/// Development 環境預設開啟 ValidateOnBuild，此時容器驗證應通過而非拋 AggregateException。
/// </summary>
public class Issue9RedisHealthCheckValidationTests
{
    // 真實宿主（WebApplication/Host）會自動提供 IHostApplicationLifetime；裸 ServiceCollection 需自行補上才能準確模擬。
    private sealed class NoopHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private static ServiceProvider BuildProvider(string? redisConnectionString)
    {
        // 模擬 appsettings 中 TokenCache.ConnectionString（null/空字串代表未配置 Redis）
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpGateway:Department"] = "TEST",
                ["McpGateway:RoutePrefix"] = "/test/mcp",
                ["McpGateway:TokenCache:ConnectionString"] = redisConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostApplicationLifetime, NoopHostApplicationLifetime>();
        services.AddMcpGateway();

        // 模擬 Development 環境的容器驗證行為（ASP.NET Core Development 預設開啟）
        // 若 RedisHealthCheck 的 IConnectionMultiplexer 依賴無法滿足，此處拋 AggregateException
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void AddMcpGateway_WithEmptyRedisConnectionString_PassesValidateOnBuild()
    {
        using var provider = BuildProvider(string.Empty);
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddMcpGateway_WithNullRedisConnectionString_PassesValidateOnBuild()
    {
        using var provider = BuildProvider(null);
        Assert.NotNull(provider);
    }
}
