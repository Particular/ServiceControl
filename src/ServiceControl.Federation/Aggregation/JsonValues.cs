namespace ServiceControl.Federation.Aggregation;

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Tolerant readers for the handful of fields the proxy inspects. Member payloads are handled as JSON
/// trees so that fields the proxy does not know about pass through unchanged.
/// </summary>
static class JsonValues
{
    /// <summary>
    /// Reads a dotted path such as <c>receiving_endpoint.name</c>.
    /// </summary>
    public static JsonNode? At(JsonNode? node, string path)
    {
        var current = node;
        foreach (var segment in path.Split('.'))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    public static string? Text(JsonNode? node, string path) => AsText(At(node, path));

    public static string? AsText(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is not JsonValueKind.Null and not JsonValueKind.Object and not JsonValueKind.Array
            ? value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString()
            : null;

    public static double? Number(JsonNode? node, string path) => AsNumber(At(node, path));

    public static double? AsNumber(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        // Parsed values hold a JsonElement, but values created in code hold the CLR type they were
        // created from, which GetValue<double> does not convert. The JSON text works for both.
        var kind = value.GetValueKind();
        string? text = null;
        if (kind == JsonValueKind.Number)
        {
            text = value.ToJsonString();
        }
        else if (kind == JsonValueKind.String)
        {
            text = value.GetValue<string>();
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static bool? Bool(JsonNode? node, string path) =>
        At(node, path) is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>()
            : null;

    public static long? TimeTicks(JsonNode? node, string path) => AsTimeTicks(At(node, path));

    public static long? AsTimeTicks(JsonNode? node) =>
        AsText(node) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time.UtcTicks
            : null;

    public static long? DurationTicks(JsonNode? node, string path) =>
        Text(node, path) is { } text && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var duration)
            ? duration.Ticks
            : null;
}
