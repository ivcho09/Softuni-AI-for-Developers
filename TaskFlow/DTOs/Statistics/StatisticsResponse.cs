namespace TaskFlow.DTOs.Statistics;

/// <summary>Aggregated productivity figures calculated from the stored tasks.</summary>
public record StatisticsResponse
{
    /// <example>42</example>
    public int TotalTasks { get; init; }

    /// <example>18</example>
    public int CompletedTasks { get; init; }

    /// <summary>Tasks still in <c>Pending</c>. Completed tasks are never counted here.</summary>
    /// <example>20</example>
    public int PendingTasks { get; init; }

    /// <example>4</example>
    public int InProgressTasks { get; init; }

    /// <summary>Tasks whose deadline has passed and that are not completed.</summary>
    /// <example>3</example>
    public int OverdueTasks { get; init; }

    /// <summary>CompletedTasks / TotalTasks * 100, rounded to two decimals. Zero when there are no tasks.</summary>
    /// <example>42.86</example>
    public double CompletionPercentage { get; init; }

    /// <summary>Task counts per priority. Always contains every priority, including zeroes.</summary>
    public IDictionary<string, int> TasksByPriority { get; init; } = new Dictionary<string, int>();

    /// <summary>Task counts per status. Always contains every status, including zeroes.</summary>
    public IDictionary<string, int> TasksByStatus { get; init; } = new Dictionary<string, int>();
}
