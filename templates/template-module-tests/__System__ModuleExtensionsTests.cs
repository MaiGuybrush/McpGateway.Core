using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using McpGateway.Core.Configuration;
using McpGateway.Core.Subsystems;
using McpGateway.__Department__.__System__.Configuration;
using McpGateway.__Department__.__System__.Services;

namespace McpGateway.__Department__.__System__.Tests;

public class __System__ModuleExtensionsTests
{
    [Fact]
    public void Add__System__Subsystem_RegistersServicesAndOptionsProperly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["McpGateway:Systems:__system__:FallbackBaseUrls:0"] = "http://test-api.corp.local",
            ["McpGateway:Systems:__system__:TimeoutSeconds"] = "15"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.Add__System__Subsystem(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<IOptions<__System__Options>>()?.Value;
        Assert.NotNull(options);
        Assert.Equal("http://test-api.corp.local", options.FallbackBaseUrls[0]);
        Assert.Equal(15, options.TimeoutSeconds);

        var service = provider.GetService<I__ToolClass__Service>();
        Assert.NotNull(service);

        var resolver = provider.GetService<IDownstreamUrlResolver>();
        Assert.NotNull(resolver);
        Assert.IsType<ConsulKvDownstreamResolver>(resolver);

        var registry = provider.GetService<IMcpSubsystemRegistry>();
        Assert.NotNull(registry);
        Assert.True(registry.ContainsSubsystem("__system__"));
    }
}

public class ConsulKvDownstreamResolverTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static ConsulKvDownstreamResolver CreateResolver(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        List<string> consulUrls,
        string? fallbackBaseUrl = null,
        string? consulKey = "McpGateway/__department__/systems/__system__/Downstream/BaseUrl")
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responder));
        var options = Options.Create(new __System__Options
        {
            ConsulKey = consulKey ?? string.Empty,
            FallbackBaseUrls = new List<string> { fallbackBaseUrl ?? "http://fallback.corp.local" }
        });
        var gatewayOptions = Options.Create(new McpGatewayOptions
        {
            Consul = new ConsulOptions { Urls = consulUrls }
        });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsulKvDownstreamResolver>.Instance;

        return new ConsulKvDownstreamResolver(httpClient, options, gatewayOptions, logger);
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_ConsulReturnsRawValue_ReturnsResolvedUrl()
    {
        var resolver = CreateResolver(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("http://__system__-from-consul.corp.local")
            },
            new List<string> { "http://consul1:8500/" });

        var url = await resolver.ResolveBaseUrlAsync();

        Assert.Equal("http://__system__-from-consul.corp.local", url);
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_FirstNodeFails_TriesNextNode()
    {
        var attempt = 0;
        var resolver = CreateResolver(
            _ =>
            {
                attempt++;
                if (attempt == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("http://__system__-from-node2.corp.local")
                };
            },
            new List<string> { "http://consul1:8500/", "http://consul2:8500/" });

        var url = await resolver.ResolveBaseUrlAsync();

        Assert.Equal(2, attempt);
        Assert.Equal("http://__system__-from-node2.corp.local", url);
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_AllNodesFail_ReturnsFallbackBaseUrl()
    {
        var resolver = CreateResolver(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new List<string> { "http://consul1:8500/" },
            fallbackBaseUrl: "http://fallback.corp.local");

        var url = await resolver.ResolveBaseUrlAsync();

        Assert.Equal("http://fallback.corp.local", url);
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_NoConsulUrlsConfigured_ReturnsFallbackBaseUrl()
    {
        var resolver = CreateResolver(
            _ => throw new InvalidOperationException("Should not be called when no Consul URLs configured."),
            new List<string>(),
            fallbackBaseUrl: "http://fallback.corp.local");

        var url = await resolver.ResolveBaseUrlAsync();

        Assert.Equal("http://fallback.corp.local", url);
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_ConsulKeyNotConfigured_SkipsConsulAndReturnsFallbackBaseUrl()
    {
        var resolver = CreateResolver(
            _ => throw new InvalidOperationException("Should not be called when ConsulKey is not configured."),
            new List<string> { "http://consul1:8500/" },
            fallbackBaseUrl: "http://fallback.corp.local",
            consulKey: "");

        var url = await resolver.ResolveBaseUrlAsync();

        Assert.Equal("http://fallback.corp.local", url);
    }
}
