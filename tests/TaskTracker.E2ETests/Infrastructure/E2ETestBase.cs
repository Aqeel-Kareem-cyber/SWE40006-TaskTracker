using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// Base class for browser tests:
/// - a fresh, isolated BrowserContext + Page per test
/// - every task a test creates gets a unique "E2E-&lt;run&gt;-" title and is deleted afterwards
/// - the browser's "Delete this task?" confirm() dialog is accepted automatically
/// - a Playwright trace + screenshot is saved when a test fails
/// </summary>
[Collection(BrowserCollection.Name)]
public abstract class E2ETestBase(BrowserFixture browser) : IAsyncLifetime
{
    private readonly List<string> _createdTitles = new();

    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!E2ESettings.IsConfigured)
        {
            return;
        }

        Context = await browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl
        });

        await Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        Page = await Context.NewPageAsync();
        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!E2ESettings.IsConfigured || Context is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        var testState = TestContext.Current.TestState;
        var failedOrUnknown = testState is null || testState.Result == TestResult.Failed;
        var safeName = Regex.Replace(TestContext.Current.Test?.TestDisplayName ?? "test", "[^A-Za-z0-9_.-]", "_");
        Directory.CreateDirectory(E2ESettings.ArtifactsDir);

        if (failedOrUnknown)
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(E2ESettings.ArtifactsDir, $"{safeName}.png"),
                FullPage = true
            });
            await Context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(E2ESettings.ArtifactsDir, $"{safeName}.trace.zip")
            });
        }
        else
        {
            await Context.Tracing.StopAsync();
        }

        await CleanUpCreatedTasksAsync();
        await Context.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    // ---------- Helpers shared by the tests ----------

    protected string UniqueTitle(string label)
    {
        var title = $"E2E-{E2ESettings.RunId}-{label}-{Guid.NewGuid().ToString("N")[..6]}";
        _createdTitles.Add(title);
        return title;
    }

    protected Task GoToTasksAsync() => Page.GotoAsync("/Tasks");

    /// <summary>The table row on /Tasks that contains this (unique) task title.</summary>
    protected ILocator TaskRow(string title)
        => Page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = title });

    protected async Task CreateTaskAsync(string title)
    {
        await GoToTasksAsync();
        await Page.GetByLabel("Task Name").FillAsync(title);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Task" }).ClickAsync();
        await Expect(TaskRow(title)).ToBeVisibleAsync();
    }

    private async Task CleanUpCreatedTasksAsync()
    {
        // Best-effort: delete every task this test created. Never fails the test
        // (staging data is rebuilt on every deployment anyway).
        foreach (var title in _createdTitles)
        {
            try
            {
                await GoToTasksAsync();
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    var remaining = await TaskRow(title).CountAsync();
                    if (remaining == 0)
                    {
                        break;
                    }

                    await TaskRow(title).First
                        .GetByRole(AriaRole.Button, new() { Name = "Delete" })
                        .ClickAsync(new LocatorClickOptions { Timeout = 5_000 });

                    // Wait for the row to actually disappear (not for a page-load event).
                    await Expect(TaskRow(title)).ToHaveCountAsync(remaining - 1, new() { Timeout = 5_000 });
                }
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                // ignore and continue with the next task
            }
        }
    }
}
