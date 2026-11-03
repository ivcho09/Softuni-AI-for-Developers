using System.Net;
using System.Net.Http.Json;
using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Statistics;

namespace TaskFlow.Tests.Integration;

/// <summary>
/// End-to-end tests for the statistics endpoints. Each test works against its own
/// project so that the figures are deterministic even though the whole class shares
/// one database.
/// </summary>
public class StatisticsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client;

    public StatisticsEndpointsTests(TaskFlowApiFactory factory) => _client = factory.CreateClient();

    private async Task<int> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { name = $"Stats {Guid.NewGuid():N}" });
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(TaskFlowApiFactory.JsonOptions))!.Id;
    }

    private async Task AddTaskAsync(int projectId, string title, string status, string priority, string? dueDate = null)
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title,
            status,
            priority,
            dueDate,
            projectId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_ReturnsEveryFieldAndASensibleTotal()
    {
        var projectId = await CreateProjectAsync();
        await AddTaskAsync(projectId, "Alpha", "Pending", "High");

        var statistics = await _client.GetFromJsonAsync<StatisticsResponse>(
            "/api/statistics", TaskFlowApiFactory.JsonOptions);

        Assert.NotNull(statistics);
        Assert.True(statistics.TotalTasks >= 1);
        Assert.Equal(
            statistics.TotalTasks,
            statistics.PendingTasks + statistics.InProgressTasks + statistics.CompletedTasks);
        Assert.Equal(["Low", "Medium", "High", "Urgent"], statistics.TasksByPriority.Keys);
        Assert.Equal(["Pending", "InProgress", "Completed"], statistics.TasksByStatus.Keys);
        Assert.InRange(statistics.CompletionPercentage, 0, 100);
    }

    [Fact]
    public async Task GetProjectStatistics_CalculatesFromTheStoredTasks()
    {
        var projectId = await CreateProjectAsync();

        await AddTaskAsync(projectId, "Overdue and open", "Pending", "High", "2020-01-01T00:00:00Z");
        await AddTaskAsync(projectId, "Overdue but done", "Completed", "Low", "2020-01-01T00:00:00Z");
        await AddTaskAsync(projectId, "Being worked on", "InProgress", "Urgent", "2099-01-01T00:00:00Z");
        await AddTaskAsync(projectId, "No deadline", "Pending", "Medium");

        var statistics = await _client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/statistics/projects/{projectId}", TaskFlowApiFactory.JsonOptions);

        Assert.NotNull(statistics);
        Assert.Equal(projectId, statistics.ProjectId);
        Assert.Equal(4, statistics.TotalTasks);
        Assert.Equal(1, statistics.CompletedTasks);
        Assert.Equal(2, statistics.PendingTasks);
        Assert.Equal(1, statistics.InProgressTasks);
        // Only the open past-due task counts; the completed one does not.
        Assert.Equal(1, statistics.OverdueTasks);
        Assert.Equal(25, statistics.CompletionPercentage);
    }

    [Fact]
    public async Task GetProjectStatistics_ForAnEmptyProject_ReportsZeroPercent()
    {
        var projectId = await CreateProjectAsync();

        var statistics = await _client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/statistics/projects/{projectId}", TaskFlowApiFactory.JsonOptions);

        Assert.Equal(0, statistics!.TotalTasks);
        Assert.Equal(0, statistics.CompletionPercentage);
        Assert.Equal(0, statistics.OverdueTasks);
    }

    [Fact]
    public async Task CompletingATask_MovesTheProjectPercentage()
    {
        var projectId = await CreateProjectAsync();
        await AddTaskAsync(projectId, "One", "Pending", "Medium");
        await AddTaskAsync(projectId, "Two", "Pending", "Medium");

        var before = await _client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/statistics/projects/{projectId}", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(0, before!.CompletionPercentage);

        var tasks = await _client.GetFromJsonAsync<List<DTOs.Tasks.TaskResponse>>(
            $"/api/projects/{projectId}/tasks", TaskFlowApiFactory.JsonOptions);
        await _client.PostAsync($"/api/tasks/{tasks![0].Id}/complete", null);

        var after = await _client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/statistics/projects/{projectId}", TaskFlowApiFactory.JsonOptions);
        Assert.Equal(50, after!.CompletionPercentage);
        Assert.Equal(1, after.CompletedTasks);
    }

    [Fact]
    public async Task GetProjectStatistics_ForAMissingProject_Returns404()
    {
        var response = await _client.GetAsync("/api/statistics/projects/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
