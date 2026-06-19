using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMetrics.Services;

// Reads the latest published release from the GitHub REST API. The /releases/latest endpoint omits
// drafts and prereleases, so a release is visible to clients only after it is published. Any network,
// timeout, or parse failure collapses to null.
public sealed class GitHubReleaseSource : IReleaseSource
{
    private const string LatestUrl = "https://api.github.com/repos/blai30/MiniMetrics/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync(LatestUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseRelease(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    // Extracts tag_name and html_url from a GitHub release JSON object. Returns null if the payload is
    // not an object or either field is missing or empty. Pure, so it is unit-tested directly.
    public static ReleaseInfo? ParseRelease(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (!root.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("html_url", out var url) || url.ValueKind != JsonValueKind.String)
                return null;

            string? tagName = tag.GetString();
            string? htmlUrl = url.GetString();
            if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(htmlUrl)) return null;

            return new(tagName, htmlUrl);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub rejects API requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MiniMetrics-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
