using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskFlow.DTOs.Common;

namespace TaskFlow.Validators;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument before the
/// action executes. Registering this once globally keeps validation calls out of the
/// controllers entirely.
/// </summary>
public class ValidationFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            // Resolve IValidator<TArgument> for whatever type was bound to this parameter.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ErrorResponse
            {
                Status = StatusCodes.Status400BadRequest,
                Message = ValidationErrorFormatter.ValidationFailedMessage,
                Errors = ValidationErrorFormatter.FromFailures(failures),
                TraceId = context.HttpContext.TraceIdentifier
            });
            return;
        }

        await next();
    }
}
