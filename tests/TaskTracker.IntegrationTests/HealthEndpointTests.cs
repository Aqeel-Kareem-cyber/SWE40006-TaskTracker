using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskTracker.IntegrationTests.Infrastructure;

namespace TaskTracker.IntegrationTests;

/// <summary>
/// Operational endpoints used by the pipeline: /health and /healthz (both include a real
/// database query) and /version (the commit the running build was made from).
/// </summary>
public class HealthEndpointTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    public async Task Health_ReturnsOkAndHealthy(string path)
    {
        var response = await Client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Version_ReturnsCommitAndEnvironmentAsJson()
    {
        var response = await Client.GetAsync("/version", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        // No APP_VERSION is baked into a local/test build, so it reports "dev";
        // the Docker image sets APP_VERSION to the commit SHA at build time.
        Assert.Equal("dev", json.RootElement.GetProperty("commit").GetString());
        Assert.Equal("Development", json.RootElement.GetProperty("environment").GetString());
    }

    [Fact]
    public async Task TaskPage_LoadsWithEmptyDatabase()
    {
        // Proves migrations ran against the temp database: the page queries the Tasks table.
        var response = await Client.GetAsync("/Tasks", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No tasks found", html);
    }
}

/// <summary>
/// Proves the health check really tests the database (Project Description, Level 3:
/// "verify ... database connections"). Separate class = separate database, because this
/// test deliberately breaks its own schema.
/// </summary>
public class HealthDatabaseFailureTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Healthz_ReportsUnhealthy_WhenTheTasksTableIsUnavailable()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/healthz", Ct)).StatusCode);

        // Simulate a broken database: the table the health query reads no longer exists.
        await Factory.QueryAsync(db => db.Database.ExecuteSqlRawAsync("DROP TABLE Tasks"));

        var response = await Client.GetAsync("/healthz", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(Ct));
    }

    // The base class empties the Tasks table before each test; skip that here
    // because this class's only test drops the table.
    public override ValueTask InitializeAsync() => ValueTask.CompletedTask;
}
