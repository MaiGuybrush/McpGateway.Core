using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace McpGateway.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class McpNamingAndDescriptionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticIdAlias = "MCP0010";
    public const string DiagnosticIdMissingDesc = "MCP0011";
    public const string DiagnosticIdInvalidFormat = "MCP0012";

    private static readonly DiagnosticDescriptor RuleAlias = new(
        DiagnosticIdAlias,
        "屬性或參數使用歷史別名而非標準領域詞彙",
        "符號 '{0}' 使用了歷史別名 '{1}'，建議改為標準詞 '{2}'（{3}）。若語意不符請忽略此警告。",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Standard domain vocabulary should be used for cross-gateway consistency.");

    private static readonly DiagnosticDescriptor RuleMissingDesc = new(
        DiagnosticIdMissingDesc,
        "MCP Tool DTO 屬性缺少 [Description] 特性",
        "屬性 '{0}'（MCP DTO '{1}'）缺少 [Description] 特性，請補上繁體中文語意說明與格式範例。",
        "Documentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DTO properties returned to LLMs should have descriptive metadata.");

    private static readonly DiagnosticDescriptor RuleInvalidFormat = new(
        DiagnosticIdInvalidFormat,
        "屬性或參數名稱未遵循標準命名規範",
        "屬性或參數 '{0}' 應使用不含底線的標準 camelCase/PascalCase 命名。",
        "Naming",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Tool parameters and DTO properties should use consistent camelCase/PascalCase.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(RuleAlias, RuleMissingDesc, RuleInvalidFormat);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        context.RegisterSymbolAction(AnalyzeParameter, SymbolKind.Parameter);
    }

    private void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var propertySymbol = (IPropertySymbol)context.Symbol;
        if (propertySymbol.DeclaredAccessibility != Accessibility.Public) return;

        var containingType = propertySymbol.ContainingType;
        if (containingType == null) return;

        // Check if containing type is a Tool or DTO (e.g. ends with Dto, Response, Input, Output, Report, Item)
        bool isDtoOrTool = IsMcpDtoOrTool(containingType);
        if (!isDtoOrTool) return;

        var propName = propertySymbol.Name;

        // 1. Check MCP0010: Known legacy alias
        var aliasMatch = Tier1Vocabulary.FindByAlias(propName);
        if (aliasMatch != null)
        {
            var diagnostic = Diagnostic.Create(
                RuleAlias,
                propertySymbol.Locations[0],
                propName,
                propName,
                aliasMatch.StandardPascal,
                aliasMatch.Description);
            context.ReportDiagnostic(diagnostic);
        }

        // 2. Check MCP0011: Missing [Description]
        bool hasDescription = propertySymbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name is "DescriptionAttribute" or "Description");

        if (!hasDescription && isDtoOrTool && !containingType.Name.EndsWith("Tool"))
        {
            var diagnostic = Diagnostic.Create(
                RuleMissingDesc,
                propertySymbol.Locations[0],
                propName,
                containingType.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // 3. Check MCP0012: Underscore check
        if (propName.Contains("_") && !propName.StartsWith("_"))
        {
            var diagnostic = Diagnostic.Create(
                RuleInvalidFormat,
                propertySymbol.Locations[0],
                propName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzeParameter(SymbolAnalysisContext context)
    {
        var paramSymbol = (IParameterSymbol)context.Symbol;
        var methodSymbol = paramSymbol.ContainingSymbol as IMethodSymbol;
        if (methodSymbol == null) return;

        // Check if method is an MCP Tool method
        bool isMcpTool = methodSymbol.GetAttributes().Any(a => a.AttributeClass?.Name is "McpServerToolAttribute" or "McpServerTool" or "McpToolAttribute" or "McpTool");
        if (!isMcpTool && !IsMcpDtoOrTool(methodSymbol.ContainingType)) return;

        var paramName = paramSymbol.Name;

        // Check MCP0010: Known legacy alias on parameters
        var aliasMatch = Tier1Vocabulary.FindByAlias(paramName);
        if (aliasMatch != null)
        {
            var diagnostic = Diagnostic.Create(
                RuleAlias,
                paramSymbol.Locations[0],
                paramName,
                paramName,
                aliasMatch.StandardCamel,
                aliasMatch.Description);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsMcpDtoOrTool(INamedTypeSymbol type)
    {
        if (type.GetAttributes().Any(a => a.AttributeClass?.Name is "McpServerToolTypeAttribute" or "McpServerToolType" or "McpToolAttribute" or "McpTool"))
            return true;

        var name = type.Name;
        return name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Response", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Request", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Input", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Output", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Report", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Summary", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Item", StringComparison.OrdinalIgnoreCase);
    }
}
