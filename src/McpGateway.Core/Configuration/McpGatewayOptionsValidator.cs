using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace McpGateway.Core.Configuration;

/// <summary>
/// Validates McpGatewayOptions configuration.
/// </summary>
public class McpGatewayOptionsValidator : IValidateOptions<McpGatewayOptions>
{
    private readonly ILogger<McpGatewayOptionsValidator> _logger;

    public McpGatewayOptionsValidator(ILogger<McpGatewayOptionsValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, McpGatewayOptions options)
    {
        // Check required fields
        if (string.IsNullOrEmpty(options.Department))
        {
            throw new ConfigurationException("Department is required");
        }

        if (string.IsNullOrEmpty(options.RoutePrefix))
        {
            throw new ConfigurationException("RoutePrefix is required");
        }

        // Warning if RoutePrefix doesn't match /{Department} or /{Department}/mcp
        var expectedRoutePrefix = $"/" + options.Department.ToLowerInvariant();
        var expectedMcpRoutePrefix = expectedRoutePrefix + "/mcp";
        if (!options.RoutePrefix.Equals(expectedRoutePrefix, StringComparison.OrdinalIgnoreCase) &&
            !options.RoutePrefix.Equals(expectedMcpRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("RoutePrefix '{RoutePrefix}' does not match recommended format '{ExpectedRoutePrefix}' or '{ExpectedMcpRoutePrefix}'. Consider using '{RecommendedMcpRoutePrefix}' for consistency", 
                options.RoutePrefix, expectedRoutePrefix, expectedMcpRoutePrefix, expectedMcpRoutePrefix);
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Exception thrown when configuration is invalid.
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
}