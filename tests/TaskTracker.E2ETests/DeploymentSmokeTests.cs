using System.Diagnostics;
using System.Net;
using System.Text.Json;
using TaskTracker.E2ETests.Infrastructure;

namespace TaskTracker.E2ETests;

/// <summary>
/// Post-deployment verification (Brief §5.1 "Staging integration" and "Production verification").
/// Plain HTTP, no browser, safe for production: it only reads, plus one labelled SMOKE task
/// that it creates and deletes again.
///   Staging    (pipeline job 4): runs together with the browser tests.
///   Production (pipeline job 6): dotnet test --filter "Category=Smoke"
/// </summary>
[Trait("Category", "Smoke")]
public sealed class DeploymentSmokeTests : IDisposable
{
    private readonly AppHttpClient _app = new(E2ESettings.BaseUrl ?? "http://localhost");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [E2EFact]
    public async Task Health_ReportsHealthy()
    {
        var response = await _app.GetAsync(E2ESettings.HealthPath, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [E2EFact]
    public async Task Version_ReportsTheCommitBeingReleased()
    {
        // The pipeline passes the commit it just deployed; locally there is nothing to compare against.
        Assert.SkipWhen(E2ESettings.ExpectedCommit is null,
            "E2E_EXPECTED_COMMIT is not set (the pipeline sets it to the commit being released).");

        var response = await _app.GetAsync("/version", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(E2ESettings.ExpectedCommit!, await response.Content.ReadAsStringAsync(Ct),
            StringComparison.OrdinalIgnoreCase);
    }

    [E2EFact]
    public async Task TaskList_ReturnsOk()
    {
        var response = await _app.GetAsync("/Tasks", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Task Management", await response.Content.ReadAsStringAsync(Ct));
    }

    [E2EFact]
    public async Task SmokeTask_CanBeCreatedAndDeleted()
    {
        // Proves the deployed app can write to and delete from its database, leaving nothing behind.
        var title = $"SMOKE-{E2ESettings.SmokeLabel}-{Guid.NewGuid().ToString("N")[..6]}";

        var created = await _app.PostFormAsync("/Tasks", "/Tasks",
            new Dictionary<string, string> { ["NewTask.Title"] = title }, Ct);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var listHtml = await (await _app.GetAsync("/Tasks", Ct)).Content.ReadAsStringAsync(Ct);
        var id = AppHttpClient.FindTaskId(listHtml, title);
        Assert.NotNull(id);

        var deleted = await _app.PostFormAsync("/Tasks", $"/Tasks?handler=Delete&id={id}",
            new Dictionary<string, string>(), Ct);
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);

        listHtml = await (await _app.GetAsync("/Tasks", Ct)).Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(title, listHtml);
    }

    [E2EFact]
    public async Task Latency_P95IsWithinTargetWithNoErrors()
    {
        // Brief §5.2: 100 sequential GET requests to the task list and health endpoint; p95 <= 500 ms, 0 errors.
        const int requests = 100;
        var paths = new[] { "/Tasks", E2ESettings.HealthPath };
        var timings = new List<double>(requests);
        var errors = 0;

        await _app.GetAsync("/Tasks", Ct); // warm-up request, not measured

        for (var i = 0; i < requests; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await _app.GetAsync(paths[i % paths.Length], Ct);
            await response.Content.ReadAsByteArrayAsync(Ct);
            stopwatch.Stop();

            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
            if (!response.IsSuccessStatusCode) errors++;
        }

        timings.Sort();
        var p50 = timings[(int)Math.Ceiling(0.50 * requests) - 1];
        var p95 = timings[(int)Math.Ceiling(0.95 * requests) - 1];

        // Keep the numbers as evidence for the metrics export (Section 5.2) and print them in the test log.
        Directory.CreateDirectory(E2ESettings.ArtifactsDir);
        var result = new
        {
            baseUrl = E2ESettings.BaseUrl,
            commit = E2ESettings.ExpectedCommit,
            measuredAtUtc = DateTime.UtcNow,
            requests,
            errors,
            p50Ms = Math.Round(p50, 1),
            p95Ms = Math.Round(p95, 1),
            maxMs = Math.Round(timings[^1], 1),
            targetP95Ms = E2ESettings.SmokeP95Milliseconds
        };
        await File.WriteAllTextAsync(
            Path.Combine(E2ESettings.ArtifactsDir, "latency.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), Ct);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Latency over {requests} requests: p50 {p50:F1} ms, p95 {p95:F1} ms, max {timings[^1]:F1} ms, errors {errors}");

        Assert.Equal(0, errors);
        Assert.True(p95 <= E2ESettings.SmokeP95Milliseconds,
            $"p95 latency {p95:F1} ms exceeds the {E2ESettings.SmokeP95Milliseconds} ms target.");
    }

    public void Dispose() => _app.Dispose();
}
