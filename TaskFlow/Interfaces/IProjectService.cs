using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;

namespace TaskFlow.Interfaces;

/// <summary>Business operations for projects.</summary>
public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The project does not exist.</exception>
    Task<ProjectResponse> GetProjectByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The project does not exist.</exception>
    Task<ProjectResponse> UpdateProjectAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a project and detaches its tasks instead of deleting them.</summary>
    /// <exception cref="Exceptions.NotFoundException">The project does not exist.</exception>
    Task DeleteProjectAsync(int id, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">The project does not exist.</exception>
    Task<IReadOnlyList<TaskResponse>> GetProjectTasksAsync(int projectId, CancellationToken cancellationToken = default);
}
