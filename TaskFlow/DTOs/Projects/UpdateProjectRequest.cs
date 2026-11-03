namespace TaskFlow.DTOs.Projects;

/// <summary>Full replacement payload for a project.</summary>
public class UpdateProjectRequest
{
    /// <summary>Project name. Required, at most 100 characters.</summary>
    /// <example>University 2026</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional detail, at most 1000 characters.</summary>
    public string? Description { get; set; }
}
