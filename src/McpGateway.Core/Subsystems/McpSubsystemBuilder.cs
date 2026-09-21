using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using McpGateway.Core.Hosting;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Fluent builder for configuring a subsystem's tools and whitelist registration.
/// </summary>
public class McpSubsystemBuilder
{
    private readonly List<Type> _toolTypes = new();
    private readonly HashSet<string> _explicitToolNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the subsystem name.
    /// </summary>
    public string SubsystemName { get; }

    /// <summary>
    /// Gets the application service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="McpSubsystemBuilder"/>.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <param name="services">The service collection.</param>
    public McpSubsystemBuilder(string subsystemName, IServiceCollection services)
    {
        if (string.IsNullOrWhiteSpace(subsystemName))
            throw new ArgumentException("Subsystem name cannot be null or empty.", nameof(subsystemName));

        SubsystemName = subsystemName.Trim().ToLowerInvariant();
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers a tool type with the subsystem and the MCP server.
    /// </summary>
    /// <typeparam name="TTool">The tool class type.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithTools<TTool>() where TTool : class
    {
        return WithTools(typeof(TTool));
    }

    /// <summary>
    /// Registers a tool type with the subsystem and the MCP server.
    /// </summary>
    /// <param name="toolType">The tool type to register.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithTools(Type toolType)
    {
        ArgumentNullException.ThrowIfNull(toolType);

        if (!_toolTypes.Contains(toolType))
        {
            _toolTypes.Add(toolType);
        }

        // Register tool with the underlying MCP Server builder.
        // 必須顯式轉型為 IEnumerable<Type>，否則 Type[] 會被 C# 重載解析選中泛型重載
        // WithTools<TToolType>(builder, TToolType target)，把整個陣列當成單一 tool 容器型別反射，
        // 導致掃描不到 [McpServerTool] 方法而註冊 0 個工具。
        Services.AddMcpServer().WithTools((IEnumerable<Type>)new[] { toolType });

        return this;
    }

    /// <summary>
    /// Registers multiple tool types with the subsystem and the MCP server.
    /// </summary>
    /// <param name="toolTypes">The tool types to register.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithTools(params Type[] toolTypes)
    {
        ArgumentNullException.ThrowIfNull(toolTypes);
        return WithTools((IEnumerable<Type>)toolTypes);
    }

    /// <summary>
    /// Registers multiple tool types with the subsystem and the MCP server.
    /// </summary>
    /// <param name="toolTypes">The tool types to register.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithTools(IEnumerable<Type> toolTypes)
    {
        ArgumentNullException.ThrowIfNull(toolTypes);

        foreach (var type in toolTypes)
        {
            WithTools(type);
        }

        return this;
    }

    /// <summary>
    /// Explicitly whitelists one or more tool names for this subsystem.
    /// </summary>
    /// <param name="toolNames">The tool names to whitelist.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithToolNames(params string[] toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        return WithToolNames((IEnumerable<string>)toolNames);
    }

    /// <summary>
    /// Explicitly whitelists tool names for this subsystem.
    /// </summary>
    /// <param name="toolNames">The tool names to whitelist.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public McpSubsystemBuilder WithToolNames(IEnumerable<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);

        foreach (var name in toolNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _explicitToolNames.Add(name.Trim().ToLowerInvariant());
            }
        }

        return this;
    }

    /// <summary>
    /// Builds the subsystem registration descriptor.
    /// </summary>
    internal McpSubsystemRegistration Build()
    {
        return new McpSubsystemRegistration(SubsystemName, _toolTypes, _explicitToolNames);
    }
}
