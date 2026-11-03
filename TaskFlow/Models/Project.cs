namespace TaskFlow.Models;

/// <summary>
/// A container used to group related tasks.
/// </summary>
public class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Assigned by the service layer when the project is created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Tasks currently assigned to this project. Deleting the project detaches
    /// them (sets <see cref="TaskItem.ProjectId"/> to null) instead of deleting them.
    /// </summary>
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
