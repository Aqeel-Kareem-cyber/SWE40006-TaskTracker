using System.Net;
using TaskTracker.IntegrationTests.Infrastructure;

namespace TaskTracker.IntegrationTests;

/// <summary>Every page the app serves responds successfully against a fresh database.</summary>
public class PageSmokeTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Tasks")]
    [InlineData("/Privacy")]
    [InlineData("/Error")]
    public async Task Page_ReturnsOk(string url)
    {
        var response = await Client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
