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

        // Write to a unique temp file and rename into place -- matching
        // SessionManager.SaveAsync's own pattern -- so a cancellation mid-write or a
        // process crash never leaves the target path truncated/partially written.
        // Writing directly to `path` would otherwise silently clobber whatever good
        // content was there before with no warning that it happened.
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
            var lines = content.Split('\n').Length;
            return ToolResult.Success($"Successfully wrote {lines} lines to {path}");
        }
        catch (OperationCanceledException)
        {
            // Let cancellation (e.g. Ctrl+C) propagate to ToolRegistry/AgentLoop
            // instead of being reported as a misleading "error writing file" --
            // the generic catch below would otherwise catch this too, since
            // OperationCanceledException derives from Exception.
            try { File.Delete(tempPath); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            try { File.Delete(tempPath); } catch { }
            return ToolResult.Error($"Error writing file: {ex.Message}");
        }
    }
}
