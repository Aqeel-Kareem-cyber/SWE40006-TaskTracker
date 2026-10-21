using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Models;

public class WorkTask
{
    public int Id { get; set; }

    // SEEDED FAULT: [Required] removed
    [StringLength(100, MinimumLength = 0,
        ErrorMessage = "Title must be between 1 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(500,
        ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(To Do|In Progress|Completed)$",
        ErrorMessage = "Please select a valid status.")]
    public string Status { get; set; } = "To Do";

    [Required]
    [RegularExpression("^(Low|Medium|High)$",
        ErrorMessage = "Please select a valid priority.")]
    public string Priority { get; set; } = "Medium";

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}