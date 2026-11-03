using TaskFlow.Models;

namespace TaskFlow.DTOs.Tasks;

/// <summary>
/// Full replacement payload for a task. Every field is applied, so omitted optional
/// fields are cleared. <c>CompletedAt</c> is maintained by the server from <c>Status</c>.
/// </summary>
public class UpdateTaskRequest
{
    /// <summary>Short summary of the task. Required, at most 100 characters.</summary>
    /// <example>Finish the database assignment</example>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional detail, at most 1000 characters.</summary>
    public string? Description { get; set; }

    /// <summary>New status. Moving to <c>Completed</c> stamps the completion time; moving away clears it.</summary>
    /// <example>InProgress</example>
    public TaskItemStatus Status { get; set; }

    /// <example>Urgent</example>
    public TaskPriority Priority { get; set; }

    /// <summary>Optional deadline in UTC.</summary>
    /// <example>2026-12-05T17:00:00Z</example>
    public DateTime? DueDate { get; set; }

    /// <summary>Optional owning project, or null to detach the task from its project.</summary>
    /// <example>1</example>
    public int? ProjectId { get; set; }
}
