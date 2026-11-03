namespace TaskFlow.Models;

/// <summary>
/// Importance of a task. The underlying integers are ordered from least to most
/// urgent, which lets "sort by priority" translate into efficient SQL.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}
