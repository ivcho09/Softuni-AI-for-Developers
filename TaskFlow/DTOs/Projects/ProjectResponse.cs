using TaskFlow.Models;

namespace TaskFlow.DTOs.Projects;

/// <summary>A project as returned by the API, including a count of the tasks it holds.</summary>
public record ProjectResponse
{
    /// <example>1</example>
    public int Id { get; init; }

    /// <example>University</example>
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <example>2026-11-03T10:00:00Z</example>
    public DateTime CreatedAt { get; init; }

    /// <summary>Number of tasks currently assigned to this project.</summary>
    /// <example>5</example>
    public int TaskCount { get; init; }

    public static ProjectResponse FromEntity(Project project, int taskCount) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        CreatedAt = project.CreatedAt,
        TaskCount = taskCount
    };
}
