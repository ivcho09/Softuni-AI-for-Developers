using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Interfaces;

namespace TaskFlow.Controllers;

/// <summary>
/// Create, read, update and delete projects, and list the tasks they contain.
/// </summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public class ProjectsController(IProjectService projectService) : ControllerBase
{
    /// <summary>Returns all projects, each with the number of tasks assigned to it.</summary>
    /// <response code="200">The list of projects.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetProjects(
        CancellationToken cancellationToken)
    {
        return Ok(await projectService.GetProjectsAsync(cancellationToken));
    }

    /// <summary>Returns a single project by its id.</summary>
    /// <param name="id">The project id.</param>
    /// <response code="200">The requested project.</response>
    /// <response code="404">No project exists with that id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProjectById(
        int id, CancellationToken cancellationToken)
    {
        return Ok(await projectService.GetProjectByIdAsync(id, cancellationToken));
    }

    /// <summary>Returns every task belonging to a project, most urgent first.</summary>
    /// <param name="id">The project id.</param>
    /// <response code="200">The project's tasks. An empty array if the project has none.</response>
    /// <response code="404">No project exists with that id.</response>
    [HttpGet("{id:int}/tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> GetProjectTasks(
        int id, CancellationToken cancellationToken)
    {
        return Ok(await projectService.GetProjectTasksAsync(id, cancellationToken));
    }

    /// <summary>Creates a project.</summary>
    /// <param name="request">The project to create.</param>
    /// <response code="201">The created project, with a Location header pointing at it.</response>
    /// <response code="400">The payload failed validation.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        [FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var created = await projectService.CreateProjectAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProjectById), new { id = created.Id }, created);
    }

    /// <summary>Replaces an existing project.</summary>
    /// <param name="id">The project id.</param>
    /// <param name="request">The new state of the project.</param>
    /// <response code="200">The updated project.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="404">No project exists with that id.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(
        int id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        return Ok(await projectService.UpdateProjectAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes a project without deleting its tasks.</summary>
    /// <remarks>
    /// Tasks that belonged to the project are kept and their <c>projectId</c> is set to null,
    /// so no work is ever lost by removing a project.
    /// </remarks>
    /// <param name="id">The project id.</param>
    /// <response code="204">The project was deleted and its tasks were detached.</response>
    /// <response code="404">No project exists with that id.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken cancellationToken)
    {
        await projectService.DeleteProjectAsync(id, cancellationToken);
        return NoContent();
    }
}
