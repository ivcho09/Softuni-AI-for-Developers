using FluentValidation;
using TaskFlow.DTOs.Projects;
using TaskFlow.Models;

namespace TaskFlow.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ValidationLimits.ProjectNameMaxLength)
            .WithMessage($"Name cannot exceed {ValidationLimits.ProjectNameMaxLength} characters.");

        RuleFor(request => request.Description)
            .MaximumLength(ValidationLimits.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {ValidationLimits.DescriptionMaxLength} characters.");
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ValidationLimits.ProjectNameMaxLength)
            .WithMessage($"Name cannot exceed {ValidationLimits.ProjectNameMaxLength} characters.");

        RuleFor(request => request.Description)
            .MaximumLength(ValidationLimits.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {ValidationLimits.DescriptionMaxLength} characters.");
    }
}
