using System.Text.Json.Serialization;

namespace TaskFlow.DTOs.Common;

/// <summary>
/// The single error shape returned by every failing request.
/// </summary>
public record ErrorResponse
{
    /// <summary>The HTTP status code of the response.</summary>
    /// <example>400</example>
    public int Status { get; init; }

    /// <summary>A human readable summary of what went wrong.</summary>
    /// <example>One or more validation errors occurred.</example>
    public string Message { get; init; } = string.Empty;

    /// <summary>Field level validation messages, keyed by property name. Omitted when not applicable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Correlation id for the request, useful when reporting a server error.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }
}
