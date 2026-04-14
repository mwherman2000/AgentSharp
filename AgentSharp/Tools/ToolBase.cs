using System.Text.Json;

namespace AgentSharp.Tools;

/// <summary>
/// Base class for tools. Provides helper methods for building JSON schemas
/// and extracting input parameters.
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract ToolRiskLevel RiskLevel { get; }

    private JsonElement? _cachedSchema;
    public JsonElement InputSchema => _cachedSchema ??= BuildInputSchema();

    public abstract Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default);

    /// <summary>
    /// Override this to define the tool's input schema.
    /// </summary>
    protected abstract JsonElement BuildInputSchema();

    /// <summary>
    /// Helper to create a JSON schema from an anonymous object.
    /// </summary>
    protected static JsonElement SchemaFrom(object schema)
        => JsonSerializer.SerializeToElement(schema);

    /// <summary>
    /// Get a required string parameter from the input.
    /// </summary>
    protected static string GetRequiredString(JsonElement input, string name)
    {
        if (!input.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"Missing required parameter: {name}");
        return prop.GetString()!;
    }

    /// <summary>
    /// Get an optional string parameter from the input.
    /// </summary>
    protected static string? GetOptionalString(JsonElement input, string name)
    {
        if (input.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    /// <summary>
    /// Get an optional integer parameter from the input.
    /// </summary>
    protected static int GetOptionalInt(JsonElement input, string name, int defaultValue)
    {
        if (input.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return defaultValue;
    }

    /// <summary>
    /// Get an optional boolean parameter from the input.
    /// </summary>
    protected static bool GetOptionalBool(JsonElement input, string name, bool defaultValue)
    {
        if (input.TryGetProperty(name, out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
            return prop.GetBoolean();
        return defaultValue;
    }
}
