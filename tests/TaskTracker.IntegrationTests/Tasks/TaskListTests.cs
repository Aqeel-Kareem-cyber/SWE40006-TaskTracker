using System.Net;
using TaskTracker.IntegrationTests.Infrastructure;
using TaskTracker.Models;

namespace TaskTracker.IntegrationTests.Tasks;

/// <summary>View workflow: GET /Tasks reads what is stored and renders it.</summary>
public class TaskListTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task List_EmptyDatabase_ShowsEmptyMessageAndForm()
    {
        var html = await Client.GetStringAsync("/Tasks", Ct);

        Assert.Contains("No tasks found", html);
        Assert.Contains("0 Total", html);
        Assert.Contains("Create Task", html); // creation form still available
    }

    [Fact]
    public async Task List_SeededTasks_AreShownNewestFirstWithTheirDetails()
    {
        await Factory.SeedAsync(
            new WorkTask { Title = "Older task", Priority = "Low", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new WorkTask
            {
                Title = "Newer task",
                Description = "Has a due date",
                Priority = "High",
                Status = "In Progress",
                DueDate = new DateTime(2026, 12, 31),
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });

        var response = await Client.GetAsync("/Tasks", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("2 Total", html);
        Assert.True(html.IndexOf("Newer task", StringComparison.Ordinal)
                    < html.IndexOf("Older task", StringComparison.Ordinal), "Newest task should be listed first.");
        Assert.Contains("Has a due date", html);
        Assert.Contains("31 Dec 2026", html);
        Assert.Contains("Not set", html); // older task has no due date
        Assert.DoesNotContain("No tasks found", html);
    }

    [Fact]
    public async Task List_CreatedTask_IsStillThereAfterAppRestart()
    {
        await FormHelper.PostFormAsync(Client, "/Tasks", "/Tasks", TaskForm.New("Survives restart"), Ct);

        // A second, completely new app instance pointed at the same SQLite file = an app restart.
        await using var restartedApp = Factory.WithWebHostBuilder(_ => { });
        using var freshClient = restartedApp.CreateClient();

        var html = await freshClient.GetStringAsync("/Tasks", Ct);

        Assert.Contains("Survives restart", html);
    }
}
