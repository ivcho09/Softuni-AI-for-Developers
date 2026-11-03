using TaskFlow.Models;

namespace TaskFlow.DTOs.Tasks;

/// <summary>
/// Payload for creating a task. <c>CreatedAt</c> is assigned by the server and cannot be supplied.
/// </summary>
public class CreateTaskRequest
{
    /// <summary>Short summary of the task. Required, at most 100 characters.</summary>
    /// <example>Finish the database assignment</example>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional detail, at most 1000 characters.</summary>
    /// <example>Normalise the schema and hand in the ER diagram.</example>
    public string? Description { get; set; }

    /// <summary>Initial status. Defaults to <c>Pending</c>.</summary>
    /// <example>Pending</example>
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    /// <summary>Importance. Defaults to <c>Medium</c>.</summary>
    /// <example>High</example>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Optional deadline in UTC. A task without a deadline is never overdue.</summary>
    /// <example>2026-12-01T09:00:00Z</example>
    public DateTime? DueDate { get; set; }

    /// <summary>Optional owning project. Must reference an existing project when supplied.</summary>
    /// <example>1</example>
    public int? ProjectId { get; set; }
}
