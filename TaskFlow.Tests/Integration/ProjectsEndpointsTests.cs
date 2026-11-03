using System.Net;
using System.Net.Http.Json;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;

namespace TaskFlow.Tests.Integration;

/// <summary>End-to-end tests for the project endpoints.</summary>
public class ProjectsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client;

    public ProjectsEndpointsTests(TaskFlowApiFactory factory) => _client = factory.CreateClient();

    private async Task<ProjectResponse> CreateProjectAsync(string? name = null)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = name ?? $"Project {Guid.NewGuid():N}",
            description = "Created by an integration test"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(TaskFlowApiFactory.JsonOptions))!;
    }

    [Fact]
    public async Task PostProject_Returns201_AndItCanBeRetrieved()
    {
        var created = await CreateProjectAsync("University");

        Assert.True(created.Id > 0);
        Assert.Equal("University", created.Name);
        Assert.NotEqual(default, created.CreatedAt);

        var fetched = await _client.GetFromJsonAsync<ProjectResponse>(
            $"/api/projects/{created.Id}", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("University", fetched.Name);
    }

    [Fact]
    public async Task GetProjects_ReturnsTheCreatedProjects()
    {
        var created = await CreateProjectAsync();

        var projects = await _client.GetFromJsonAsync<List<ProjectResponse>>(
            "/api/projects", TaskFlowApiFactory.JsonOptions);

        Assert.Contains(projects!, project => project.Id == created.Id);
    }

    [Fact]
    public async Task PutProject_UpdatesIt()
    {
        var created = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            name = "Renamed project",
            description = "New description"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ProjectResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.Equal("Renamed project", updated!.Name);
        Assert.Equal("New description", updated.Description);
    }

    [Fact]
    public async Task GetProjectTasks_ReturnsOnlyThatProjectsTasks()
    {
        var project = await CreateProjectAsync();
        var other = await CreateProjectAsync();

        await _client.PostAsJsonAsync("/api/tasks", new { title = "Mine", projectId = project.Id });
        await _client.PostAsJsonAsync("/api/tasks", new { title = "Not mine", projectId = other.Id });

        var tasks = await _client.GetFromJsonAsync<List<TaskResponse>>(
            $"/api/projects/{project.Id}/tasks", TaskFlowApiFactory.JsonOptions);

        Assert.Equal("Mine", Assert.Single(tasks!).Title);
    }

    [Fact]
    public async Task TaskCount_ReflectsTheTasksAssignedToTheProject()
    {
        var project = await CreateProjectAsync();

        await _client.PostAsJsonAsync("/api/tasks", new { title = "One", projectId = project.Id });
        await _client.PostAsJsonAsync("/api/tasks", new { title = "Two", projectId = project.Id });

        var fetched = await _client.GetFromJsonAsync<ProjectResponse>(
            $"/api/projects/{project.Id}", TaskFlowApiFactory.JsonOptions);

        Assert.Equal(2, fetched!.TaskCount);
    }

    [Fact]
    public async Task DeleteProject_Returns204_AndDetachesItsTasksInsteadOfDeletingThem()
    {
        var project = await CreateProjectAsync();

        var taskResponse = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Survive the project deletion",
            projectId = project.Id
        });
        var task = (await taskResponse.Content
            .ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions))!;

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The project is gone.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/projects/{project.Id}")).StatusCode);

        // The task is not: it survives with no project.
        var survivor = await _client.GetFromJsonAsync<TaskResponse>(
            $"/api/tasks/{task.Id}", TaskFlowApiFactory.JsonOptions);
        Assert.NotNull(survivor);
        Assert.Equal("Survive the project deletion", survivor.Title);
        Assert.Null(survivor.ProjectId);
        Assert.Null(survivor.ProjectName);
    }

    [Theory]
    [InlineData("{\"name\":\"\"}")]
    [InlineData("{\"name\":\"   \"}")]
    public async Task PostProject_WithoutAName_Returns400(string body)
    {
        var response = await _client.PostAsync("/api/projects",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.True(error!.Errors!.ContainsKey("name"));
    }

    [Fact]
    public async Task OperationsOnANonexistentProject_Return404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/projects/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/projects/987654/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/projects/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync("/api/projects/987654", new { name = "Nope" })).StatusCode);
    }
}
