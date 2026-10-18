using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Pages.Tasks;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public WorkTask TaskItem { get; set; } = new();

    // Load the existing task
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var task = await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            return NotFound();
        }

        TaskItem = task;

        return Page();
    }

    // Save changes to the task
    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate the selected status and priority
        string[] validStatuses =
        {
            "To Do",
            "In Progress",
            "Completed"
        };

        string[] validPriorities =
        {
            "Low",
            "Medium",
            "High"
        };

        if (!validStatuses.Contains(TaskItem.Status))
        {
            ModelState.AddModelError(
                "TaskItem.Status",
                "Please select a valid status."
            );
        }

        if (!validPriorities.Contains(TaskItem.Priority))
        {
            ModelState.AddModelError(
                "TaskItem.Priority",
                "Please select a valid priority."
            );
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existingTask = await _context.Tasks.FindAsync(id);

        if (existingTask == null)
        {
            return NotFound();
        }

        // Update only the editable fields
        existingTask.Title = TaskItem.Title.Trim();
        existingTask.Description = TaskItem.Description;
        existingTask.Status = TaskItem.Status;
        existingTask.Priority = TaskItem.Priority;
        existingTask.DueDate = TaskItem.DueDate;

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}