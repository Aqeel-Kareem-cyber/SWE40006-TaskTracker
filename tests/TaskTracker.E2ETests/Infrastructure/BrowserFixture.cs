using Microsoft.Playwright;

namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// One Playwright instance and one Chromium browser shared by all E2E tests (starting a browser
/// is slow). Each test still gets its own isolated BrowserContext (cookies, storage) - see E2ETestBase.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!E2ESettings.IsConfigured)
        {
            return; // tests will be skipped; don't start a browser
        }

        await EnsureAppIsReachableAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !E2ESettings.Headed,
            Channel = E2ESettings.BrowserChannel
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    // Fail fast with a clear message instead of 8 confusing browser timeouts.
    private static async Task EnsureAppIsReachableAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var response = await http.GetAsync($"{E2ESettings.BaseUrl}/health");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"TaskTracker is not reachable at {E2ESettings.BaseUrl}/health. " +
                "Start the app first (locally: dotnet run --project TaskTracker --launch-profile http).", ex);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class BrowserCollection : ICollectionFixture<BrowserFixture>
{
    // Browser tests share one task list, so they run sequentially in a single collection.
    public const string Name = "Browser";
}
