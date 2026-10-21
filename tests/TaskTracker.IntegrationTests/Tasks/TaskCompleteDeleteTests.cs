using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.IntegrationTests.Infrastructure;
using TaskTracker.Models;

namespace TaskTracker.IntegrationTests.Tasks;

/// <summary>Complete and delete workflows (the row buttons on /Tasks).</summary>
public class TaskCompleteDeleteTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    private static readonly Dictionary<string, string> NoFields = new();

    private Task<HttpResponseMessage> PostHandlerAsync(string handler, int id)
        => FormHelper.PostFormAsync(Client, "/Tasks", $"/Tasks?handler={handler}&id={id}", NoFields, Ct);

    [Fact]
    public async Task Complete_ExistingTask_MarksItCompletedWithoutRemovingIt()
    {
        var task = (await Factory.SeedAsync(new WorkTask { Title = "Finish report" }))[0];

        var response = await PostHandlerAsync("Complete", task.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var stored = await Factory.QueryAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id));
        Assert.Equal("Completed", stored.Status);
        Assert.Equal("Finish report", stored.Title);
    }

    [Fact]
    public async Task Complete_AfterReload_TaskShowsNoCompleteButton()
    {
        var task = (await Factory.SeedAsync(new WorkTask { Title = "Reload check" }))[0];

        await PostHandlerAsync("Complete", task.Id);
        var html = await Client.GetStringAsync("/Tasks", Ct);

        Assert.Contains("Reload check", html);
        Assert.DoesNotContain("handler=Complete", html); // button is only rendered for unfinished tasks
    }

    [Fact]
    public async Task Complete_MissingTask_Returns404()
    {
        var response = await PostHandlerAsync("Complete", 99999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingTask_RemovesOnlyThatTask()
    {
        var seeded = await Factory.SeedAsync(
            new WorkTask { Title = "Delete me" },
            new WorkTask { Title = "Keep me", Status = "In Progress" });

        var response = await PostHandlerAsync("Delete", seeded[0].Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var remaining = Assert.Single(await Factory.QueryAsync(db => db.Tasks.AsNoTracking().ToListAsync()));
        Assert.Equal("Keep me", remaining.Title);
        Assert.Equal("In Progress", remaining.Status); // other records keep their state

        var html = await Client.GetStringAsync("/Tasks", Ct);
        Assert.DoesNotContain("Delete me", html);       // still gone after a reload
    }

    [Fact]
    public async Task Delete_MissingTask_Returns404()
    {
        var response = await PostHandlerAsync("Delete", 99999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
