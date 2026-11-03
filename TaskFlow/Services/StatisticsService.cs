using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.DTOs.Statistics;
using TaskFlow.Exceptions;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Services;

/// <inheritdoc cref="IStatisticsService"/>
public class StatisticsService(TaskFlowDbContext dbContext, TimeProvider timeProvider) : IStatisticsService
{
    public async Task<StatisticsResponse> GetOverallStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        return await CalculateAsync(dbContext.Tasks.AsNoTracking(), cancellationToken);
    }

    public async Task<ProjectStatisticsResponse> GetProjectStatisticsAsync(
        int projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw NotFoundException.ForProject(projectId);

        var scoped = dbContext.Tasks.AsNoTracking().Where(task => task.ProjectId == projectId);
        var statistics = await CalculateAsync(scoped, cancellationToken);

        return new ProjectStatisticsResponse
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            TotalTasks = statistics.TotalTasks,
            CompletedTasks = statistics.CompletedTasks,
            PendingTasks = statistics.PendingTasks,
            InProgressTasks = statistics.InProgressTasks,
            OverdueTasks = statistics.OverdueTasks,
            CompletionPercentage = statistics.CompletionPercentage,
            TasksByPriority = statistics.TasksByPriority,
            TasksByStatus = statistics.TasksByStatus
        };
    }

    /// <summary>
    /// Aggregates a task query into productivity figures. The counting is done by the
    /// database (three GROUP BY/COUNT queries) rather than by loading tasks into memory.
    /// </summary>
    private async Task<StatisticsResponse> CalculateAsync(
        IQueryable<TaskItem> tasks, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var statusCounts = await tasks
            .GroupBy(task => task.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);

        var priorityCounts = await tasks
            .GroupBy(task => task.Priority)
            .Select(group => new { Priority = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Priority, row => row.Count, cancellationToken);

        var overdueTasks = await tasks.CountAsync(
            task => task.Status != TaskItemStatus.Completed
                    && task.DueDate != null
                    && task.DueDate < utcNow,
            cancellationToken);

        var totalTasks = statusCounts.Values.Sum();
        var completedTasks = statusCounts.GetValueOrDefault(TaskItemStatus.Completed);

        return new StatisticsResponse
        {
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            // Pending is its own status, so completed tasks are inherently excluded.
            PendingTasks = statusCounts.GetValueOrDefault(TaskItemStatus.Pending),
            InProgressTasks = statusCounts.GetValueOrDefault(TaskItemStatus.InProgress),
            OverdueTasks = overdueTasks,
            CompletionPercentage = totalTasks == 0
                ? 0
                : Math.Round(completedTasks / (double)totalTasks * 100, 2),
            // Every enum value is listed, including zeroes, so the shape is stable for clients.
            TasksByPriority = Enum.GetValues<TaskPriority>()
                .ToDictionary(priority => priority.ToString(), priorityCounts.GetValueOrDefault),
            TasksByStatus = Enum.GetValues<TaskItemStatus>()
                .ToDictionary(status => status.ToString(), statusCounts.GetValueOrDefault)
        };
    }
}
