using TaskFlow.Models;

namespace TaskFlow.DTOs.Tasks;

/// <summary>Fields that the task list can be sorted by.</summary>
public enum TaskSortField
{
    CreatedAt = 0,
    DueDate = 1,
    Priority = 2,
    Title = 3
}

/// <summary>
/// Filtering, searching, sorting and pagination options for <c>GET /api/tasks</c>.
/// All properties are optional.
/// </summary>
public class TaskQueryParameters
{
    /// <summary>Return only tasks in this status.</summary>
    /// <example>Pending</example>
    public TaskItemStatus? Status { get; set; }

    /// <summary>Return only tasks with this priority.</summary>
    /// <example>High</example>
    public TaskPriority? Priority { get; set; }

    /// <summary>Return only tasks belonging to this project.</summary>
    /// <example>1</example>
    public int? ProjectId { get; set; }

    /// <summary>When true, return only overdue tasks; when false, only tasks that are not overdue.</summary>
    /// <example>true</example>
    public bool? Overdue { get; set; }

    /// <summary>Case-insensitive substring match on the task title.</summary>
    /// <example>homework</example>
    public string? Search { get; set; }

    /// <summary>Sort field. Defaults to <c>CreatedAt</c>.</summary>
    /// <example>dueDate</example>
    public TaskSortField SortBy { get; set; } = TaskSortField.CreatedAt;

    /// <summary>
    /// Sort direction. Defaults to ascending, except for <c>Priority</c> which defaults to
    /// descending so that the most urgent tasks come first.
    /// </summary>
    public bool? Descending { get; set; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    /// <example>1</example>
    public int Page { get; set; } = 1;

    /// <summary>Records per page, between 1 and 100. Defaults to 20.</summary>
    /// <example>20</example>
    public int PageSize { get; set; } = 20;

    /// <summary>Effective sort direction after applying the per-field default.</summary>
    public bool SortDescending => Descending ?? SortBy == TaskSortField.Priority;
}
