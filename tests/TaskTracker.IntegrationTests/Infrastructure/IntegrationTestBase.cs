namespace TaskTracker.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for component integration tests.
/// - One TaskTrackerFactory (and therefore one temp database) per test class.
/// - The database is emptied before every test (xUnit also creates a new class instance per test).
/// - Redirects are NOT followed automatically, so tests can assert the Post-Redirect-Get behaviour.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<TaskTrackerFactory>, IAsyncLifetime
{
    protected TaskTrackerFactory Factory { get; }
    protected HttpClient Client { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected IntegrationTestBase(TaskTrackerFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            HandleCookies = true // keeps the antiforgery cookie between GET and POST
        });
    }

    public virtual async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public virtual ValueTask DisposeAsync()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
