using System.Net;
using System.Net.Http.Json;
using TaskFlow.DTOs.Common;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Models;

namespace TaskFlow.Tests.Integration;

/// <summary>End-to-end tests for the task endpoints, driven over real HTTP.</summary>
public class TasksEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client;

    public TasksEndpointsTests(TaskFlowApiFactory factory) => _client = factory.CreateClient();

    private async Task<TaskResponse> CreateTaskAsync(object payload)
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions))!;
    }

    [Fact]
    public async Task PostTask_Returns201_WithALocationHeader()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Finish the integration tests",
            description = "Cover every endpoint",
            priority = "High",
            dueDate = "2026-12-01T09:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Finish the integration tests", created.Title);
        Assert.Equal(TaskPriority.High, created.Priority);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.NotEqual(default, created.CreatedAt);

        // The Location header must actually resolve.
        var followed = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_ReturnsTheStoredTask()
    {
        var created = await CreateTaskAsync(new { title = "Retrieve me" });

        var fetched = await _client.GetFromJsonAsync<TaskResponse>(
            $"/api/tasks/{created.Id}", TaskFlowApiFactory.JsonOptions);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Retrieve me", fetched.Title);
    }

    [Fact]
    public async Task PutTask_UpdatesTheTask()
    {
        var created = await CreateTaskAsync(new { title = "Before" });

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", new
        {
            title = "After",
            status = "InProgress",
            priority = "Urgent"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.Equal("After", updated!.Title);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
        Assert.Equal(TaskPriority.Urgent, updated.Priority);
    }

    [Fact]
    public async Task DeleteTask_Returns204_AndTheTaskIsGone()
    {
        var created = await CreateTaskAsync(new { title = "Delete me" });

        var deleteResponse = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task CompleteThenReopen_MaintainsCompletedAt()
    {
        var created = await CreateTaskAsync(new { title = "Complete me" });

        var completeResponse = await _client.PostAsync($"/api/tasks/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completed = await completeResponse.Content
            .ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.Equal(TaskItemStatus.Completed, completed!.Status);
        Assert.NotNull(completed.CompletedAt);

        var reopenResponse = await _client.PostAsync($"/api/tasks/{created.Id}/reopen", null);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var reopened = await reopenResponse.Content
            .ReadFromJsonAsync<TaskResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.Equal(TaskItemStatus.Pending, reopened!.Status);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task GetTasks_SupportsFilteringSearchingAndPagination()
    {
        var project = await _client.PostAsJsonAsync("/api/projects", new { name = $"Filter project {Guid.NewGuid():N}" });
        var projectId = (await project.Content
            .ReadFromJsonAsync<ProjectResponse>(TaskFlowApiFactory.JsonOptions))!.Id;

        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateTaskAsync(new { title = $"{marker} urgent report", priority = "Urgent", projectId });
        await CreateTaskAsync(new { title = $"{marker} low chore", priority = "Low", projectId });
        await CreateTaskAsync(new { title = $"{marker} done item", priority = "High", status = "Completed", projectId });

        var byStatus = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?projectId={projectId}&status=Pending", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(2, byStatus!.TotalCount);

        var byPriority = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?projectId={projectId}&priority=Urgent", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(TaskPriority.Urgent, Assert.Single(byPriority!.Items).Priority);

        var bySearch = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?search={marker}", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(3, bySearch!.TotalCount);

        var paged = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?projectId={projectId}&page=1&pageSize=2", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(2, paged!.Items.Count);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.TotalPages);
        Assert.True(paged.HasNextPage);
        Assert.False(paged.HasPreviousPage);
    }

    [Fact]
    public async Task GetTasks_OverdueFilter_ExcludesCompletedTasks()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var pastDue = "2020-01-01T00:00:00Z";

        await CreateTaskAsync(new { title = $"{marker} overdue open", dueDate = pastDue });
        await CreateTaskAsync(new { title = $"{marker} overdue done", dueDate = pastDue, status = "Completed" });
        await CreateTaskAsync(new { title = $"{marker} future", dueDate = "2099-01-01T00:00:00Z" });

        var overdue = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?search={marker}&overdue=true", TaskFlowApiFactory.JsonOptions);

        var single = Assert.Single(overdue!.Items);
        Assert.Equal($"{marker} overdue open", single.Title);
        Assert.True(single.IsOverdue);
    }

    [Fact]
    public async Task GetTasks_SortByDueDate_ReturnsSoonestFirst()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateTaskAsync(new { title = $"{marker} later", dueDate = "2030-06-01T00:00:00Z" });
        await CreateTaskAsync(new { title = $"{marker} sooner", dueDate = "2030-01-01T00:00:00Z" });

        var sorted = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>(
            $"/api/tasks?search={marker}&sortBy=dueDate", TaskFlowApiFactory.JsonOptions);

        Assert.Equal($"{marker} sooner", sorted!.Items[0].Title);
        Assert.Equal($"{marker} later", sorted.Items[1].Title);
    }

    [Theory]
    [InlineData("{\"title\":\"\"}", "title")]
    [InlineData("{\"title\":\"Valid\",\"status\":\"Bogus\"}", "status")]
    [InlineData("{\"title\":\"Valid\",\"projectId\":987654}", "projectId")]
    public async Task PostTask_WithInvalidInput_Returns400_WithFieldLevelErrors(string body, string expectedField)
    {
        var response = await _client.PostAsync("/api/tasks",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.NotNull(error);
        Assert.Equal(400, error.Status);
        Assert.NotNull(error.Errors);
        Assert.True(error.Errors.ContainsKey(expectedField),
            $"Expected an error for '{expectedField}' but got: {string.Join(", ", error.Errors.Keys)}");
    }

    [Fact]
    public async Task PostTask_WithATooLongTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_WithAnInvalidPageSize_Returns400()
    {
        var response = await _client.GetAsync("/api/tasks?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OperationsOnANonexistentTask_Return404_WithTheErrorContract()
    {
        var get = await _client.GetAsync("/api/tasks/987654");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var error = await get.Content.ReadFromJsonAsync<ErrorResponse>(TaskFlowApiFactory.JsonOptions);
        Assert.Equal(404, error!.Status);
        Assert.Contains("987654", error.Message);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/tasks/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsync("/api/tasks/987654/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsync("/api/tasks/987654/reopen", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync("/api/tasks/987654", new { title = "Nope" })).StatusCode);
    }
}
