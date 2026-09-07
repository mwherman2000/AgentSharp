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
            return GetIntFlexible(prop);
        return defaultValue;
    }

    /// <summary>
    /// Reads a JSON numeric element as an int, tolerating models that emit
    /// whole numbers with a decimal point (e.g. "offset": 5.0). GetInt32() uses
    /// a strict digit-only fast path and throws ("Expected an ASCII digit") on
    /// such values, so we fall back to a double-based parse and truncate.
    /// </summary>
    private static int GetIntFlexible(JsonElement element)
    {
        if (element.TryGetInt32(out var i))
            return i;
        return (int)element.GetDouble();
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

    /// <summary>
    /// A trailing note for a file-tool result when the model passed a *relative*
    /// path: it spells out the absolute location the path resolved to and that a
    /// shell command which cd's into a subdirectory resolves the same relative
    /// path somewhere else. Empty for an already-absolute path. This is the
    /// diagnostic that breaks the "write_file reported success but my cd'd build
    /// script can't find the file, so the write must have silently failed --
    /// let me retry it" loop: the file tools resolve relative paths against the
    /// AgentSharp process working directory, which run_shell's own default
    /// matches, but a `cd` *inside* a run_shell command string does not.
    /// </summary>
    protected static string RelativePathNote(string requestedPath)
    {
        if (Path.IsPathRooted(requestedPath))
            return string.Empty;
        return $"\n(\"{requestedPath}\" was resolved against the working directory " +
               $"{Directory.GetCurrentDirectory()}; a shell command that cd's into a " +
               "subdirectory resolves the same relative path to a different location)";
    }
}
