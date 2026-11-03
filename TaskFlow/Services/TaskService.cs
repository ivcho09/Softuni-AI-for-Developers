using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Exceptions;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Services;

/// <inheritdoc cref="ITaskService"/>
public class TaskService(TaskFlowDbContext dbContext, TimeProvider timeProvider) : ITaskService
{
    private const string LikeEscapeCharacter = "\\";

    public async Task<PagedResponse<TaskResponse>> GetTasksAsync(
        TaskQueryParameters query, CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var filtered = ApplyFilters(dbContext.Tasks.AsNoTracking(), query, utcNow);

        // Counted before paging so the metadata describes the whole result set.
        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await ApplySorting(filtered, query)
            .Include(task => task.Project)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<TaskResponse>
        {
            Items = items.Select(task => TaskResponse.FromEntity(task, utcNow)).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TaskResponse> GetTaskByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw NotFoundException.ForTask(id);

        return TaskResponse.FromEntity(task, timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<TaskResponse> CreateTaskAsync(
        CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectExistsAsync(request.ProjectId, cancellationToken);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = request.Priority,
            CreatedAt = utcNow,
            DueDate = request.DueDate,
            ProjectId = request.ProjectId
        };

        // A task created directly in the Completed state still needs its timestamp.
        ApplyStatusTransition(task, request.Status, utcNow);

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        await LoadProjectAsync(task, cancellationToken);
        return TaskResponse.FromEntity(task, utcNow);
    }

    public async Task<TaskResponse> UpdateTaskAsync(
        int id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw NotFoundException.ForTask(id);

        await EnsureProjectExistsAsync(request.ProjectId, cancellationToken);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.ProjectId = request.ProjectId;
        ApplyStatusTransition(task, request.Status, utcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await LoadProjectAsync(task, cancellationToken);
        return TaskResponse.FromEntity(task, utcNow);
    }

    public async Task DeleteTaskAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw NotFoundException.ForTask(id);

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TaskResponse> CompleteTaskAsync(int id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, TaskItemStatus.Completed, cancellationToken);

    public Task<TaskResponse> ReopenTaskAsync(int id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, TaskItemStatus.Pending, cancellationToken);

    private async Task<TaskResponse> ChangeStatusAsync(
        int id, TaskItemStatus status, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw NotFoundException.ForTask(id);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        ApplyStatusTransition(task, status, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await LoadProjectAsync(task, cancellationToken);
        return TaskResponse.FromEntity(task, utcNow);
    }

    /// <summary>
    /// The one place the CompletedAt rule lives: entering Completed stamps the time
    /// (preserving an existing stamp, so completing twice is idempotent), and leaving
    /// Completed clears it.
    /// </summary>
    private static void ApplyStatusTransition(TaskItem task, TaskItemStatus newStatus, DateTime utcNow)
    {
        task.Status = newStatus;
        task.CompletedAt = newStatus == TaskItemStatus.Completed
            ? task.CompletedAt ?? utcNow
            : null;
    }

    /// <summary>Rejects a task that points at a project which does not exist.</summary>
    private async Task EnsureProjectExistsAsync(int? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return;
        }

        var exists = await dbContext.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!exists)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(CreateTaskRequest.ProjectId),
                    "Project with id " + projectId + " does not exist.")
            ]);
        }
    }

    /// <summary>Populates the project navigation so the response can include the project name.</summary>
    private async Task LoadProjectAsync(TaskItem task, CancellationToken cancellationToken)
    {
        task.Project = task.ProjectId is null
            ? null
            : await dbContext.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId, cancellationToken);
    }

    private static IQueryable<TaskItem> ApplyFilters(
        IQueryable<TaskItem> tasks, TaskQueryParameters query, DateTime utcNow)
    {
        if (query.Status.HasValue)
        {
            tasks = tasks.Where(task => task.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            tasks = tasks.Where(task => task.Priority == query.Priority.Value);
        }

        if (query.ProjectId.HasValue)
        {
            tasks = tasks.Where(task => task.ProjectId == query.ProjectId.Value);
        }

        if (query.Overdue.HasValue)
        {
            tasks = query.Overdue.Value
                ? tasks.Where(task => task.Status != TaskItemStatus.Completed
                                      && task.DueDate != null
                                      && task.DueDate < utcNow)
                : tasks.Where(task => task.Status == TaskItemStatus.Completed
                                      || task.DueDate == null
                                      || task.DueDate >= utcNow);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE keeps the match case-insensitive in SQLite, unlike Contains/instr.
            var pattern = "%" + EscapeLikePattern(query.Search.Trim()) + "%";
            tasks = tasks.Where(task => EF.Functions.Like(task.Title, pattern, LikeEscapeCharacter));
        }

        return tasks;
    }

    /// <summary>Stops user input containing % or _ from acting as a LIKE wildcard.</summary>
    private static string EscapeLikePattern(string value) => value
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
        .Replace("%", LikeEscapeCharacter + "%")
        .Replace("_", LikeEscapeCharacter + "_");

    private static IQueryable<TaskItem> ApplySorting(IQueryable<TaskItem> tasks, TaskQueryParameters query)
    {
        var descending = query.SortDescending;

        return query.SortBy switch
        {
            // Tasks without a deadline are listed after those that have one.
            TaskSortField.DueDate => descending
                ? tasks.OrderBy(task => task.DueDate == null)
                       .ThenByDescending(task => task.DueDate).ThenBy(task => task.Id)
                : tasks.OrderBy(task => task.DueDate == null)
                       .ThenBy(task => task.DueDate).ThenBy(task => task.Id),
            TaskSortField.Priority => descending
                ? tasks.OrderByDescending(task => task.Priority).ThenBy(task => task.Id)
                : tasks.OrderBy(task => task.Priority).ThenBy(task => task.Id),
            TaskSortField.Title => descending
                ? tasks.OrderByDescending(task => task.Title).ThenBy(task => task.Id)
                : tasks.OrderBy(task => task.Title).ThenBy(task => task.Id),
            _ => descending
                ? tasks.OrderByDescending(task => task.CreatedAt).ThenByDescending(task => task.Id)
                : tasks.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id)
        };
    }
}
