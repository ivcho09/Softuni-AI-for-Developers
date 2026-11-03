using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Tasks;

namespace TaskFlow.Interfaces;

/// <summary>Business operations for tasks.</summary>
public interface ITaskService
{
    /// <summary>Returns a filtered, searched, sorted and paginated page of tasks.</summary>
    Task<PagedResponse<TaskResponse>> GetTasksAsync(TaskQueryParameters query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single task.</summary>
    /// <exception cref="Exceptions.NotFoundException">The task does not exist.</exception>
    Task<TaskResponse> GetTaskByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <exception cref="FluentValidation.ValidationException">The referenced project does not exist.</exception>
    Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The task does not exist.</exception>
    /// <exception cref="FluentValidation.ValidationException">The referenced project does not exist.</exception>
    Task<TaskResponse> UpdateTaskAsync(int id, UpdateTaskRequest request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The task does not exist.</exception>
    Task DeleteTaskAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Marks a task completed and stamps its completion time. Idempotent.</summary>
    /// <exception cref="Exceptions.NotFoundException">The task does not exist.</exception>
    Task<TaskResponse> CompleteTaskAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns a task to <c>Pending</c> and clears its completion time. Idempotent.</summary>
    /// <exception cref="Exceptions.NotFoundException">The task does not exist.</exception>
    Task<TaskResponse> ReopenTaskAsync(int id, CancellationToken cancellationToken = default);
}
