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
/// Regression tests for MCP0010 (legacy alias detection), ensuring Tier1Vocabulary.cs entries
/// merged from the now-removed CoreGlossary.cs (see docs/issue/issue-11-glossary-tier-governance.md)
/// still trigger the compile-time diagnostic correctly.
/// </summary>
public class McpNamingAndDescriptionAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var sourceTree = CSharpSyntaxTree.ParseText(source);

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "McpGateway.Test.Dtos",
            new[] { sourceTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Any())
        {
            throw new InvalidOperationException("Source compilation failed: " + string.Join("; ", compileErrors.Select(e => e.ToString())));
        }

        var analyzer = new McpNamingAndDescriptionAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task LegacyAliasMergedFromCoreGlossary_EmitsMCP0010()
    {
        // "wc" is the historical alias for "workCenter", an entry merged into Tier1Vocabulary.cs
        // from the now-deleted CoreGlossary.cs as part of the Tier 1/Tier 2 consolidation (Plan A).
        var source = @"
public class WorkCenterDto
{
    public string Wc { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        var aliasDiagnostics = diagnostics.Where(d => d.Id == McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias).ToList();

        Assert.Single(aliasDiagnostics);
        Assert.Contains("workCenter", aliasDiagnostics[0].GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StandardTermName_NoMCP0010Emitted()
    {
        var source = @"
public class WorkCenterDto
{
    public string WorkCenter { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        var aliasDiagnostics = diagnostics.Where(d => d.Id == McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias);

        Assert.Empty(aliasDiagnostics);
    }

    [Fact]
    public async Task ProcessIdStandaloneTerm_NotReportedAsStepIdAlias()
    {
        // Regression guard for the processId/stepId conflict documented in
        // docs/issue/issue-11-glossary-tier-governance.md section 6.1 item 1:
        // processId must remain its own standard term, not a legacy alias of stepId.
        var source = @"
public class ProcessIdDto
{
    public string ProcessId { get; set; } = string.Empty;
}
";
        var diagnostics = await RunAnalyzerAsync(source);
        var aliasDiagnostics = diagnostics.Where(d => d.Id == McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias);

        Assert.Empty(aliasDiagnostics);
    }
}
