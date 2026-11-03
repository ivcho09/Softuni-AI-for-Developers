namespace TaskFlow.DTOs.Projects;

/// <summary>Payload for creating a project. <c>CreatedAt</c> is assigned by the server.</summary>
public class CreateProjectRequest
{
    /// <summary>Project name. Required, at most 100 characters.</summary>
    /// <example>University</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional detail, at most 1000 characters.</summary>
    /// <example>Everything related to coursework and exams.</example>
    public string? Description { get; set; }
}
