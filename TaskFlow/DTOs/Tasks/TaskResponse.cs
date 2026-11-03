using TaskFlow.Models;

namespace TaskFlow.DTOs.Tasks;

/// <summary>
/// A task as returned by the API.
/// </summary>
public record TaskResponse
{
    /// <example>1</example>
    public int Id { get; init; }

    /// <example>Finish the database assignment</example>
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <example>Pending</example>
    public TaskItemStatus Status { get; init; }

    /// <example>High</example>
    public TaskPriority Priority { get; init; }

    /// <example>2026-11-03T10:00:00Z</example>
    public DateTime CreatedAt { get; init; }

    /// <example>2026-12-01T09:00:00Z</example>
    public DateTime? DueDate { get; init; }

    public DateTime? CompletedAt { get; init; }

    /// <example>1</example>
    public int? ProjectId { get; init; }

    /// <summary>Name of the owning project, when the task belongs to one.</summary>
    /// <example>University</example>
    public string? ProjectName { get; init; }

    /// <summary>True when the task has a deadline in the past and is not completed.</summary>
    /// <example>false</example>
    public bool IsOverdue { get; init; }

    /// <summary>
    /// Projects an entity onto its API representation. <paramref name="utcNow"/> is passed in
    /// rather than read from the clock so that the result is deterministic and testable.
    /// </summary>
    public static TaskResponse FromEntity(TaskItem task, DateTime utcNow) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status,
        Priority = task.Priority,
        CreatedAt = task.CreatedAt,
        DueDate = task.DueDate,
        CompletedAt = task.CompletedAt,
        ProjectId = task.ProjectId,
        ProjectName = task.Project?.Name,
        IsOverdue = task.Status != TaskItemStatus.Completed
                    && task.DueDate.HasValue
                    && task.DueDate.Value < utcNow
    };
}
