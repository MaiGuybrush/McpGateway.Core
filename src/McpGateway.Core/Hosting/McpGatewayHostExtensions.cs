using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using McpGateway.Core.Cache;
using McpGateway.Core.Configuration;
using McpGateway.Core.Downstream;
using McpGateway.Core.Auth;
using McpGateway.Core.Observability;
using McpGateway.Core.Subsystems;
using McpGateway.Core.Tools;
using McpGateway.Core.Validation;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace McpGateway.Core.Hosting;

/// <summary>
/// Provides extension methods for configuring and running MCP Gateway services.
/// </summary>
public static class McpGatewayHostExtensions
{
    /// <summary>
    /// Adds MCP Gateway services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpGateway(this IServiceCollection services)
    {
        // Add required services
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        
        // Register CorrelationId services
        services.AddScoped<ICorrelationIdService, CorrelationIdService>();
        
        // Register configuration
        services.AddOptions<McpGatewayOptions>()
            .BindConfiguration("McpGateway")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register AuthOptions for health checks
        services.AddSingleton<AuthOptions>(sp => 
            sp.GetRequiredService<IOptions<McpGatewayOptions>>().Value.Auth ?? new AuthOptions()
        );

        // Register OcelotOptions for DownstreamClient
        services.AddSingleton<IOptions<OcelotOptions>>(sp =>
        {
            var gatewayOptions = sp.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
            return Options.Create(gatewayOptions.Ocelot ?? new OcelotOptions());
        });

        // Validate required fields
        services.AddSingleton<IValidateOptions<McpGatewayOptions>, McpGatewayOptionsValidator>();

        // Register individual health checks
        services.AddTransient<RedisHealthCheck>();
        services.AddTransient<JwksHealthCheck>();
        services.AddTransient<UacApiHealthCheck>();
        services.AddTransient<GatewayReadinessHealthCheck>();
        
        // Register health checks with tags
        services.AddHealthChecks()
            .AddCheck<GatewayHealthChecks>("gateway_live", tags: new[] { "live" })
            .AddCheck<GatewayReadinessHealthCheck>("gateway_ready", tags: new[] { "ready" });

        // Register Redis for token cache with circuit breaker and degradation.
        // Read the connection string from the already-registered IConfiguration instance instead of
        // building a temporary ServiceProvider (which would create duplicate singleton instances).
        var configuration = services
            .Where(d => d.ServiceType == typeof(IConfiguration))
            .Select(d => d.ImplementationInstance as IConfiguration)
            .LastOrDefault(c => c != null);
        var tokenCacheConnectionString = configuration?["McpGateway:TokenCache:ConnectionString"];
        if (!string.IsNullOrEmpty(tokenCacheConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RedisTokenCacheService>>();
                try
                {
                    return ConnectionMultiplexer.Connect(tokenCacheConnectionString);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to connect to Redis. Token caching will use in-memory fallback.");
                    return null!;
                }
            });
            
            services.AddSingleton<ITokenCacheService>(sp =>
            {
                try
                {
                    var redis = sp.GetService<IConnectionMultiplexer>();
                    if (redis != null && redis.IsConnected)
                    {
                        var logger = sp.GetRequiredService<ILogger<RedisTokenCacheService>>();
                        return new RedisTokenCacheService(redis, logger);
                    }
                    
                    // Redis not connected, use degraded mode
                    var nullLogger = sp.GetRequiredService<ILogger<NullTokenCacheService>>();
                    var memoryCache = sp.GetService<IMemoryCache>();
                    return new NullTokenCacheService(nullLogger, memoryCache);
                }
                catch (Exception ex)
                {
                    var logger = sp.GetRequiredService<ILogger<NullTokenCacheService>>();
                    logger.LogWarning(ex, "Redis service check failed, using degraded token cache");
                    
                    var nullLogger = sp.GetRequiredService<ILogger<NullTokenCacheService>>();
                    var memoryCache = sp.GetService<IMemoryCache>();
                    return new NullTokenCacheService(nullLogger, memoryCache);
                }
            });
        }
        else
        {
            // No Redis configured, use in-memory cache
            services.AddSingleton<ITokenCacheService>(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<NullTokenCacheService>>();
                var memoryCache = sp.GetRequiredService<IMemoryCache>();
                return new NullTokenCacheService(logger, memoryCache);
            });
        }

        // Register authentication and dynamic resolver services
        services.AddHttpClient<IUacApiEndpointResolver, UacApiEndpointResolver>();
        services.AddHttpClient<IApiKeyValidator, ApiKeyValidator>();
        services.AddHttpClient<UacApiHealthCheck>();
        services.AddTransient<IAsyncStartupValidator, UacSystemStartupValidator>();
        services.AddSingleton<AuthenticationProxy>();

        // Register downstream client
        services.AddHttpClient<IDownstreamClient, McpGateway.Core.Downstream.DownstreamClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
            var ocelotOptions = options.Ocelot ?? new OcelotOptions();
            
            if (!string.IsNullOrEmpty(ocelotOptions.BaseUrl))
            {
                client.BaseAddress = new Uri(ocelotOptions.BaseUrl);
            }
        });

        // Register Subsystem registry
        McpSubsystemServiceCollectionExtensions.GetOrAddSubsystemRegistry(services);

        // Register MCP services
        services.AddMcpServer(options =>
        {
            // Minimal configuration for gateway
        })
        .WithHttpTransport(httpOptions =>
        {
            httpOptions.Stateless = true;

            var existingCallback = httpOptions.ConfigureSessionOptions;
            httpOptions.ConfigureSessionOptions = async (httpContext, mcpOptions, cancellationToken) =>
            {
                if (existingCallback != null)
                {
                    await existingCallback(httpContext, mcpOptions, cancellationToken);
                }

                var options = httpContext.RequestServices.GetService<IOptions<McpGatewayOptions>>()?.Value;
                var subsystem = SubsystemRouteParser.ExtractSubsystem(httpContext, options?.Department);
                if (!string.IsNullOrEmpty(subsystem))
                {
                    var registry = httpContext.RequestServices.GetService<IMcpSubsystemRegistry>();
                    if (registry != null && mcpOptions.ToolCollection != null)
                    {
                        mcpOptions.ToolCollection = registry.FilterToolsForSubsystem(subsystem, mcpOptions.ToolCollection);
                    }
                }
            };
        });

        // Register Metrics service
        services.AddSingleton<MetricsService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
            return new MetricsService(options.Department ?? "unknown");
        });

        return services;
    }

    /// <summary>
    /// Maps MCP Gateway endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapMcpGateway(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
        
        // Get configuration
        var options = app.Services.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
        
        logger.LogInformation("Mapping MCP Gateway endpoints: RoutePrefix={RoutePrefix}, EnableHealthChecks={EnableHealthChecks}", 
            options.RoutePrefix, options.EnableHealthChecks);

        // Map health check endpoints
        if (options.EnableHealthChecks)
        {
            logger.LogInformation("Mapping health check endpoints");
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });
            
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
                // Timeout is handled by cancellation token in the health check implementation
            });
        }

        // Add CorrelationId middleware first (before authentication)
        app.UseMiddleware<CorrelationIdMiddleware>();
        
        // Add API-KEY authentication middleware to MCP endpoints
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        
        // Map MCP endpoint
        logger.LogInformation("Mapping MCP endpoint at {RoutePrefix}", options.RoutePrefix);
        app.MapMcp(options.RoutePrefix);

        // Map Subsystem MCP endpoint: /{department}/{system}/mcp
        var dept = !string.IsNullOrEmpty(options.Department) ? options.Department.ToLowerInvariant() : "{department}";
        var subsystemPath = $"/{dept}/{{system}}/mcp";
        if (!options.RoutePrefix.Equals(subsystemPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Mapping subsystem MCP endpoint at {SubsystemPath}", subsystemPath);
            app.MapMcp(subsystemPath);
        }

        return app;
    }

    /// <summary>
    /// Runs the MCP Gateway application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunMcpGatewayAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
        
        // Get configuration
        var options = app.Services.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
        
        logger.LogInformation("MCP Gateway configuration: Department={Department}, RoutePrefix={RoutePrefix}, EnableHealthChecks={EnableHealthChecks}", 
            options.Department, options.RoutePrefix, options.EnableHealthChecks);

        // Execute async startup validators
        var asyncValidators = app.Services.GetServices<IAsyncStartupValidator>();
        var allErrors = new List<ValidationError>();
        foreach (var validator in asyncValidators)
        {
            var errors = await validator.ValidateAsync();
            if (errors != null && errors.Count > 0)
            {
                allErrors.AddRange(errors);
            }
        }

        if (allErrors.Count > 0)
        {
            logger.LogError("Gateway startup validation failed with {Count} error(s):", allErrors.Count);
            foreach (var error in allErrors)
            {
                logger.LogError("  - [{ToolName}] ({FileLocation}): {Description}", 
                    error.ToolName, error.FileLocation, error.Description);
            }
            throw new StartupValidationException(allErrors);
        }

        // Initialize Metrics
        var metrics = app.Services.GetRequiredService<MetricsService>();
        logger.LogInformation("Metrics initialized for department: {Department}", options.Department);

        // Run the application
        logger.LogInformation("Starting MCP Gateway...");
        await app.RunAsync();
    }
}
