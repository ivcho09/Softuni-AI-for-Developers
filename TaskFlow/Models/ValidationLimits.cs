namespace TaskFlow.Models;

/// <summary>
/// Field length limits shared by the EF Core model configuration and the
/// FluentValidation validators, so the database constraints and the API
/// validation rules can never drift apart.
/// </summary>
public static class ValidationLimits
{
    public const int TaskTitleMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
    public const int ProjectNameMaxLength = 100;
}
