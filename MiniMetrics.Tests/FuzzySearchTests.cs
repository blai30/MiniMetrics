using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class FuzzySearchTests
{
    private const string Display = "English (United States)";
    private const string Key = "en-US";

    [TestMethod]
    public void Empty_query_matches_everything()
    {
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, ""));
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "   "));
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, null));
    }

    [TestMethod]
    public void Multiple_tokens_match_across_the_display_name_in_any_order()
    {
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "english uni"));
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "states english"));
    }

    [TestMethod]
    public void Matches_the_key_with_either_separator()
    {
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "en-US"));
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "en_US"));
    }

    [TestMethod]
    public void Matches_the_key_with_no_separator()
    {
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "enus"));
    }

    [TestMethod]
    public void Is_case_insensitive()
    {
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "ENGLISH"));
        Assert.IsTrue(FuzzySearch.Matches(Display, Key, "EN-us"));
    }

    [TestMethod]
    public void Works_for_time_zone_display_and_id()
    {
        Assert.IsTrue(FuzzySearch.Matches("(UTC-08:00) Pacific Time (US & Canada)", "Pacific Standard Time", "pacific"));
        Assert.IsTrue(FuzzySearch.Matches("(UTC-08:00) Pacific Time (US & Canada)", "Pacific Standard Time", "pacific standard"));
    }

    [TestMethod]
    public void Non_matching_query_is_rejected()
    {
        Assert.IsFalse(FuzzySearch.Matches(Display, Key, "german"));
        Assert.IsFalse(FuzzySearch.Matches("French (France)", "fr-FR", "english"));
    }

    [TestMethod]
    public void Every_token_must_match_not_just_one()
    {
        Assert.IsFalse(FuzzySearch.Matches(Display, Key, "english zzz"));
    }
}
