using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public List<WorkTask> Tasks { get; set; } = new();

    // Dashboard preview role
    public string SelectedView { get; set; } = "Member";

    public string RoleLabel => SelectedView switch
    {
        "Manager" => "Department Manager",
        "Owner" => "Workspace Owner",
        _ => "Team Member"
    };

    public bool IsMember => SelectedView == "Member";
    public bool IsManager => SelectedView == "Manager";
    public bool IsOwner => SelectedView == "Owner";

    // Task statistics
    public int TotalTasks => Tasks.Count;

    public int CompletedTasks =>
        Tasks.Count(t => t.Status == "Completed");

    public int InProgressTasks =>
        Tasks.Count(t => t.Status == "In Progress");

    public int ToDoTasks =>
        Tasks.Count(t => t.Status == "To Do");

    public int OverdueTasks =>
        Tasks.Count(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value.Date < DateTime.Today &&
            t.Status != "Completed");

    public int HighPriorityTasks =>
        Tasks.Count(t =>
            t.Priority == "High" &&
            t.Status != "Completed");

    public int CompletionPercentage =>
        TotalTasks == 0
            ? 0
            : (int)Math.Round(
                (double)CompletedTasks / TotalTasks * 100);

    public List<WorkTask> AttentionTasks =>
        Tasks
            .Where(t =>
                t.Status != "Completed" &&
                (
                    t.Priority == "High" ||
                    (
                        t.DueDate.HasValue &&
                        t.DueDate.Value.Date < DateTime.Today
                    )
                ))
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToList();

    public async Task OnGetAsync()
    {
        if (_environment.IsDevelopment())
        {
            var savedView =
                Request.Cookies["TaskTrackerPreviewRole"];

            if (savedView is "Member" or "Manager" or "Owner")
            {
                SelectedView = savedView;
            }
        }

        Tasks = await _context.Tasks
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // Change dashboard preview
    public IActionResult OnPostSwitchView(string selectedView)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (selectedView is not ("Member" or "Manager" or "Owner"))
        {
            return BadRequest();
        }

        Response.Cookies.Append(
            "TaskTrackerPreviewRole",
            selectedView,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = TimeSpan.FromDays(1)
            }
        );

        return RedirectToPage("/Index");
    }
}