namespace TaskFlow.Exceptions;

/// <summary>
/// Thrown by the service layer when a requested resource does not exist.
/// Translated into an HTTP 404 by <see cref="Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
public class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException ForTask(int id) => new($"Task with id {id} was not found.");

    public static NotFoundException ForProject(int id) => new($"Project with id {id} was not found.");
}
