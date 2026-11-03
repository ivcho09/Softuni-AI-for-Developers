using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Interfaces;

namespace TaskFlow.Controllers;

/// <summary>
/// Create, read, update and delete tasks, and move them between open and completed.
/// </summary>
[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Returns a page of tasks, with optional filtering, searching and sorting.</summary>
    /// <remarks>
    /// All query parameters are optional and can be combined, for example
    /// <c>GET /api/tasks?status=Pending&amp;priority=High</c>,
    /// <c>GET /api/tasks?overdue=true</c>,
    /// <c>GET /api/tasks?search=homework</c>,
    /// <c>GET /api/tasks?sortBy=dueDate&amp;page=2&amp;pageSize=10</c>.
    /// </remarks>
    /// <param name="query">Filtering, searching, sorting and pagination options.</param>
    /// <response code="200">A page of matching tasks together with pagination metadata.</response>
    /// <response code="400">One or more query parameters were invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> GetTasks(
        [FromQuery] TaskQueryParameters query, CancellationToken cancellationToken)
    {
        return Ok(await taskService.GetTasksAsync(query, cancellationToken));
    }

    /// <summary>Returns a single task by its id.</summary>
    /// <param name="id">The task id.</param>
    /// <response code="200">The requested task.</response>
    /// <response code="404">No task exists with that id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetTaskById(int id, CancellationToken cancellationToken)
    {
        return Ok(await taskService.GetTaskByIdAsync(id, cancellationToken));
    }

    /// <summary>Creates a task.</summary>
    /// <remarks>
    /// <c>CreatedAt</c> is assigned by the server. Supplying a <c>projectId</c> that does
    /// not exist is rejected with 400.
    /// </remarks>
    /// <param name="request">The task to create.</param>
    /// <response code="201">The created task, with a Location header pointing at it.</response>
    /// <response code="400">The payload failed validation, or the project does not exist.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> CreateTask(
        [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var created = await taskService.CreateTaskAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTaskById), new { id = created.Id }, created);
    }

    /// <summary>Replaces an existing task.</summary>
    /// <remarks>
    /// This is a full replacement: omitted optional fields are cleared. Setting
    /// <c>status</c> to <c>Completed</c> stamps <c>completedAt</c>; any other status clears it.
    /// </remarks>
    /// <param name="id">The task id.</param>
    /// <param name="request">The new state of the task.</param>
    /// <response code="200">The updated task.</response>
    /// <response code="400">The payload failed validation, or the project does not exist.</response>
    /// <response code="404">No task exists with that id.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> UpdateTask(
        int id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await taskService.UpdateTaskAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes a task permanently.</summary>
    /// <param name="id">The task id.</param>
    /// <response code="204">The task was deleted.</response>
    /// <response code="404">No task exists with that id.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken cancellationToken)
    {
        await taskService.DeleteTaskAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Marks a task as completed and records the completion time.</summary>
    /// <remarks>Idempotent: completing an already completed task keeps the original timestamp.</remarks>
    /// <param name="id">The task id.</param>
    /// <response code="200">The completed task.</response>
    /// <response code="404">No task exists with that id.</response>
    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> CompleteTask(int id, CancellationToken cancellationToken)
    {
        return Ok(await taskService.CompleteTaskAsync(id, cancellationToken));
    }

    /// <summary>Reopens a task, returning it to <c>Pending</c> and clearing its completion time.</summary>
    /// <param name="id">The task id.</param>
    /// <response code="200">The reopened task.</response>
    /// <response code="404">No task exists with that id.</response>
    [HttpPost("{id:int}/reopen")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> ReopenTask(int id, CancellationToken cancellationToken)
    {
        return Ok(await taskService.ReopenTaskAsync(id, cancellationToken));
    }
}
