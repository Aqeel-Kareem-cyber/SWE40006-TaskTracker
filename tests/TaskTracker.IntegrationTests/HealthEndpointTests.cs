using System.Net;
using TaskTracker.IntegrationTests.Infrastructure;

namespace TaskTracker.IntegrationTests;

/// <summary>
/// Smoke-level proof that the test host boots the real app (Program.cs, EF Core migrations,
/// temp SQLite database) and that the health endpoint responds.
/// </summary>
public class HealthEndpointTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Health_ReturnsOkAndHealthy()
    {
        var response = await Client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
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
