using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.IntegrationTests.Infrastructure;
using TaskTracker.Models;

namespace TaskTracker.IntegrationTests.Tasks;

/// <summary>
/// Create workflow: POST /Tasks through routing, model binding, validation and EF Core
/// into a real SQLite file, then assert on what was actually stored.
/// </summary>
public class TaskCreateTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    private Task<HttpResponseMessage> PostNewTaskAsync(Dictionary<string, string> fields)
        => FormHelper.PostFormAsync(Client, "/Tasks", "/Tasks", fields, Ct);

    private Task<List<WorkTask>> StoredTasksAsync()
        => Factory.QueryAsync(db => db.Tasks.AsNoTracking().ToListAsync());

    [Fact]
    public async Task Create_ValidTask_IsSavedAndRedirectsToTaskList()
    {
        var response = await PostNewTaskAsync(TaskForm.New(
            "Write integration tests", "Cover the create workflow", "High", "In Progress", "2026-12-31"));

        // Post-Redirect-Get back to the list
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Tasks", response.Headers.Location?.OriginalString);

        var task = Assert.Single(await StoredTasksAsync());
        Assert.Equal("Write integration tests", task.Title);
        Assert.Equal("Cover the create workflow", task.Description);
        Assert.Equal("High", task.Priority);
        Assert.Equal("In Progress", task.Status);
        Assert.Equal(new DateTime(2026, 12, 31), task.DueDate);
    }

    [Fact]
    public async Task Create_TitleOnly_UsesDefaultStatusAndPriority()
    {
        await PostNewTaskAsync(TaskForm.New("Title only task"));

        var task = Assert.Single(await StoredTasksAsync());
        Assert.Equal("To Do", task.Status);       // new tasks start incomplete
        Assert.Equal("Medium", task.Priority);
        Assert.Null(task.DueDate);
    }

    [Theory]
    [InlineData(1)]   // Brief §1.2: 1–100 characters after trimming
    [InlineData(2)]
    [InlineData(100)]
    public async Task Create_TitleAtLengthBoundary_IsAccepted(int length)
    {
        var title = new string('a', length);

        var response = await PostNewTaskAsync(TaskForm.New(title));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(title, Assert.Single(await StoredTasksAsync()).Title);
    }

    public static TheoryData<string, string> InvalidTitles => new()
    {
        { "", "Task title is required." },
        { "   ", "Task title is required." },
        { new string('a', 101), "Title must be between 1 and 100 characters." },
    };

    [Theory]
    [MemberData(nameof(InvalidTitles))]
    public async Task Create_InvalidTitle_IsRejectedAndNothingIsSaved(string title, string expectedMessage)
    {
        var response = await PostNewTaskAsync(TaskForm.New(title));
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-displayed, not redirected
        Assert.Contains(expectedMessage, html);
        Assert.Empty(await StoredTasksAsync());              // validation happens before any DB write
    }

    [Fact]
    public async Task Create_DescriptionOf500Characters_IsAccepted()
    {
        var response = await PostNewTaskAsync(TaskForm.New("Long description", new string('d', 500)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(await StoredTasksAsync());
    }

    [Fact]
    public async Task Create_DescriptionOver500Characters_IsRejected()
    {
        var response = await PostNewTaskAsync(TaskForm.New("Too long description", new string('d', 501)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Description cannot exceed 500 characters.", await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(await StoredTasksAsync());
    }

    [Theory]
    [InlineData("Done", "Medium", "Please select a valid status.")]
    [InlineData("To Do", "Urgent", "Please select a valid priority.")]
    public async Task Create_TamperedStatusOrPriority_IsRejected(string status, string priority, string expectedMessage)
    {
        // A browser can only send the dropdown values; this simulates a hand-crafted request.
        var response = await PostNewTaskAsync(TaskForm.New("Tampered task", priority: priority, status: status));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedMessage, await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(await StoredTasksAsync());
    }

    [Fact]
    public async Task Create_WithoutAntiforgeryToken_IsRejected()
    {
        // Proves the form is CSRF-protected: posting without the hidden token fails.
        var response = await Client.PostAsync(
            "/Tasks", new FormUrlEncodedContent(TaskForm.New("No token task")), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await StoredTasksAsync());
    }

    // ---- Trimming (Brief §1.2): whitespace is removed BEFORE length validation and storage ----

    [Fact]
    public async Task Create_TitleWithSurroundingWhitespace_IsStoredTrimmed()
    {
        await PostNewTaskAsync(TaskForm.New("   Trimmed title   "));

        Assert.Equal("Trimmed title", Assert.Single(await StoredTasksAsync()).Title);
    }

    [Fact]
    public async Task Create_OneCharacterSurroundedByWhitespace_IsAcceptedAsOneCharacter()
    {
        var response = await PostNewTaskAsync(TaskForm.New("  A  "));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("A", Assert.Single(await StoredTasksAsync()).Title);
    }

    [Fact]
    public async Task Create_100CharactersPlusSurroundingWhitespace_IsAccepted()
    {
        // 104 characters as typed, but only 100 after trimming, so it is valid.
        var title = new string('a', 100);

        var response = await PostNewTaskAsync(TaskForm.New($"  {title}  "));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(title, Assert.Single(await StoredTasksAsync()).Title);
    }
}
