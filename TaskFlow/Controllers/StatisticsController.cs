using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Statistics;
using TaskFlow.Interfaces;

namespace TaskFlow.Controllers;

/// <summary>
/// Productivity reporting calculated from the stored tasks.
/// </summary>
[ApiController]
[Route("api/statistics")]
[Produces("application/json")]
public class StatisticsController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>Returns productivity statistics across all tasks.</summary>
    /// <remarks>
    /// Overdue counts tasks whose deadline has passed and that are not completed.
    /// <c>completionPercentage</c> is completed / total * 100, and is 0 when there are no tasks.
    /// </remarks>
    /// <response code="200">The aggregated statistics.</response>
    [HttpGet]
    [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatisticsResponse>> GetStatistics(CancellationToken cancellationToken)
    {
        return Ok(await statisticsService.GetOverallStatisticsAsync(cancellationToken));
    }

    /// <summary>Returns productivity statistics for a single project.</summary>
    /// <param name="projectId">The project id.</param>
    /// <response code="200">The aggregated statistics for the project.</response>
    /// <response code="404">No project exists with that id.</response>
    [HttpGet("projects/{projectId:int}")]
    [ProducesResponseType(typeof(ProjectStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectStatisticsResponse>> GetProjectStatistics(
        int projectId, CancellationToken cancellationToken)
    {
        return Ok(await statisticsService.GetProjectStatisticsAsync(projectId, cancellationToken));
    }
}
