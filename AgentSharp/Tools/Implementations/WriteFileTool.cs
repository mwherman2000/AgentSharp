using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Writes content to a file, creating it if it doesn't exist.
/// Creates parent directories as needed.
/// </summary>
public class WriteFileTool : ToolBase
{
    public override string Name => "write_file";
    public override string Description =>
        "Write content to a file at the given path. Creates the file if it doesn't exist, " +
        "or overwrites it if it does. Creates parent directories as needed.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Write;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Absolute or relative path to the file to write" },
            content = new { type = "string", description = "The content to write to the file" }
        },
        required = new[] { "path", "content" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = GetRequiredString(input, "path");
        var content = GetRequiredString(input, "content");

        path = Path.GetFullPath(path);

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content, ct);
            var lines = content.Split('\n').Length;
            return ToolResult.Success($"Successfully wrote {lines} lines to {path}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Error writing file: {ex.Message}");
        }
    }
}
