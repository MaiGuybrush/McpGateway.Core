using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace McpGateway.Analyzers;

/// <summary>
/// MCP0013（Issue #15 / ADR-016）：MCP tool 的輸出契約（tool 回傳可達閉包內的 public 型別）
/// 不得標註 [JsonPropertyName]——輸出 JSON 鍵一律由 C# 屬性名的 camelCase 推導，
/// 使詞庫標準詞直接成為 Agent 可見鍵名。鏡射契約應標 internal 並止於 Service 層。
///
/// 判定權威為「從 tool 簽名追線路」的可達性閉包（Issue #15 §5.3），不靠類別名字後綴推測；
/// internal／不可達／Core 與 Analyzers 組件型別一律豁免（§5.2、§6 #6）。
/// 過渡期預設 severity 為 suggestion（Info，不擋建置），下游在遷移完成的 PR 中
/// 逐專案以 .editorconfig 收為 warning（§6 #11）。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class McpOutputContractAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCP0013";

    // §6 #6：Core / Analyzers 組件定義的型別是外部系統契約，帶 JsonPropertyName 正當。
    private static readonly HashSet<string> ExemptAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "McpGateway.Core",
        "McpGateway.Analyzers"
    };

    // 容器型別：從 tool 回傳型別剝除後取其元素型別（§5.3 步驟 2）。
    private static readonly HashSet<string> ContainerTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Task", "ValueTask",
        "IEnumerable", "IAsyncEnumerable", "IReadOnlyList", "IReadOnlyCollection",
        "ICollection", "IList", "List", "HashSet", "SortedSet", "Collection",
        "ReadOnlyCollection", "ReadOnlyDictionary", "Dictionary", "ConcurrentDictionary",
        "IQueryable", "KeyValuePair"
    };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "輸出契約屬性標註了 JsonPropertyName，Agent 將讀到非詞庫鍵名",
        "輸出契約屬性 '{0}' 標註了 JsonPropertyName，Agent 將讀到非詞庫鍵名 '{1}'；鏡射契約請標 internal 並止於 Service 層，輸出欄位請使用詞庫標準詞",
        "Design",
        DiagnosticSeverity.Info, // 過渡預設 suggestion（§6 #11）；下游遷移完成後以 .editorconfig 收為 warning
        isEnabledByDefault: true,
        description: "MCP tool output contracts must not use [JsonPropertyName]; JSON keys seen by agents must derive from standard-vocabulary C# property names (ADR-016). Mirror contracts belong behind internal visibility in the Service layer.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 可達性集合是編譯層狀態：CompilationStart 建一次並快取（§5.2、§6 #2）。
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var assemblyName = compilationContext.Compilation.Assembly.Name;
            if (ExemptAssemblyNames.Contains(assemblyName))
            {
                return;
            }

            var reachableTypes = BuildToolReturnReachabilityClosure(compilationContext.Compilation);
            if (reachableTypes.Count == 0)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var property = (IPropertySymbol)symbolContext.Symbol;
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    return;
                }

                var containingType = property.ContainingType;
                if (containingType == null
                    || containingType.DeclaredAccessibility != Accessibility.Public
                    || !reachableTypes.Contains(containingType))
                {
                    return;
                }

                var jsonName = GetJsonPropertyNameValue(property);
                if (jsonName == null)
                {
                    return;
                }

                symbolContext.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    property.Locations.Length > 0 ? property.Locations[0] : Location.None,
                    property.Name,
                    jsonName));
            }, SymbolKind.Property);
        });
    }

    /// <summary>
    /// §5.3：找所有 [McpServerTool] 方法 → 剝除容器取回傳元素型別 → 遞迴展開屬性／欄位型別，
    /// 所得可達集合即為本模組的輸出契約閉包。
    /// </summary>
    private static HashSet<INamedTypeSymbol> BuildToolReturnReachabilityClosure(Compilation compilation)
    {
        var reachable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var type in GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (!IsToolMethod(method))
                {
                    continue;
                }

                foreach (var elementType in UnwrapToNamedTypes(method.ReturnType))
                {
                    CollectReachable(elementType, reachable);
                }
            }
        }

        return reachable;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamedTypeSymbol namedType)
            {
                yield return namedType;
                foreach (var nested in GetAllTypes(namedType))
                {
                    yield return nested;
                }
            }
            else if (member is INamespaceSymbol nestedNamespace)
            {
                foreach (var nested in GetAllTypes(nestedNamespace))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamedTypeSymbol typeSymbol)
    {
        foreach (var member in typeSymbol.GetTypeMembers())
        {
            yield return member;
            foreach (var nested in GetAllTypes(member))
            {
                yield return nested;
            }
        }
    }

    private static bool IsToolMethod(IMethodSymbol method)
    {
        return method.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "McpServerToolAttribute" or "McpServerTool" or "McpToolAttribute" or "McpTool");
    }

    /// <summary>
    /// 剝除 Task&lt;&gt;／IAsyncEnumerable&lt;&gt;／IReadOnlyList&lt;&gt; 等容器與元組，取得實際元素型別。
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> UnwrapToNamedTypes(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            foreach (var element in UnwrapToNamedTypes(arrayType.ElementType))
            {
                yield return element;
            }
            yield break;
        }

        if (type is not INamedTypeSymbol named)
        {
            yield break;
        }

        if (named.IsTupleType)
        {
            foreach (var argument in named.TypeArguments)
            {
                foreach (var element in UnwrapToNamedTypes(argument))
                {
                    yield return element;
                }
            }
            yield break;
        }

        if (IsContainer(named))
        {
            foreach (var argument in named.TypeArguments)
            {
                foreach (var element in UnwrapToNamedTypes(argument))
                {
                    yield return element;
                }
            }
            yield break;
        }

        if (named.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface)
        {
            yield return named;
        }
    }

    private static bool IsContainer(INamedTypeSymbol named)
    {
        var ns = named.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!ns.StartsWith("System", StringComparison.Ordinal))
        {
            return false;
        }

        if (!named.IsGenericType)
        {
            // 非泛型 Task／ValueTask 的回傳本身不含輸出契約。
            return named.OriginalDefinition.Name is "Task" or "ValueTask";
        }

        return ContainerTypeNames.Contains(named.OriginalDefinition.Name);
    }

    /// <summary>
    /// 從輸出根型別遞迴展開：基類、public 執行個體屬性與欄位的型別皆納入閉包（序列化可見面）。
    /// </summary>
    private static void CollectReachable(INamedTypeSymbol type, HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return;
        }

        if (type.BaseType is INamedTypeSymbol baseType
            && baseType.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum))
        {
            CollectReachable(baseType, visited);
        }

        foreach (var member in type.GetMembers())
        {
            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol property when !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public => property.Type,
                IFieldSymbol field when !field.IsStatic && !field.IsImplicitlyDeclared && field.DeclaredAccessibility == Accessibility.Public => field.Type,
                _ => null
            };

            if (memberType == null)
            {
                continue;
            }

            foreach (var element in UnwrapToNamedTypes(memberType))
            {
                CollectReachable(element, visited);
            }
        }
    }

    private static string? GetJsonPropertyNameValue(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass?.Name is not ("JsonPropertyNameAttribute" or "JsonPropertyName"))
            {
                continue;
            }

            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Value is string name)
                {
                    return name;
                }
            }

            return string.Empty;
        }

        return null;
    }
}
