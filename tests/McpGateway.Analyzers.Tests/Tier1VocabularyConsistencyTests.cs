using System;
using System.Linq;
using Xunit;

namespace McpGateway.Analyzers.Tests;

/// <summary>
/// 詞庫結構一致性測試（Issue #14 §5.3/§7.1）。
/// 這些測試不驗證語意正確性，只強制「名稱佔用的唯一性」——
/// 避免 FindByAlias 的 FirstOrDefault 靜默吞掉別名衝突
/// （startTime 曾被 startHours 與自身標準詞同時佔用而無任何警告）。
/// </summary>
public class Tier1VocabularyConsistencyTests
{
    [Fact]
    public void Tier1Vocabulary_HasNoAmbiguousAliases()
    {
        var errors = Tier1VocabularyValidator.Validate(Tier1Vocabulary.Entries);
        Assert.Empty(errors);
    }

    // 以下為驗證器自身的反例測試：確認規則真的會觸發，
    // 而非因實作錯誤（如 camel/Pascal 合併比對）永遠通過或永遠失敗。

    [Fact]
    public void Validator_DetectsAliasCollidingWithStandardName()
    {
        var entries = new[]
        {
            new GlossaryEntry("startHours", "StartHours", "d", new[] { "startTime" }),
            new GlossaryEntry("startTime", "StartTime", "d", Array.Empty<string>()),
        };

        var errors = Tier1VocabularyValidator.Validate(entries);
        Assert.Contains(errors, e => e.Contains("startTime") && e.Contains("撞上標準名稱"));
    }

    [Fact]
    public void Validator_DetectsAliasOwnedByTwoEntries()
    {
        var entries = new[]
        {
            new GlossaryEntry("processId", "ProcessId", "d", new[] { "procCode" }),
            new GlossaryEntry("stepId", "StepId", "d", new[] { "procCode" }),
        };

        var errors = Tier1VocabularyValidator.Validate(entries);
        Assert.Contains(errors, e => e.Contains("procCode") && e.Contains("同時屬於"));
    }

    [Fact]
    public void Validator_DetectsDuplicateStandardNamesWithinSameCaseStyle()
    {
        var entries = new[]
        {
            new GlossaryEntry("lotId", "LotId", "d", Array.Empty<string>()),
            new GlossaryEntry("lotId", "LotId2", "d", Array.Empty<string>()),
        };

        var errors = Tier1VocabularyValidator.Validate(entries);
        Assert.Contains(errors, e => e.Contains("camelCase") && e.Contains("lotId"));
    }

    [Fact]
    public void Validator_DoesNotFlagCamelAgainstItsOwnPascalForm()
    {
        // 規則 1 的實作陷阱：camel 與其對應 Pascal 是同一詞條的兩種形式，不得互相比對。
        var entries = new[]
        {
            new GlossaryEntry("productId", "ProductId", "d", new[] { "prodId" }),
        };

        Assert.Empty(Tier1VocabularyValidator.Validate(entries));
    }
}
