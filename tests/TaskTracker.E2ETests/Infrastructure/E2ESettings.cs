namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// All E2E configuration comes from environment variables so the same tests can target
/// a local app, the staging server (pipeline job 4) or anything else without code changes.
///   E2E_BASE_URL         required, e.g. http://localhost:5075 or http://&lt;staging-host&gt;
///   E2E_BROWSER_CHANNEL  optional, e.g. "chrome" or "msedge" to use an installed browser
///   E2E_HEADED           optional, "1" shows the browser window (local debugging)
///   E2E_ARTIFACTS_DIR    optional, where failure screenshots/traces/latency results are written
///   E2E_HEALTH_PATH      optional, health endpoint path (default /health; brief target is /healthz)
///   E2E_EXPECTED_COMMIT  optional, commit SHA the deployed /version must report (set by the pipeline)
///   SMOKE_P95_MS         optional, latency target for the smoke sample (default 500 ms, Brief §5.2)
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

    public static string HealthPath
        => Environment.GetEnvironmentVariable("E2E_HEALTH_PATH") is { Length: > 0 } path ? path : "/health";

    public static string? ExpectedCommit
        => Environment.GetEnvironmentVariable("E2E_EXPECTED_COMMIT") is { Length: > 0 } sha ? sha.Trim() : null;

    public static int SmokeP95Milliseconds
        => int.TryParse(Environment.GetEnvironmentVariable("SMOKE_P95_MS"), out var ms) ? ms : 500;

    // Label for the smoke-test task: SMOKE-<short commit> in the pipeline, SMOKE-<run id> locally.
    public static string SmokeLabel
        => ExpectedCommit is { } sha ? sha[..Math.Min(7, sha.Length)] : RunId;
}
