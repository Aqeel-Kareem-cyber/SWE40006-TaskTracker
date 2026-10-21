namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// All E2E configuration comes from environment variables so the same tests can target
/// a local app, the staging server (pipeline job 4) or anything else without code changes.
///   E2E_BASE_URL         required, e.g. http://localhost:5075 or http://&lt;staging-host&gt;
///   E2E_BROWSER_CHANNEL  optional, e.g. "chrome" or "msedge" to use an installed browser
///   E2E_HEADED           optional, "1" shows the browser window (local debugging)
///   E2E_ARTIFACTS_DIR    optional, where failure screenshots/traces are written
/// </summary>
public static class E2ESettings
{
    public const string NotConfiguredReason =
        "E2E_BASE_URL is not set. Set it to the URL of a running TaskTracker app to run the browser tests.";

    public static string? BaseUrl
        => Environment.GetEnvironmentVariable("E2E_BASE_URL")?.Trim().TrimEnd('/') is { Length: > 0 } url ? url : null;

    // Used by [E2EFact] to decide whether to run or skip.
    public static bool IsConfigured => BaseUrl is not null;

    public static string? BrowserChannel
        => Environment.GetEnvironmentVariable("E2E_BROWSER_CHANNEL") is { Length: > 0 } channel ? channel : null;

    public static bool Headed => Environment.GetEnvironmentVariable("E2E_HEADED") == "1";

    public static string ArtifactsDir
        => Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    // Unique per pipeline run (GitHub run number) or per local run, used in task titles: E2E-<run>-...
    public static string RunId { get; } =
        Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? DateTime.UtcNow.ToString("HHmmss");
}
