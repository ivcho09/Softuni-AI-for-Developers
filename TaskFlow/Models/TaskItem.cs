namespace TaskFlow.Models;

/// <summary>
/// A single personal task. Optionally belongs to a <see cref="Models.Project"/>.
/// </summary>
public class TaskItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Assigned by the service layer when the task is created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional deadline (UTC). A task without a deadline can never be overdue.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Set when the task is completed and cleared when it is reopened (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Owning project, or <c>null</c> for an unassigned task.</summary>
    public int? ProjectId { get; set; }

    public Project? Project { get; set; }
}
