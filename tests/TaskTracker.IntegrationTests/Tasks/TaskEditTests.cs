using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.IntegrationTests.Infrastructure;
using TaskTracker.Models;

namespace TaskTracker.IntegrationTests.Tasks;

/// <summary>Edit workflow: GET and POST /Tasks/Edit/{id}.</summary>
public class TaskEditTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    private Task<WorkTask> SeedOriginalAsync()
        => Factory.SeedAsync(new WorkTask
        {
            Title = "Original title",
            Description = "Original description",
            Priority = "Low",
            Status = "To Do"
        }).ContinueWith(t => t.Result[0]);

    private Task<WorkTask> LoadAsync(int id)
        => Factory.QueryAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == id));

    private Task<HttpResponseMessage> PostEditAsync(int id, Dictionary<string, string> fields)
        => FormHelper.PostFormAsync(Client, "/Tasks", $"/Tasks/Edit/{id}", fields, Ct);

    [Fact]
    public async Task EditPage_ExistingTask_ShowsCurrentValues()
    {
        var task = await SeedOriginalAsync();

        var response = await Client.GetAsync($"/Tasks/Edit/{task.Id}", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("value=\"Original title\"", html);
        Assert.Contains("Original description", html);
    }

    [Fact]
    public async Task EditPage_MissingTask_Returns404()
    {
        var response = await Client.GetAsync("/Tasks/Edit/99999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Edit_ValidChanges_AreSavedAndTitleIsTrimmed()
    {
        var task = await SeedOriginalAsync();

        var response = await PostEditAsync(task.Id, TaskForm.Edit(
            "   Updated title   ", "Updated description", "High", "In Progress", "2026-11-15"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Tasks", response.Headers.Location?.OriginalString);

        var stored = await LoadAsync(task.Id);
        Assert.Equal("Updated title", stored.Title);
        Assert.Equal("Updated description", stored.Description);
        Assert.Equal("High", stored.Priority);
        Assert.Equal("In Progress", stored.Status);
        Assert.Equal(new DateTime(2026, 11, 15), stored.DueDate);
    }

    [Fact]
    public async Task Edit_ClearingDueDateAndDescription_IsSaved()
    {
        var task = (await Factory.SeedAsync(new WorkTask
        {
            Title = "Has extras", Description = "Remove me", DueDate = new DateTime(2026, 12, 1)
        }))[0];

        await PostEditAsync(task.Id, TaskForm.Edit("Has extras"));

        var stored = await LoadAsync(task.Id);
        Assert.Null(stored.DueDate);
        Assert.Null(stored.Description);
    }

    [Theory]
    [InlineData("", "Medium", "To Do", "Task title is required.")]
    [InlineData("   ", "Medium", "To Do", "Task title is required.")] // whitespace-only is empty after trimming
    [InlineData("TOO_LONG", "Medium", "To Do", "Title must be between 1 and 100 characters.")]
    [InlineData("Valid title", "Medium", "Done", "Please select a valid status.")]
    [InlineData("Valid title", "Urgent", "To Do", "Please select a valid priority.")]
    public async Task Edit_InvalidInput_IsRejectedAndTaskIsUnchanged(
        string title, string priority, string status, string expectedMessage)
    {
        var task = await SeedOriginalAsync();
        if (title == "TOO_LONG") title = new string('a', 101); // InlineData can't build a 101-char string

        var response = await PostEditAsync(task.Id, TaskForm.Edit(title, priority: priority, status: status));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedMessage, await response.Content.ReadAsStringAsync(Ct));

        var stored = await LoadAsync(task.Id);
        Assert.Equal("Original title", stored.Title);
        Assert.Equal("To Do", stored.Status);
        Assert.Equal("Low", stored.Priority);
    }

    [Fact]
    public async Task Edit_OneCharacterTitle_IsAccepted()
    {
        var task = await SeedOriginalAsync();

        var response = await PostEditAsync(task.Id, TaskForm.Edit(" Z "));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Z", (await LoadAsync(task.Id)).Title);
    }

    [Fact]
    public async Task Edit_MissingTask_Returns404()
    {
        var response = await PostEditAsync(99999, TaskForm.Edit("Valid title"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
