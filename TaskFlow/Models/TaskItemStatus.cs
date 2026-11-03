namespace TaskFlow.Models;

/// <summary>
/// Lifecycle state of a task. Persisted as an integer so that database-side
/// ordering follows the natural progression of the values.
/// </summary>
public enum TaskItemStatus
{
    /// <summary>Created but not started yet.</summary>
    Pending = 0,

    /// <summary>Actively being worked on.</summary>
    InProgress = 1,

    /// <summary>Finished. Carries a <see cref="TaskItem.CompletedAt"/> timestamp.</summary>
    Completed = 2
}
