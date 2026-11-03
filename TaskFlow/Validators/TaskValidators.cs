using FluentValidation;
using TaskFlow.DTOs.Tasks;
using TaskFlow.Models;

namespace TaskFlow.Validators;

/// <summary>
/// Rules shared by the create and update task payloads.
/// </summary>
public static class TaskRuleExtensions
{
    /// <summary>
    /// A deadline is accepted anywhere in a sane calendar range. Past deadlines are
    /// deliberately allowed, because a task that is already overdue is a legitimate
    /// thing to record, and the overdue filter exists precisely to surface them.
    /// </summary>
    public static readonly DateTime EarliestAllowedDueDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime LatestAllowedDueDate = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IRuleBuilderOptions<T, DateTime?> ValidDueDate<T>(
        this IRuleBuilder<T, DateTime?> rule) =>
        rule.Must(dueDate => dueDate is null
                             || (dueDate.Value.ToUniversalTime() >= EarliestAllowedDueDate
                                 && dueDate.Value.ToUniversalTime() <= LatestAllowedDueDate))
            .WithMessage("Due date must be a valid date between 2000-01-01 and 2100-01-01.");

    public static IRuleBuilderOptions<T, int?> ValidProjectId<T>(
        this IRuleBuilder<T, int?> rule) =>
        rule.Must(projectId => projectId is null || projectId > 0)
            .WithMessage("Project id must be a positive number.");
}

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(ValidationLimits.TaskTitleMaxLength)
            .WithMessage($"Title cannot exceed {ValidationLimits.TaskTitleMaxLength} characters.");

        RuleFor(request => request.Description)
            .MaximumLength(ValidationLimits.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {ValidationLimits.DescriptionMaxLength} characters.");

        RuleFor(request => request.Status)
            .IsInEnum().WithMessage("Status must be one of: Pending, InProgress, Completed.");

        RuleFor(request => request.Priority)
            .IsInEnum().WithMessage("Priority must be one of: Low, Medium, High, Urgent.");

        RuleFor(request => request.DueDate).ValidDueDate();
        RuleFor(request => request.ProjectId).ValidProjectId();
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(ValidationLimits.TaskTitleMaxLength)
            .WithMessage($"Title cannot exceed {ValidationLimits.TaskTitleMaxLength} characters.");

        RuleFor(request => request.Description)
            .MaximumLength(ValidationLimits.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {ValidationLimits.DescriptionMaxLength} characters.");

        RuleFor(request => request.Status)
            .IsInEnum().WithMessage("Status must be one of: Pending, InProgress, Completed.");

        RuleFor(request => request.Priority)
            .IsInEnum().WithMessage("Priority must be one of: Low, Medium, High, Urgent.");

        RuleFor(request => request.DueDate).ValidDueDate();
        RuleFor(request => request.ProjectId).ValidProjectId();
    }
}

public class TaskQueryParametersValidator : AbstractValidator<TaskQueryParameters>
{
    public const int MaxPageSize = 100;

    public TaskQueryParametersValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Page size must be between 1 and {MaxPageSize}.");

        RuleFor(query => query.Status)
            .IsInEnum().WithMessage("Status must be one of: Pending, InProgress, Completed.");

        RuleFor(query => query.Priority)
            .IsInEnum().WithMessage("Priority must be one of: Low, Medium, High, Urgent.");

        RuleFor(query => query.SortBy)
            .IsInEnum().WithMessage("SortBy must be one of: CreatedAt, DueDate, Priority, Title.");

        RuleFor(query => query.Search)
            .MaximumLength(ValidationLimits.TaskTitleMaxLength)
            .WithMessage($"Search term cannot exceed {ValidationLimits.TaskTitleMaxLength} characters.");

        RuleFor(query => query.ProjectId).ValidProjectId();
    }
}
