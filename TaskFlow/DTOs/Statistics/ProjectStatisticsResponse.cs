namespace TaskFlow.DTOs.Statistics;

/// <summary>Productivity figures scoped to a single project.</summary>
public record ProjectStatisticsResponse : StatisticsResponse
{
    /// <example>1</example>
    public int ProjectId { get; init; }

    /// <example>University</example>
    public string ProjectName { get; init; } = string.Empty;
}
