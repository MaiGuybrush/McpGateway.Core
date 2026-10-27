using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace McpGateway.Analyzers.Tests;

/// <summary>
/// Tests for MCP0013 (McpOutputContractAnalyzer, Issue #15 / ADR-016):
/// 輸出契約（tool 回傳可達閉包內的 public 型別）不得標註 [JsonPropertyName]。
/// 覆蓋 §7.1 #1 要求的四類案例：tool 可達正例、internal 反例、
/// 設定鏡射不可達反例（ConsulShopItem 形態）、Core 型別豁免反例。
/// </summary>
public class McpOutputContractAnalyzerTests
{
    private const string AttributeDeclarations = @"
namespace ModelContextProtocol
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class McpServerToolTypeAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class McpServerToolAttribute : System.Attribute
    {
        public string? Name { get; set; }
        public McpServerToolAttribute() { }
        public McpServerToolAttribute(string name) { Name = name; }
    }
}

namespace System.Text.Json.Serialization
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class JsonPropertyNameAttribute : System.Attribute
    {
        public string Name { get; }
        public JsonPropertyNameAttribute(string name) { Name = name; }
    }
}
";

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        string assemblyName = "McpGateway.Qsys.Issapi")
    {
        var attrTree = CSharpSyntaxTree.ParseText(AttributeDeclarations);
        var sourceTree = CSharpSyntaxTree.ParseText(source);

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { attrTree, sourceTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Any())
        {
            throw new InvalidOperationException("Source compilation failed: " + string.Join("; ", compileErrors.Select(e => e.ToString())));
        }

        var analyzer = new McpOutputContractAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics.Where(d => d.Id == McpOutputContractAnalyzer.DiagnosticId).ToImmutableArray();
    }

    [Fact]
    public async Task ToolReachablePublicDto_WithJsonPropertyName_EmitsMCP0013()
    {
        // Issapi 現況形態：tool 直接回傳帶 JsonPropertyName 的 public DTO。
        var source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Qsys.Issapi;

[McpServerToolType]
public class IssapiQueryTool
{
    [McpServerTool(Name = ""qsys_issapi_query_gurobi"")]
    public Task<GurobiInfeasibleDto> QueryAsync() => Task.FromResult(new GurobiInfeasibleDto());
}

public class GurobiInfeasibleDto
{
    [JsonPropertyName(""totalCount"")]
    public int TotalCount { get; set; }

    [JsonPropertyName(""createTime"")]
    public string CreateTime { get; set; } = string.Empty;

    public string NotRenamed { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Contains("JsonPropertyName", d.GetMessage()));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("totalCount"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("createTime"));
    }

    [Fact]
    public async Task NestedContainerAndRecordPositional_WithJsonPropertyName_EmitsMCP0013()
    {
        // Task<IReadOnlyList<Record>> 展開 + record positional [property:] 標註亦在閉包內。
        var source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Qsys.Issapi;

[McpServerToolType]
public class IssapiQueryTool
{
    [McpServerTool(Name = ""qsys_issapi_query_jobs"")]
    public Task<IReadOnlyList<JobItem>> QueryAsync() => Task.FromResult<IReadOnlyList<JobItem>>(new List<JobItem>());
}

public sealed record JobItem(
    [property: JsonPropertyName(""jobNo"")] string JobNo,
    [property: Description(""工作中心"")] string WorkCenter);

public class DescriptionAttribute : System.Attribute
{
    public DescriptionAttribute(string text) { }
}
";
        var diagnostics = await RunAnalyzerAsync(source);

        Assert.Single(diagnostics);
        Assert.Contains("jobNo", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task InternalMirrorContract_WithJsonPropertyName_NoDiagnostic()
    {
        // R1 目標形態：鏡射契約標 internal、止於 Service 層 → 天然不可達、免檢。
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Qsys.Issapi;

[McpServerToolType]
public class IssapiQueryTool
{
    [McpServerTool(Name = ""qsys_issapi_query_gurobi"")]
    public Task<GurobiInfeasibleResponse> QueryAsync() => Task.FromResult(new GurobiInfeasibleResponse());
}

internal sealed class UpstreamGurobiInfeasibleResult
{
    [JsonPropertyName(""totalCount"")]
    public int TotalCount { get; set; }
}

public sealed class GurobiInfeasibleResponse
{
    public int TotalItems { get; set; }
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnreachableConfigMirror_WithJsonPropertyName_NoDiagnostic()
    {
        // ConsulShopItem 形態：public 但從任何 tool 簽名不可達（設定鏡射）→ 自動豁免，不需豁免清單。
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Report.Mfg;

[McpServerToolType]
public class MfgReportTool
{
    [McpServerTool(Name = ""mfg_report_query_wip"")]
    public Task<WipItemDto> QueryAsync() => Task.FromResult(new WipItemDto());
}

public class ConsulShopItem
{
    [JsonPropertyName(""shop_id"")]
    public string ShopId { get; set; } = string.Empty;
}

public class WipItemDto
{
    public string ProcessId { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task CoreAssemblyTypes_AreExempt_NoDiagnostic()
    {
        // §6 #6：Core 組件型別（ErrorResponse 等外部系統契約）一律豁免。
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Core.Contracts;

[McpServerToolType]
public class CoreTool
{
    [McpServerTool(Name = ""core_ping"")]
    public Task<ErrorResponse> PingAsync() => Task.FromResult(new ErrorResponse());
}

public class ErrorResponse
{
    [JsonPropertyName(""error_code"")]
    public string ErrorCode { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source, assemblyName: "McpGateway.Core");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoToolInCompilation_NoDiagnostic()
    {
        // 無 tool 的模組（如純 Service 專案）：可達集合為空，不註冊屬性檢查。
        var source = @"
using System.Text.Json.Serialization;

namespace McpGateway.Qsys.Umsapi;

public class SomeSettings
{
    [JsonPropertyName(""api_url"")]
    public string ApiUrl { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task PrivatePropertyOnReachableType_WithJsonPropertyName_NoDiagnostic()
    {
        // 非 public 屬性不參與序列化輸出表面，免檢。
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;
using System.Text.Json.Serialization;

namespace McpGateway.Qsys.Issapi;

[McpServerToolType]
public class IssapiQueryTool
{
    [McpServerTool(Name = ""qsys_issapi_query_gurobi"")]
    public Task<GurobiInfeasibleResponse> QueryAsync() => Task.FromResult(new GurobiInfeasibleResponse());
}

public sealed class GurobiInfeasibleResponse
{
    public int TotalItems { get; set; }

    [JsonPropertyName(""internal_flag"")]
    private string InternalFlag { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics);
    }
}
