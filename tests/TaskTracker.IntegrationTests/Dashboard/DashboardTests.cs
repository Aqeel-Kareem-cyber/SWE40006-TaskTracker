using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Data;
using TaskTracker.IntegrationTests.Infrastructure;
using TaskTracker.Models;
using DashboardModel = TaskTracker.Pages.IndexModel;

namespace TaskTracker.IntegrationTests.Dashboard;

/// <summary>
/// Dashboard (/): statistics are checked on the PageModel itself against the real SQLite
/// database (exact numbers), and the Development-only role preview is checked over HTTP.
/// The three roles are UI previews only, not authentication; these tests do not treat them as security.
/// </summary>
public class DashboardTests(TaskTrackerFactory factory) : IntegrationTestBase(factory)
{
    private static DateTime Today => DateTime.Today;

    private async Task<(DashboardModel Model, DefaultHttpContext Http)> LoadDashboardAsync(
        string environment = "Development", string? roleCookie = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var http = new DefaultHttpContext();
        if (roleCookie is not null)
        {
            http.Request.Headers.Cookie = $"TaskTrackerPreviewRole={roleCookie}";
        }

        var model = new DashboardModel(db, new TestHostEnvironment(environment))
        {
            PageContext = new PageContext { HttpContext = http }
        };

        await model.OnGetAsync();
        return (model, http);
    }

    // ---------- Statistics ----------

    [Fact]
    public async Task Statistics_EmptyDatabase_AreAllZero()
    {
        var (model, _) = await LoadDashboardAsync();

        Assert.Equal(0, model.TotalTasks);
        Assert.Equal(0, model.CompletionPercentage); // no divide-by-zero
        Assert.Empty(model.AttentionTasks);
    }

    [Fact]
    public async Task Statistics_KnownData_AreCalculatedCorrectly()
    {
        await Factory.SeedAsync(
            new WorkTask { Title = "Overdue high", Status = "To Do", Priority = "High", DueDate = Today.AddDays(-1) },
            new WorkTask { Title = "In progress low", Status = "In Progress", Priority = "Low", DueDate = Today.AddDays(1) },
            new WorkTask { Title = "Done high, past date", Status = "Completed", Priority = "High", DueDate = Today.AddDays(-7) },
            new WorkTask { Title = "No due date", Status = "To Do", Priority = "Medium" });

        var (model, _) = await LoadDashboardAsync();

        Assert.Equal(4, model.TotalTasks);
        Assert.Equal(2, model.ToDoTasks);
        Assert.Equal(1, model.InProgressTasks);
        Assert.Equal(1, model.CompletedTasks);
        Assert.Equal(1, model.OverdueTasks);       // completed tasks are never overdue
        Assert.Equal(1, model.HighPriorityTasks);  // completed high-priority work doesn't count
        Assert.Equal(25, model.CompletionPercentage);
        Assert.Equal("Overdue high", Assert.Single(model.AttentionTasks).Title);
    }

    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(3, 3, 100)]
    public async Task CompletionPercentage_IsRoundedToWholeNumber(int completed, int total, int expected)
    {
        var tasks = Enumerable.Range(1, total)
            .Select(i => new WorkTask { Title = $"Task {i}", Status = i <= completed ? "Completed" : "To Do" })
            .ToArray();
        await Factory.SeedAsync(tasks);

        var (model, _) = await LoadDashboardAsync();

        Assert.Equal(expected, model.CompletionPercentage);
    }

    [Fact]
    public async Task AttentionTasks_AreLimitedToFive()
    {
        var tasks = Enumerable.Range(1, 7)
            .Select(i => new WorkTask { Title = $"Urgent {i}", Priority = "High", DueDate = Today.AddDays(i) })
            .ToArray();
        await Factory.SeedAsync(tasks);

        var (model, _) = await LoadDashboardAsync();

        Assert.Equal(5, model.AttentionTasks.Count);
        Assert.Equal("Urgent 1", model.AttentionTasks[0].Title); // soonest due date first
    }

    // ---------- Role preview (Development only) ----------

    [Theory]
    [InlineData(null, "Member", "Team Member")]
    [InlineData("Manager", "Manager", "Department Manager")]
    [InlineData("Owner", "Owner", "Workspace Owner")]
    [InlineData("Admin", "Member", "Team Member")] // unknown cookie value falls back safely
    public async Task RolePreview_InDevelopment_FollowsCookie(string? cookie, string expectedView, string expectedLabel)
    {
        var (model, _) = await LoadDashboardAsync("Development", cookie);

        Assert.Equal(expectedView, model.SelectedView);
        Assert.Equal(expectedLabel, model.RoleLabel);
    }

    [Fact]
    public async Task RolePreview_InProduction_IgnoresCookie()
    {
        var (model, _) = await LoadDashboardAsync("Production", "Owner");

        Assert.Equal("Member", model.SelectedView);
        Assert.True(model.IsMember);
    }

    [Fact]
    public async Task SwitchView_InProduction_Returns404()
    {
        var (model, _) = await LoadDashboardAsync("Production");

        Assert.IsType<NotFoundResult>(model.OnPostSwitchView("Owner"));
    }

    [Fact]
    public async Task SwitchView_InvalidRole_Returns400()
    {
        var (model, _) = await LoadDashboardAsync();

        Assert.IsType<BadRequestResult>(model.OnPostSwitchView("Admin"));
    }

    [Fact]
    public async Task SwitchView_ValidRole_SetsPreviewCookieAndRedirects()
    {
        var (model, http) = await LoadDashboardAsync();

        var result = model.OnPostSwitchView("Owner");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Contains("TaskTrackerPreviewRole=Owner", http.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task SwitchView_OverHttp_ChangesRenderedDashboard()
    {
        await Factory.SeedAsync(new WorkTask { Title = "Needs attention", Priority = "High" });

        Assert.Contains("My Dashboard", await Client.GetStringAsync("/", Ct)); // default: Team Member

        var response = await FormHelper.PostFormAsync(
            Client, "/", "/?handler=SwitchView", new Dictionary<string, string> { ["selectedView"] = "Manager" }, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var html = await Client.GetStringAsync("/", Ct); // cookie now sent automatically
        Assert.Contains("Team Dashboard", html);
        Assert.Contains("Priority Tasks", html);
        Assert.Contains("Needs attention", html);
    }
}
