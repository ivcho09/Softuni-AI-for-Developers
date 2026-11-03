using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskFlow.Validators;

/// <summary>
/// Serialises enums as their names and, crucially, rejects unknown names with a message
/// that lists the supported values. The built-in converter reports a failure that names
/// the internal .NET type, which is not something an API client should ever see.
/// </summary>
public class StrictEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var name = reader.GetString();
                if (Enum.TryParse<TEnum>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                {
                    return parsed;
                }

                throw new JsonException(BuildMessage(name));

            case JsonTokenType.Number when reader.TryGetInt32(out var number):
                var fromNumber = (TEnum)Enum.ToObject(typeof(TEnum), number);
                if (Enum.IsDefined(fromNumber))
                {
                    return fromNumber;
                }

                throw new JsonException(BuildMessage(number.ToString()));

            default:
                throw new JsonException(BuildMessage(null));
        }
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string BuildMessage(string? suppliedValue)
    {
        var allowed = string.Join(", ", Enum.GetNames<TEnum>());
        return suppliedValue is null
            ? $"A value is required. Allowed values: {allowed}."
            : $"'{suppliedValue}' is not a supported value. Allowed values: {allowed}.";
    }
}

/// <summary>Applies <see cref="StrictEnumConverter{TEnum}"/> to every enum in the API surface.</summary>
public class StrictEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;
}
