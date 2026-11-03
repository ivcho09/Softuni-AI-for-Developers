using Microsoft.EntityFrameworkCore;
using TaskFlow.DTOs.Projects;
using TaskFlow.Exceptions;
using TaskFlow.Models;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

/// <summary>Business rules for projects, including the detach-on-delete behaviour.</summary>
public class ProjectServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new();

    private ProjectService CreateService() => new(_database.CreateContext(), _clock);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task CreateProject_AssignsCreatedAt_AndCanBeRetrieved()
    {
        var service = CreateService();

        var created = await service.CreateProjectAsync(new CreateProjectRequest
        {
            Name = "University",
            Description = "Coursework"
        });

        Assert.True(created.Id > 0);
        Assert.Equal(FixedTimeProvider.DefaultNow, created.CreatedAt);
        Assert.Equal(0, created.TaskCount);

        var retrieved = await CreateService().GetProjectByIdAsync(created.Id);
        Assert.Equal("University", retrieved.Name);
        Assert.Equal("Coursework", retrieved.Description);
    }

    [Fact]
    public async Task GetProjects_ReportsHowManyTasksEachProjectHolds()
    {
        await using (var context = _database.CreateContext())
        {
            context.Projects.AddRange(
                new Project { Id = 1, Name = "University", CreatedAt = FixedTimeProvider.DefaultNow },
                new Project { Id = 2, Name = "Home", CreatedAt = FixedTimeProvider.DefaultNow });
            context.Tasks.AddRange(
                new TaskItem { Title = "A", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 1 },
                new TaskItem { Title = "B", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 1 },
                new TaskItem { Title = "C", CreatedAt = FixedTimeProvider.DefaultNow });
            await context.SaveChangesAsync();
        }

        var projects = await CreateService().GetProjectsAsync();

        Assert.Equal(2, projects.Count);
        Assert.Equal(2, projects.Single(project => project.Id == 1).TaskCount);
        Assert.Equal(0, projects.Single(project => project.Id == 2).TaskCount);
    }

    [Fact]
    public async Task UpdateProject_ChangesNameAndDescription()
    {
        var created = await CreateService().CreateProjectAsync(new CreateProjectRequest { Name = "Old" });

        var updated = await CreateService().UpdateProjectAsync(created.Id, new UpdateProjectRequest
        {
            Name = "New",
            Description = "Updated"
        });

        Assert.Equal("New", updated.Name);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task DeleteProject_KeepsItsTasksAndDetachesThem()
    {
        await using (var context = _database.CreateContext())
        {
            context.Projects.Add(new Project { Id = 1, Name = "University", CreatedAt = FixedTimeProvider.DefaultNow });
            context.Tasks.AddRange(
                new TaskItem { Title = "Keep me", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 1 },
                new TaskItem { Title = "Keep me too", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 1 });
            await context.SaveChangesAsync();
        }

        await CreateService().DeleteProjectAsync(1);

        await using var verification = _database.CreateContext();
        var tasks = await verification.Tasks.ToListAsync();

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, task => Assert.Null(task.ProjectId));
        Assert.Empty(await verification.Projects.ToListAsync());
    }

    [Fact]
    public async Task GetProjectTasks_ReturnsOnlyThatProjectsTasks()
    {
        await using (var context = _database.CreateContext())
        {
            context.Projects.AddRange(
                new Project { Id = 1, Name = "University", CreatedAt = FixedTimeProvider.DefaultNow },
                new Project { Id = 2, Name = "Home", CreatedAt = FixedTimeProvider.DefaultNow });
            context.Tasks.AddRange(
                new TaskItem { Title = "Essay", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 1 },
                new TaskItem { Title = "Milk", CreatedAt = FixedTimeProvider.DefaultNow, ProjectId = 2 });
            await context.SaveChangesAsync();
        }

        var tasks = await CreateService().GetProjectTasksAsync(1);

        Assert.Equal("Essay", Assert.Single(tasks).Title);
    }

    [Fact]
    public async Task GetProjectTasks_ForAProjectWithoutTasks_ReturnsAnEmptyList()
    {
        var created = await CreateService().CreateProjectAsync(new CreateProjectRequest { Name = "Empty" });

        Assert.Empty(await CreateService().GetProjectTasksAsync(created.Id));
    }

    [Fact]
    public async Task OperationsOnAMissingProject_ThrowNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProjectByIdAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteProjectAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProjectTasksAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateProjectAsync(999, new UpdateProjectRequest { Name = "Nope" }));
    }
}
