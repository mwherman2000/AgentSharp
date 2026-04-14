using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Reads the contents of a file and returns it with line numbers.
/// Supports reading partial files with offset and limit parameters.
/// </summary>
public class ReadFileTool : ToolBase
{
    public override string Name => "read_file";
    public override string Description =>
        "Read the contents of a file at the given path. Returns the file contents with line numbers. " +
        "Use offset and limit to read specific portions of large files.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Absolute or relative path to the file to read" },
            offset = new { type = "integer", description = "Line number to start reading from (0-based). Default: 0" },
            limit = new { type = "integer", description = "Maximum number of lines to read. Default: 2000" }
        },
        required = new[] { "path" }
    });

    public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = GetRequiredString(input, "path");
        var offset = GetOptionalInt(input, "offset", 0);
        var limit = GetOptionalInt(input, "limit", 2000);

        path = Path.GetFullPath(path);

        if (!File.Exists(path))
            return Task.FromResult(ToolResult.Error($"File not found: {path}"));

        try
        {
            var lines = File.ReadLines(path)
                .Skip(offset)
                .Take(limit)
                .Select((line, i) => $"{offset + i + 1}\t{line}");

            var content = string.Join(Environment.NewLine, lines);
            var totalLines = File.ReadLines(path).Count();

            var result = content;
            if (offset + limit < totalLines)
                result += $"\n\n[Showing lines {offset + 1}-{Math.Min(offset + limit, totalLines)} of {totalLines} total]";

            return Task.FromResult(ToolResult.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Error reading file: {ex.Message}"));
        }
    }
}
