using System.Net;
using System.Text.RegularExpressions;

namespace TaskTracker.IntegrationTests.Infrastructure;

/// <summary>
/// Razor Pages forms are protected by an antiforgery token. A real browser gets it from a hidden
/// input on the page; these helpers do the same so tests submit forms exactly like a user would.
/// </summary>
public static partial class FormHelper
{
    // Matches the hidden input in either attribute order:
    // <input name="__RequestVerificationToken" type="hidden" value="..." />
    [GeneratedRegex(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|<input[^>]*value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    public static string ExtractAntiforgeryToken(string html)
    {
        var match = TokenRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("No __RequestVerificationToken found in the page HTML.");
        }

        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }

    /// <summary>
    /// GETs <paramref name="pageUrl"/> to obtain a fresh antiforgery token + cookie,
    /// then POSTs the form fields (plus the token) to <paramref name="postUrl"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string pageUrl,
        string postUrl,
        IDictionary<string, string> fields,
        CancellationToken ct)
    {
        var page = await client.GetAsync(pageUrl, ct);
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync(ct));

        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = token
        };

        return await client.PostAsync(postUrl, new FormUrlEncodedContent(form), ct);
    }
}
