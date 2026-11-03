using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Exceptions;
using TaskFlow.Models;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

/// <summary>Business rules for tasks, exercised directly against the service.</summary>
public class TaskServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new();

    private TaskService CreateService() => new(_database.CreateContext(), _clock);

    public void Dispose() => _database.Dispose();

    private async Task<int> SeedProjectAsync(string name = "University")
    {
        await using var context = _database.CreateContext();
        var project = new Project { Name = name, CreatedAt = FixedTimeProvider.DefaultNow };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedTasksAsync(params TaskItem[] tasks)
    {
        await using var context = _database.CreateContext();
        context.Tasks.AddRange(tasks);
        await context.SaveChangesAsync();
    }

    // --- Creating ---------------------------------------------------------

    [Fact]
    public async Task CreateTask_AssignsCreatedAt_AndPersistsTheTask()
    {
        var service = CreateService();

        var created = await service.CreateTaskAsync(new CreateTaskRequest
        {
            Title = "  Finish homework  ",
            Description = "Chapter 4",
            Priority = TaskPriority.High,
            DueDate = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(created.Id > 0);
        Assert.Equal("Finish homework", created.Title); // whitespace is trimmed
        Assert.Equal(FixedTimeProvider.DefaultNow, created.CreatedAt);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.Null(created.CompletedAt);

        await using var context = _database.CreateContext();
        var stored = await context.Tasks.SingleAsync();
        Assert.Equal("Finish homework", stored.Title);
        Assert.Equal(FixedTimeProvider.DefaultNow, stored.CreatedAt);
    }

    [Fact]
    public async Task CreateTask_InCompletedState_StampsCompletedAt()
    {
        var service = CreateService();

        var created = await service.CreateTaskAsync(new CreateTaskRequest
        {
            Title = "Already done",
            Status = TaskItemStatus.Completed
        });

        Assert.Equal(FixedTimeProvider.DefaultNow, created.CompletedAt);
    }

    [Fact]
    public async Task CreateTask_WithNonexistentProject_IsRejected()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTaskAsync(new CreateTaskRequest { Title = "Orphan", ProjectId = 4242 }));

        Assert.Contains(exception.Errors, failure => failure.PropertyName == nameof(CreateTaskRequest.ProjectId));
    }

    [Fact]
    public async Task CreateTask_WithExistingProject_LinksThem()
    {
        var projectId = await SeedProjectAsync();
        var service = CreateService();

        var created = await service.CreateTaskAsync(new CreateTaskRequest
        {
            Title = "Write essay",
            ProjectId = projectId
        });

        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal("University", created.ProjectName);
    }

    // --- Reading ----------------------------------------------------------

    [Fact]
    public async Task GetTaskById_ReturnsTheTask()
    {
        await SeedTasksAsync(new TaskItem { Title = "Read chapter 4", CreatedAt = FixedTimeProvider.DefaultNow });
        var service = CreateService();

        var task = await service.GetTaskByIdAsync(1);

        Assert.Equal("Read chapter 4", task.Title);
    }

    [Fact]
    public async Task GetTaskById_WhenMissing_ThrowsNotFound()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.GetTaskByIdAsync(999));

        Assert.Contains("999", exception.Message);
    }

    // --- Updating ---------------------------------------------------------

    [Fact]
    public async Task UpdateTask_ReplacesEveryField()
    {
        await SeedTasksAsync(new TaskItem
        {
            Title = "Old title",
            Description = "Old description",
            Priority = TaskPriority.Low,
            CreatedAt = FixedTimeProvider.DefaultNow,
            DueDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var service = CreateService();

        var updated = await service.UpdateTaskAsync(1, new UpdateTaskRequest
        {
            Title = "New title",
            Status = TaskItemStatus.InProgress,
            Priority = TaskPriority.Urgent
        });

        Assert.Equal("New title", updated.Title);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
        Assert.Equal(TaskPriority.Urgent, updated.Priority);
        // A full replacement clears the fields that were not supplied.
        Assert.Null(updated.Description);
        Assert.Null(updated.DueDate);
    }

    [Fact]
    public async Task UpdateTask_ToCompleted_StampsCompletedAt_AndClearsItWhenMovedBack()
    {
        await SeedTasksAsync(new TaskItem { Title = "Task", CreatedAt = FixedTimeProvider.DefaultNow });
        var service = CreateService();

        var completed = await service.UpdateTaskAsync(1, new UpdateTaskRequest
        {
            Title = "Task",
            Status = TaskItemStatus.Completed
        });
        Assert.Equal(FixedTimeProvider.DefaultNow, completed.CompletedAt);

        var reopened = await service.UpdateTaskAsync(1, new UpdateTaskRequest
        {
            Title = "Task",
            Status = TaskItemStatus.InProgress
        });
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task UpdateTask_WhenMissing_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateTaskAsync(999, new UpdateTaskRequest { Title = "Nope" }));
    }

    // --- Deleting ---------------------------------------------------------

    [Fact]
    public async Task DeleteTask_RemovesItFromTheDatabase()
    {
        await SeedTasksAsync(new TaskItem { Title = "Temporary", CreatedAt = FixedTimeProvider.DefaultNow });
        var service = CreateService();

        await service.DeleteTaskAsync(1);

        await using var context = _database.CreateContext();
        Assert.Empty(await context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task DeleteTask_WhenMissing_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteTaskAsync(999));
    }

    // --- Completing and reopening ----------------------------------------

    [Fact]
    public async Task CompleteTask_SetsStatusAndTimestamp()
    {
        await SeedTasksAsync(new TaskItem { Title = "Task", CreatedAt = FixedTimeProvider.DefaultNow });
        var service = CreateService();

        var completed = await service.CompleteTaskAsync(1);

        Assert.Equal(TaskItemStatus.Completed, completed.Status);
        Assert.Equal(FixedTimeProvider.DefaultNow, completed.CompletedAt);
    }

    [Fact]
    public async Task CompleteTask_Twice_KeepsTheOriginalTimestamp()
    {
        await SeedTasksAsync(new TaskItem { Title = "Task", CreatedAt = FixedTimeProvider.DefaultNow });

        var first = await CreateService().CompleteTaskAsync(1);

        _clock.UtcNow = FixedTimeProvider.DefaultNow.AddDays(5);
        var second = await CreateService().CompleteTaskAsync(1);

        Assert.Equal(first.CompletedAt, second.CompletedAt);
    }

    [Fact]
    public async Task ReopenTask_ReturnsItToPending_AndClearsCompletedAt()
    {
        await SeedTasksAsync(new TaskItem
        {
            Title = "Task",
            Status = TaskItemStatus.Completed,
            CreatedAt = FixedTimeProvider.DefaultNow,
            CompletedAt = FixedTimeProvider.DefaultNow
        });
        var service = CreateService();

        var reopened = await service.ReopenTaskAsync(1);

        Assert.Equal(TaskItemStatus.Pending, reopened.Status);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task CompleteTask_WhenMissing_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CompleteTaskAsync(999));
    }
}
