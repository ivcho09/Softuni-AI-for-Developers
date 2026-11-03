using TaskFlow.DTOs.Tasks;
using TaskFlow.Models;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

/// <summary>Filtering, searching, sorting and pagination of the task list.</summary>
public class TaskFilteringTests : IAsyncLifetime, IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new();

    private static readonly DateTime Now = FixedTimeProvider.DefaultNow;

    private TaskService CreateService() => new(_database.CreateContext(), _clock);

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// Seven tasks across two projects, deliberately mixing overdue, completed and
    /// deadline-free tasks so that each filter has something to include and exclude.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var context = _database.CreateContext();

        context.Projects.AddRange(
            new Project { Id = 1, Name = "University", CreatedAt = Now },
            new Project { Id = 2, Name = "Home", CreatedAt = Now });

        context.Tasks.AddRange(
            new TaskItem { Title = "Maths homework", Status = TaskItemStatus.Pending, Priority = TaskPriority.High, CreatedAt = Now.AddDays(-6), DueDate = Now.AddDays(-30), ProjectId = 1 },
            new TaskItem { Title = "History homework", Status = TaskItemStatus.InProgress, Priority = TaskPriority.Urgent, CreatedAt = Now.AddDays(-5), DueDate = Now.AddDays(-10), ProjectId = 1 },
            new TaskItem { Title = "Read chapter 4", Status = TaskItemStatus.Completed, Priority = TaskPriority.Low, CreatedAt = Now.AddDays(-4), DueDate = Now.AddDays(-20), CompletedAt = Now.AddDays(-3), ProjectId = 1 },
            new TaskItem { Title = "Write essay", Status = TaskItemStatus.Pending, Priority = TaskPriority.High, CreatedAt = Now.AddDays(-3), DueDate = Now.AddDays(45), ProjectId = 1 },
            new TaskItem { Title = "Buy milk", Status = TaskItemStatus.Pending, Priority = TaskPriority.Low, CreatedAt = Now.AddDays(-2), ProjectId = 2 },
            new TaskItem { Title = "Fix the sink", Status = TaskItemStatus.Pending, Priority = TaskPriority.Medium, CreatedAt = Now.AddDays(-1), DueDate = Now.AddDays(-50), ProjectId = 2 },
            new TaskItem { Title = "Plan holiday", Status = TaskItemStatus.Pending, Priority = TaskPriority.Medium, CreatedAt = Now });

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FilterByStatus_ReturnsOnlyThatStatus()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { Status = TaskItemStatus.Pending });

        Assert.Equal(5, page.TotalCount);
        Assert.All(page.Items, task => Assert.Equal(TaskItemStatus.Pending, task.Status));
    }

    [Fact]
    public async Task FilterByStatusAndPriority_AppliesBoth()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters
        {
            Status = TaskItemStatus.Pending,
            Priority = TaskPriority.High
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["Maths homework", "Write essay"], page.Items.Select(task => task.Title));
    }

    [Fact]
    public async Task FilterByProject_ReturnsOnlyThatProjectsTasks()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { ProjectId = 2 });

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, task => Assert.Equal(2, task.ProjectId));
    }

    [Fact]
    public async Task FilterOverdue_ExcludesCompletedAndDeadlineFreeTasks()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { Overdue = true });

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(
            ["Fix the sink", "History homework", "Maths homework"],
            page.Items.Select(task => task.Title).Order());
        // "Read chapter 4" is past its deadline but completed, so it is not overdue.
        Assert.DoesNotContain(page.Items, task => task.Title == "Read chapter 4");
        Assert.All(page.Items, task => Assert.True(task.IsOverdue));
    }

    [Fact]
    public async Task FilterNotOverdue_IsTheExactComplement()
    {
        var overdue = await CreateService().GetTasksAsync(new TaskQueryParameters { Overdue = true, PageSize = 100 });
        var notOverdue = await CreateService().GetTasksAsync(new TaskQueryParameters { Overdue = false, PageSize = 100 });

        Assert.Equal(7, overdue.TotalCount + notOverdue.TotalCount);
        Assert.All(notOverdue.Items, task => Assert.False(task.IsOverdue));
    }

    [Fact]
    public async Task Search_MatchesPartOfTheTitle_IgnoringCase()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { Search = "HOMEwork" });

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, task => Assert.Contains("homework", task.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_TreatsWildcardCharactersLiterally()
    {
        // "%" must not behave as a LIKE wildcard that matches every row.
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { Search = "%" });

        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task SortByPriority_DefaultsToMostUrgentFirst()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters
        {
            SortBy = TaskSortField.Priority,
            PageSize = 100
        });

        var priorities = page.Items.Select(task => task.Priority).ToList();
        Assert.Equal(TaskPriority.Urgent, priorities[0]);
        Assert.Equal(priorities.OrderByDescending(priority => priority), priorities);
    }

    [Fact]
    public async Task SortByDueDate_PutsTasksWithoutADeadlineLast()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters
        {
            SortBy = TaskSortField.DueDate,
            PageSize = 100
        });

        var dueDates = page.Items.Select(task => task.DueDate).ToList();
        var withDeadline = dueDates.Where(date => date.HasValue).Select(date => date!.Value).ToList();

        Assert.Equal(withDeadline.OrderBy(date => date), withDeadline);
        Assert.All(dueDates.Skip(withDeadline.Count), date => Assert.Null(date));
    }

    [Fact]
    public async Task SortByCreatedAt_IsTheDefault()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { PageSize = 100 });

        var created = page.Items.Select(task => task.CreatedAt).ToList();
        Assert.Equal(created.OrderBy(date => date), created);
    }

    [Fact]
    public async Task Pagination_SplitsResultsAndReportsAccurateMetadata()
    {
        var service = CreateService();

        var firstPage = await service.GetTasksAsync(new TaskQueryParameters { Page = 1, PageSize = 3 });
        var lastPage = await service.GetTasksAsync(new TaskQueryParameters { Page = 3, PageSize = 3 });

        Assert.Equal(3, firstPage.Items.Count);
        Assert.Equal(7, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.False(firstPage.HasPreviousPage);
        Assert.True(firstPage.HasNextPage);

        Assert.Single(lastPage.Items);
        Assert.True(lastPage.HasPreviousPage);
        Assert.False(lastPage.HasNextPage);

        // No task appears on two different pages.
        Assert.Empty(firstPage.Items.Select(task => task.Id).Intersect(lastPage.Items.Select(task => task.Id)));
    }

    [Fact]
    public async Task Pagination_BeyondTheLastPage_ReturnsNoItemsButKeepsTheTotal()
    {
        var page = await CreateService().GetTasksAsync(new TaskQueryParameters { Page = 50, PageSize = 10 });

        Assert.Empty(page.Items);
        Assert.Equal(7, page.TotalCount);
    }
}
