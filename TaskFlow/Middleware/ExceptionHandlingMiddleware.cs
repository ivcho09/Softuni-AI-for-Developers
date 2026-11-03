using System.Text.Json;
using FluentValidation;
using TaskFlow.DTOs.Common;
using TaskFlow.Exceptions;
using TaskFlow.Validators;

namespace TaskFlow.Middleware;

/// <summary>
/// Centralised exception handling. Controllers and services simply throw; this is the
/// only place that maps a failure onto an HTTP status code and an <see cref="ErrorResponse"/>,
/// which is why there is not a single try/catch in the controllers.
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException exception)
        {
            logger.LogInformation("Resource not found: {Message}", exception.Message);
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, exception.Message);
        }
        catch (ValidationException exception)
        {
            logger.LogInformation("Validation failed: {Message}", exception.Message);
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                ValidationErrorFormatter.ValidationFailedMessage,
                ValidationErrorFormatter.FromFailures(exception.Errors));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is nobody left to send a response to.
            logger.LogDebug("Request was aborted by the client.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while processing the request.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message,
        IDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            // Headers are already on the wire, so the status code can no longer be changed.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = new ErrorResponse
        {
            Status = statusCode,
            Message = message,
            Errors = errors,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, SerializerOptions));
    }
}
