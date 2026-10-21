using Microsoft.Playwright;
using TaskTracker.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace TaskTracker.E2ETests;

/// <summary>
/// End-to-end acceptance tests for the four required workflows (Brief §1.2 / §1.4):
/// create, view, complete and delete - plus edit and title validation - driven through
/// a real Chromium browser against a running TaskTracker (E2E_BASE_URL).
/// </summary>
[Trait("Category", "E2E")]
public class TaskWorkflowTests(BrowserFixture browser) : E2ETestBase(browser)
{
    [E2EFact]
    public async Task Create_ValidTask_AppearsAsToDoAndSurvivesReload()
    {
        var title = UniqueTitle("create");

        await CreateTaskAsync(title);
        await Page.ReloadAsync();

        await Expect(TaskRow(title)).ToBeVisibleAsync();
        await Expect(TaskRow(title)).ToContainTextAsync("To Do");
    }

    [E2EFact]
    public async Task View_CreatedTask_IsShownOnTaskListAndDashboardBoard()
    {
        var title = UniqueTitle("view");
        await CreateTaskAsync(title);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Task Management" })).ToBeVisibleAsync();

        await Page.GotoAsync("/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = title })).ToBeVisibleAsync(); // status board card
    }

    [E2EFact]
    public async Task Complete_Task_ShowsCompletedAndPersistsAfterReload()
    {
        var title = UniqueTitle("complete");
        await CreateTaskAsync(title);

        await TaskRow(title).GetByRole(AriaRole.Button, new() { Name = "Complete" }).ClickAsync();
        await Page.ReloadAsync();

        await Expect(TaskRow(title)).ToBeVisibleAsync();                 // completed, not removed
        await Expect(TaskRow(title)).ToContainTextAsync("Completed");
        await Expect(TaskRow(title).GetByRole(AriaRole.Button, new() { Name = "Complete" })).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Delete_Task_IsRemovedAndStaysRemovedAfterReload()
    {
        var keep = UniqueTitle("keep");
        var remove = UniqueTitle("delete");
        await CreateTaskAsync(keep);
        await CreateTaskAsync(remove);

        // Clicking Delete opens the browser's confirm() dialog; the base class accepts it.
        await TaskRow(remove).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Page.ReloadAsync();

        await Expect(TaskRow(remove)).ToHaveCountAsync(0);
        await Expect(TaskRow(keep)).ToBeVisibleAsync(); // other records untouched
    }

    [E2EFact]
    public async Task Edit_Task_SavesNewTitleAndStatus()
    {
        var original = UniqueTitle("edit");
        var renamed = UniqueTitle("edited");
        await CreateTaskAsync(original);

        await TaskRow(original).GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await Page.GetByLabel("Task Name").FillAsync(renamed);
        await Page.GetByLabel("Status", new() { Exact = true }).SelectOptionAsync("In Progress");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();

        await Expect(TaskRow(renamed)).ToBeVisibleAsync();
        await Expect(TaskRow(renamed)).ToContainTextAsync("In Progress");
        await Expect(TaskRow(original)).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Create_TitleOf100Characters_IsAccepted()
    {
        var prefix = UniqueTitle("max") + "-";
        var title = prefix + new string('a', 100 - prefix.Length);

        await CreateTaskAsync(title);

        await Expect(TaskRow(title)).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Create_EmptyTitle_ShowsValidationMessageAndCreatesNothing()
    {
        await GoToTasksAsync();
        var rowsBefore = await Page.Locator("tbody tr").CountAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Task" }).ClickAsync();

        await Expect(Page.GetByText("Task title is required.").First).ToBeVisibleAsync();
        await GoToTasksAsync(); // fresh GET (a reload could re-submit the form)
        Assert.Equal(rowsBefore, await Page.Locator("tbody tr").CountAsync());
    }

    [E2EFact]
    public async Task Create_TitleInput_StopsAt100Characters()
    {
        // The form copies the model's StringLength(100) into maxlength="100", so a user cannot
        // even type a 101st character. (A hand-crafted 101-character POST is rejected by the
        // server - covered by TaskCreateTests in the integration suite.)
        var prefix = UniqueTitle("toolong") + "-";
        var typed = prefix + new string('a', 101 - prefix.Length);

        await GoToTasksAsync();
        var titleInput = Page.GetByLabel("Task Name");
        await titleInput.FillAsync(typed);

        Assert.Equal(101, typed.Length);
        Assert.Equal(100, (await titleInput.InputValueAsync()).Length);
        await Expect(titleInput).ToHaveAttributeAsync("maxlength", "100");
    }
}
