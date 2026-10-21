using System.Net;
using System.Text.RegularExpressions;

namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// Plain HTTP access to a deployed TaskTracker (no browser): keeps cookies so Razor Pages
/// antiforgery works, and does not follow redirects so Post-Redirect-Get can be asserted.
/// </summary>
public sealed partial class AppHttpClient : IDisposable
{
    private readonly HttpClient _http;

    public AppHttpClient(string baseUrl)
    {
        _http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false
        })
        {
            BaseAddress = new Uri(baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct)
        => _http.GetAsync(path.TrimStart('/'), ct);

    public async Task<HttpResponseMessage> PostFormAsync(
        string pagePath, string postPath, IDictionary<string, string> fields, CancellationToken ct)
    {
        var page = await GetAsync(pagePath, ct);
        page.EnsureSuccessStatusCode();
        var match = TokenRegex().Match(await page.Content.ReadAsStringAsync(ct));
        if (!match.Success)
        {
            throw new InvalidOperationException($"No antiforgery token found on {pagePath}.");
        }

        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value)
        };
        return await _http.PostAsync(postPath.TrimStart('/'), new FormUrlEncodedContent(form), ct);
    }

    /// <summary>Finds the database ID of a task on /Tasks by its (unique) title, via its Edit link.</summary>
    public static int? FindTaskId(string tasksPageHtml, string title)
    {
        var titleIndex = tasksPageHtml.IndexOf(WebUtility.HtmlEncode(title), StringComparison.Ordinal);
        if (titleIndex < 0)
        {
            return null;
        }

        var edit = Regex.Match(tasksPageHtml[titleIndex..], "href=\"/Tasks/Edit/(\\d+)\"");
        return edit.Success ? int.Parse(edit.Groups[1].Value) : null;
    }

    public void Dispose() => _http.Dispose();
}
