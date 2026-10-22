using System;
using System.Collections.Generic;
using System.Linq;

namespace McpGateway.Analyzers;

/// <summary>
/// 詞庫結構一致性檢查（Issue #14 §5.3）。
/// 僅檢查「名稱佔用的唯一性」，不檢查語意正確性；
/// 由測試（Tier1VocabularyConsistencyTests）於 CI 強制執行，
/// 避免 FindByAlias 的 FirstOrDefault 靜默吞掉別名衝突。
/// </summary>
internal static class Tier1VocabularyValidator
{
    public static IReadOnlyList<string> Validate(GlossaryEntry[] entries)
    {
        var errors = new List<string>();
        if (entries == null || entries.Length == 0)
            return errors;

        // 規則 1：camel 與 Pascal 標準名稱各自不得重複。
        // 注意：兩組必須分別比對——camel/Pascal 是同一詞條的兩種大小寫形式，
        // 若合併為單一集合做大小寫不敏感分組，全部詞條都會誤報。
        CheckDuplicates(entries, e => e.StandardCamel, "camelCase");
        CheckDuplicates(entries, e => e.StandardPascal, "PascalCase");

        void CheckDuplicates(GlossaryEntry[] items, Func<GlossaryEntry, string> selector, string label)
        {
            var duplicates = items
                .Select(selector)
                .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);
            foreach (var g in duplicates)
                errors.Add($"標準名稱（{label}）重複：'{g.Key}' 出現 {g.Count()} 次");
        }

        var standardNames = new HashSet<string>(
            entries.SelectMany(e => new[] { e.StandardCamel, e.StandardPascal }),
            StringComparer.OrdinalIgnoreCase);

        // 規則 2：別名不得撞上任何標準名稱（含自己那條）。
        // 規則 3：同一別名不得同時屬於兩條詞條（FindByAlias 會靜默選第一個）。
        var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var alias in entry.Aliases ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    errors.Add($"{entry.StandardCamel}：別名不可為空白");
                    continue;
                }

                if (standardNames.Contains(alias))
                    errors.Add($"別名 '{alias}'（屬 {entry.StandardCamel}）撞上標準名稱");
                else if (owner.TryGetValue(alias, out var previousOwner))
                    errors.Add($"別名 '{alias}' 同時屬於 {previousOwner} 與 {entry.StandardCamel}");
                else
                    owner[alias] = entry.StandardCamel;
            }
        }

        return errors;
    }
}
