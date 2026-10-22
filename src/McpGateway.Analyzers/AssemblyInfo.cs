using System.Runtime.CompilerServices;

// 詞庫一致性測試（Tier1VocabularyConsistencyTests）需存取 internal 的
// Tier1Vocabulary / Tier1VocabularyValidator（Issue #14 §5.3）。
[assembly: InternalsVisibleTo("McpGateway.Analyzers.Tests")]
