using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace McpGateway.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(McpNamingAndDescriptionCodeFixProvider)), Shared]
public class McpNamingAndDescriptionCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias,
            McpNamingAndDescriptionAnalyzer.DiagnosticIdMissingDesc);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindNode(diagnosticSpan);

        if (diagnostic.Id == McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias)
        {
            // 此處另行呼叫 FindByAlias（非從 Diagnostic 取標準名），因此需自行取用
            // aliasMatch.Description 帶出詞條語意，讓開發者在套用前即可判斷是否改名。
            var aliasMatch = Tier1Vocabulary.FindByAlias(node.ToString().Trim('\"', ' '));
            if (aliasMatch != null)
            {
                var standardName = aliasMatch.StandardPascal;
                var title = $"改名為標準詞 '{standardName}'（{aliasMatch.Description}）";
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedSolution: c => RenameSymbolAsync(context.Document, node, standardName, c),
                        equivalenceKey: nameof(McpNamingAndDescriptionAnalyzer.DiagnosticIdAlias)),
                    diagnostic);
            }
        }
        else if (diagnostic.Id == McpNamingAndDescriptionAnalyzer.DiagnosticIdMissingDesc)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "加上 [Description(\"...\")] 特性",
                    createChangedDocument: c => AddDescriptionAttributeAsync(context.Document, node, c),
                    equivalenceKey: nameof(McpNamingAndDescriptionAnalyzer.DiagnosticIdMissingDesc)),
                diagnostic);
        }
    }

    private static async Task<Solution> RenameSymbolAsync(
        Document document,
        SyntaxNode node,
        string newName,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return document.Project.Solution;

        var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) ??
                     semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;

        if (symbol == null) return document.Project.Solution;

        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        return await Renamer.RenameSymbolAsync(originalSolution, symbol, newName, optionSet, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Document> AddDescriptionAttributeAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Check if node is a PropertyDeclarationSyntax or ParameterSyntax
        if (node is PropertyDeclarationSyntax propertyDecl)
        {
            var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory.IdentifierName("Description"),
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal("欄位說明"))))))));

            var newPropertyDecl = propertyDecl.AddAttributeLists(attributeList);
            var newRoot = root.ReplaceNode(propertyDecl, newPropertyDecl);
            return document.WithSyntaxRoot(newRoot);
        }
        else if (node is ParameterSyntax parameterSyntax)
        {
            var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.PropertyKeyword)),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory.IdentifierName("Description"),
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal("欄位說明"))))))));

            var newParam = parameterSyntax.AddAttributeLists(attributeList);
            var newRoot = root.ReplaceNode(parameterSyntax, newParam);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}
