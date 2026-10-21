namespace TaskTracker.IntegrationTests.Infrastructure;

/// <summary>
/// Builds form fields with the exact names Razor Pages generates for the task forms:
/// "NewTask.*" on /Tasks (create) and "TaskItem.*" on /Tasks/Edit/{id}.
/// </summary>
public static class TaskForm
{
    public static Dictionary<string, string> New(
        string title,
        string? description = null,
        string? priority = null,
        string? status = null,
        string? dueDate = null)
        => Build("NewTask", title, description, priority, status, dueDate);

    public static Dictionary<string, string> Edit(
        string title,
        string? description = null,
        string priority = "Medium",
        string status = "To Do",
        string? dueDate = null)
        => Build("TaskItem", title, description, priority, status, dueDate);

    private static Dictionary<string, string> Build(
        string prefix, string title, string? description, string? priority, string? status, string? dueDate)
    {
        var fields = new Dictionary<string, string> { [$"{prefix}.Title"] = title };

        // Fields left null are simply not posted, so the model's own defaults apply.
        if (description is not null) fields[$"{prefix}.Description"] = description;
        if (priority is not null) fields[$"{prefix}.Priority"] = priority;
        if (status is not null) fields[$"{prefix}.Status"] = status;
        if (dueDate is not null) fields[$"{prefix}.DueDate"] = dueDate; // yyyy-MM-dd, like <input type="date">

        return fields;
    }
}
