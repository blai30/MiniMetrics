using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class GitHubReleaseSourceTests
{
    [TestMethod]
    public void ParseRelease_reads_tag_and_url()
    {
        const string json = """
        { "tag_name": "v1.3.0", "html_url": "https://github.com/blai30/MiniMetrics/releases/tag/v1.3.0", "draft": false }
        """;

        ReleaseInfo? release = GitHubReleaseSource.ParseRelease(json);

        Assert.IsNotNull(release);
        Assert.AreEqual("v1.3.0", release!.TagName);
        Assert.AreEqual("https://github.com/blai30/MiniMetrics/releases/tag/v1.3.0", release.HtmlUrl);
    }

    [TestMethod]
    public void ParseRelease_returns_null_when_a_field_is_missing()
    {
        Assert.IsNull(GitHubReleaseSource.ParseRelease("""{ "tag_name": "v1.3.0" }"""));
    }

    [TestMethod]
    public void ParseRelease_returns_null_for_garbage()
    {
        Assert.IsNull(GitHubReleaseSource.ParseRelease("not json"));
        Assert.IsNull(GitHubReleaseSource.ParseRelease("[]"));
    }
}
