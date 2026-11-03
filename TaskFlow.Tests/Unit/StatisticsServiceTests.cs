using TaskFlow.Exceptions;
using TaskFlow.Models;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

/// <summary>Productivity statistics, including the counting and percentage rules.</summary>
public class StatisticsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new();

    private static readonly DateTime Now = FixedTimeProvider.DefaultNow;

    private StatisticsService CreateService() => new(_database.CreateContext(), _clock);

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync()
    {
        await using var context = _database.CreateContext();

        context.Projects.AddRange(
            new Project { Id = 1, Name = "University", CreatedAt = Now },
            new Project { Id = 2, Name = "Home", CreatedAt = Now });

        context.Tasks.AddRange(
            // Project 1: 2 pending (1 overdue), 1 in progress (overdue), 1 completed (past due but done)
            new TaskItem { Title = "Maths", Status = TaskItemStatus.Pending, Priority = TaskPriority.High, CreatedAt = Now, DueDate = Now.AddDays(-5), ProjectId = 1 },
            new TaskItem { Title = "Essay", Status = TaskItemStatus.Pending, Priority = TaskPriority.High, CreatedAt = Now, DueDate = Now.AddDays(5), ProjectId = 1 },
            new TaskItem { Title = "History", Status = TaskItemStatus.InProgress, Priority = TaskPriority.Urgent, CreatedAt = Now, DueDate = Now.AddDays(-1), ProjectId = 1 },
            new TaskItem { Title = "Chapter 4", Status = TaskItemStatus.Completed, Priority = TaskPriority.Low, CreatedAt = Now, DueDate = Now.AddDays(-9), CompletedAt = Now, ProjectId = 1 },
            // Project 2: 1 completed, 1 pending with no deadline
            new TaskItem { Title = "Sink", Status = TaskItemStatus.Completed, Priority = TaskPriority.Medium, CreatedAt = Now, CompletedAt = Now, ProjectId = 2 },
            new TaskItem { Title = "Milk", Status = TaskItemStatus.Pending, Priority = TaskPriority.Low, CreatedAt = Now, ProjectId = 2 });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task OverallStatistics_CountEachCategoryCorrectly()
    {
        await SeedAsync();

        var statistics = await CreateService().GetOverallStatisticsAsync();

        Assert.Equal(6, statistics.TotalTasks);
        Assert.Equal(2, statistics.CompletedTasks);
        Assert.Equal(3, statistics.PendingTasks);
        Assert.Equal(1, statistics.InProgressTasks);
        // Overdue: "Maths" and "History". "Chapter 4" is past its deadline but completed.
        Assert.Equal(2, statistics.OverdueTasks);
    }

    [Fact]
    public async Task PendingCount_NeverIncludesCompletedTasks()
    {
        await SeedAsync();

        var statistics = await CreateService().GetOverallStatisticsAsync();

        Assert.Equal(statistics.TotalTasks,
            statistics.PendingTasks + statistics.InProgressTasks + statistics.CompletedTasks);
    }

    [Fact]
    public async Task CompletionPercentage_IsCompletedOverTotal()
    {
        await SeedAsync();

        var statistics = await CreateService().GetOverallStatisticsAsync();

        Assert.Equal(33.33, statistics.CompletionPercentage); // 2 / 6 * 100, rounded
    }

    [Fact]
    public async Task CompletionPercentage_IsZeroWhenThereAreNoTasks()
    {
        var statistics = await CreateService().GetOverallStatisticsAsync();

        Assert.Equal(0, statistics.TotalTasks);
        Assert.Equal(0, statistics.CompletionPercentage);
    }

    [Fact]
    public async Task Breakdowns_ListEveryEnumValueIncludingZeroes()
    {
        await SeedAsync();

        var statistics = await CreateService().GetOverallStatisticsAsync();

        Assert.Equal(["Low", "Medium", "High", "Urgent"], statistics.TasksByPriority.Keys);
        Assert.Equal(["Pending", "InProgress", "Completed"], statistics.TasksByStatus.Keys);

        Assert.Equal(2, statistics.TasksByPriority["High"]);
        Assert.Equal(1, statistics.TasksByPriority["Urgent"]);
        Assert.Equal(statistics.TotalTasks, statistics.TasksByPriority.Values.Sum());
        Assert.Equal(statistics.TotalTasks, statistics.TasksByStatus.Values.Sum());
    }

    [Fact]
    public async Task ProjectStatistics_AreScopedToThatProject()
    {
        await SeedAsync();

        var statistics = await CreateService().GetProjectStatisticsAsync(1);

        Assert.Equal(1, statistics.ProjectId);
        Assert.Equal("University", statistics.ProjectName);
        Assert.Equal(4, statistics.TotalTasks);
        Assert.Equal(1, statistics.CompletedTasks);
        Assert.Equal(2, statistics.PendingTasks);
        Assert.Equal(1, statistics.InProgressTasks);
        Assert.Equal(2, statistics.OverdueTasks);
        Assert.Equal(25, statistics.CompletionPercentage);
    }

    [Fact]
    public async Task ProjectStatistics_ForAMissingProject_ThrowNotFound()
    {
        await SeedAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetProjectStatisticsAsync(999));
    }

    [Fact]
    public async Task OverdueCount_FollowsTheClock()
    {
        await using (var context = _database.CreateContext())
        {
            context.Tasks.Add(new TaskItem
            {
                Title = "Due tomorrow",
                Status = TaskItemStatus.Pending,
                CreatedAt = Now,
                DueDate = Now.AddDays(1)
            });
            await context.SaveChangesAsync();
        }

        Assert.Equal(0, (await CreateService().GetOverallStatisticsAsync()).OverdueTasks);

        // Move the clock past the deadline; the same row is now overdue.
        _clock.UtcNow = Now.AddDays(2);
        Assert.Equal(1, (await CreateService().GetOverallStatisticsAsync()).OverdueTasks);
    }
}
