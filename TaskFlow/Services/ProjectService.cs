using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Exceptions;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Services;

/// <inheritdoc cref="IProjectService"/>
public class ProjectService(TaskFlowDbContext dbContext, TimeProvider timeProvider) : IProjectService
{
    public async Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        // Projected in one query so the task counts do not cause N+1 round trips.
        return await dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Id)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                TaskCount = project.Tasks.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectResponse> GetProjectByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == id)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                TaskCount = project.Tasks.Count
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.ForProject(id);
    }

    public async Task<ProjectResponse> CreateProjectAsync(
        CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProjectResponse.FromEntity(project, taskCount: 0);
    }

    public async Task<ProjectResponse> UpdateProjectAsync(
        int id, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw NotFoundException.ForProject(id);

        project.Name = request.Name.Trim();
        project.Description = request.Description;

        await dbContext.SaveChangesAsync(cancellationToken);

        var taskCount = await dbContext.Tasks.CountAsync(task => task.ProjectId == id, cancellationToken);
        return ProjectResponse.FromEntity(project, taskCount);
    }

    public async Task DeleteProjectAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw NotFoundException.ForProject(id);

        // Loading the tasks lets EF apply the SetNull delete behaviour to the tracked
        // entities; the FK in SQLite is also declared ON DELETE SET NULL as a backstop.
        await dbContext.Tasks.Where(task => task.ProjectId == id).LoadAsync(cancellationToken);

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskResponse>> GetProjectTasksAsync(
        int projectId, CancellationToken cancellationToken = default)
    {
        var projectExists = await dbContext.Projects
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            throw NotFoundException.ForProject(projectId);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var tasks = await dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Project)
            .Where(task => task.ProjectId == projectId)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);

        return tasks.Select(task => TaskResponse.FromEntity(task, utcNow)).ToList();
    }
}
