using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Pages.Tasks;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<WorkTask> Tasks { get; set; }
        = new List<WorkTask>();

    [BindProperty]
    public WorkTask NewTask { get; set; } = new();

    // Retrieve all tasks
    public async Task OnGetAsync()
    {
        Tasks = await _context.Tasks
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // Create a task
    public async Task<IActionResult> OnPostAsync()
    {
        NewTask.Title = (NewTask.Title ?? string.Empty).Trim();

        // Revalidate the title after trimming whitespace
        ModelState.Remove("NewTask.Title");
        TryValidateModel(NewTask, nameof(NewTask));

        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        _context.Tasks.Add(NewTask);

        await _context.SaveChangesAsync();

        return RedirectToPage();
    }

    // Complete a task
    public async Task<IActionResult> OnPostCompleteAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        task.Status = "In Progress"; // SEEDED FAULT

        await _context.SaveChangesAsync();

        return RedirectToPage();
    }

    // Delete a task
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);

        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}