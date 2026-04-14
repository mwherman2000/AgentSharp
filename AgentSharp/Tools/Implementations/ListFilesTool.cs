using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Lists files and directories at a given path.
/// Supports recursive listing and glob pattern filtering.
/// </summary>
public class ListFilesTool : ToolBase
{
    public override string Name => "list_files";
    public override string Description =>
        "List files and directories at the given path. Use pattern to filter by glob pattern " +
        "(e.g., '*.cs', '**/*.json'). Set recursive to true to search subdirectories.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Directory path to list. Default: current directory" },
            pattern = new { type = "string", description = "Glob pattern to filter files (e.g., '*.cs', '**/*.json'). Default: '*'" },
            recursive = new { type = "boolean", description = "Search subdirectories recursively. Default: false" }
        },
        required = Array.Empty<string>()
    });

    public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = GetOptionalString(input, "path") ?? ".";
        var pattern = GetOptionalString(input, "pattern") ?? "*";
        var recursive = GetOptionalBool(input, "recursive", false);

        path = Path.GetFullPath(path);

        if (!Directory.Exists(path))
            return Task.FromResult(ToolResult.Error($"Directory not found: {path}"));

        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var entries = new List<string>();

            // Directories
            foreach (var dir in Directory.GetDirectories(path, "*", searchOption))
            {
                var relative = Path.GetRelativePath(path, dir);
                if (!ShouldSkip(relative))
                    entries.Add($"[DIR] {relative}/");
            }

            // Files matching pattern
            foreach (var file in Directory.GetFiles(path, pattern, searchOption))
            {
                var relative = Path.GetRelativePath(path, file);
                if (!ShouldSkip(relative))
                {
                    var size = new FileInfo(file).Length;
                    entries.Add($"      {relative} ({FormatSize(size)})");
                }
            }

            if (entries.Count == 0)
                return Task.FromResult(ToolResult.Success($"No files found matching '{pattern}' in {path}"));

            var result = $"Contents of {path}:\n\n{string.Join("\n", entries.Take(500))}";
            if (entries.Count > 500)
                result += $"\n\n[Showing 500 of {entries.Count} entries]";

            return Task.FromResult(ToolResult.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Error listing files: {ex.Message}"));
        }
    }

    private static bool ShouldSkip(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is ".git" or "node_modules" or "bin" or "obj" or ".vs" or ".idea");
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
