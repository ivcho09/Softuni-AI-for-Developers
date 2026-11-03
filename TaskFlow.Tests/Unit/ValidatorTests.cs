using TaskFlow.DTOs.Projects;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Models;
using TaskFlow.Validators;

namespace TaskFlow.Tests.Unit;

/// <summary>Input validation rules, checked without going through HTTP.</summary>
public class ValidatorTests
{
    private readonly CreateTaskRequestValidator _taskValidator = new();
    private readonly CreateProjectRequestValidator _projectValidator = new();
    private readonly TaskQueryParametersValidator _queryValidator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TaskTitle_CannotBeEmpty(string title)
    {
        var result = _taskValidator.Validate(new CreateTaskRequest { Title = title });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTaskRequest.Title));
    }

    [Fact]
    public void TaskTitle_CannotExceed100Characters()
    {
        var result = _taskValidator.Validate(new CreateTaskRequest { Title = new string('x', 101) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("100"));
    }

    [Fact]
    public void TaskTitle_OfExactly100Characters_IsAccepted()
    {
        Assert.True(_taskValidator.Validate(new CreateTaskRequest { Title = new string('x', 100) }).IsValid);
    }

    [Fact]
    public void Description_CannotExceed1000Characters()
    {
        var result = _taskValidator.Validate(new CreateTaskRequest
        {
            Title = "Valid",
            Description = new string('x', 1001)
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTaskRequest.Description));
    }

    [Fact]
    public void PastDueDates_AreAllowed_BecauseOverdueTasksMustBeRecordable()
    {
        var result = _taskValidator.Validate(new CreateTaskRequest
        {
            Title = "Already late",
            DueDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1899-01-01T00:00:00Z")]
    [InlineData("2500-01-01T00:00:00Z")]
    public void DueDate_OutsideTheSupportedRange_IsRejected(string dueDate)
    {
        var result = _taskValidator.Validate(new CreateTaskRequest
        {
            Title = "Valid",
            DueDate = DateTime.Parse(dueDate).ToUniversalTime()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTaskRequest.DueDate));
    }

    [Fact]
    public void UnsupportedStatusOrPriority_IsRejected()
    {
        var result = _taskValidator.Validate(new CreateTaskRequest
        {
            Title = "Valid",
            Status = (TaskItemStatus)42,
            Priority = (TaskPriority)99
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTaskRequest.Status));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTaskRequest.Priority));
    }

    [Fact]
    public void ProjectName_CannotBeEmpty()
    {
        var result = _projectValidator.Validate(new CreateProjectRequest { Name = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectRequest.Name));
    }

    [Fact]
    public void ValidPayloads_PassValidation()
    {
        Assert.True(_taskValidator.Validate(new CreateTaskRequest
        {
            Title = "Finish homework",
            Description = "Chapter 4",
            Status = TaskItemStatus.InProgress,
            Priority = TaskPriority.Urgent,
            DueDate = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc),
            ProjectId = 1
        }).IsValid);

        Assert.True(_projectValidator.Validate(new CreateProjectRequest { Name = "University" }).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void QueryParameters_WithInvalidPaging_AreRejected(int page, int pageSize)
    {
        var result = _queryValidator.Validate(new TaskQueryParameters { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void QueryParameters_WithDefaultPaging_AreAccepted()
    {
        Assert.True(_queryValidator.Validate(new TaskQueryParameters()).IsValid);
    }
}
