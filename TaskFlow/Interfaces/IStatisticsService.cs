using TaskFlow.DTOs.Statistics;

namespace TaskFlow.Interfaces;

/// <summary>Productivity reporting calculated from stored tasks.</summary>
public interface IStatisticsService
{
    Task<StatisticsResponse> GetOverallStatisticsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The project does not exist.</exception>
    Task<ProjectStatisticsResponse> GetProjectStatisticsAsync(int projectId, CancellationToken cancellationToken = default);
}
