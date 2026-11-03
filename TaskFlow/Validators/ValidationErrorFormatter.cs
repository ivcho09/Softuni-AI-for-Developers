using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskFlow.Validators;

/// <summary>
/// Turns the three different sources of validation failure (FluentValidation rules,
/// model binding/JSON errors, and service-level rules) into one error dictionary,
/// so every 400 response from the API has the same shape.
/// </summary>
public static class ValidationErrorFormatter
{
    public const string ValidationFailedMessage = "One or more validation errors occurred.";

    public static IDictionary<string, string[]> FromFailures(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(failure => NormaliseKey(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

    public static IDictionary<string, string[]> FromModelState(ModelStateDictionary modelState)
    {
        var entries = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToList();

        // When the JSON body itself cannot be parsed, MVC records both the parse error
        // (keyed by JSON path, e.g. "$.status") and a synthetic "the parameter is
        // required" error for the unbound action argument. Only the parse error is useful.
        if (entries.Any(entry => entry.Key.StartsWith('$')))
        {
            entries = entries.Where(entry => entry.Key.StartsWith('$')).ToList();
        }

        return entries.ToDictionary(
            entry => NormaliseKey(entry.Key),
            entry => entry.Value!.Errors
                .Select(error => Sanitise(error.ErrorMessage))
                .Distinct()
                .ToArray());
    }

    /// <summary>
    /// Maps a property name or JSON path onto the camelCase field name the client sent,
    /// so "$.status" and "Status" both come back as "status".
    /// </summary>
    private static string NormaliseKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "request";
        }

        var trimmed = key.TrimStart('$', '.');
        return string.IsNullOrEmpty(trimmed)
            ? "request"
            : JsonNamingPolicy.CamelCase.ConvertName(trimmed);
    }

    /// <summary>
    /// Drops the "Path: $.status | LineNumber: 0 | BytePositionInLine: 38" tail that
    /// System.Text.Json appends to converter messages, and replaces an empty message.
    /// </summary>
    private static string Sanitise(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "The supplied value is not valid for this field.";
        }

        var pathIndex = message.IndexOf(" Path:", StringComparison.Ordinal);
        return pathIndex < 0 ? message : message[..pathIndex].TrimEnd();
    }
}
