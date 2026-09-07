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

        // Directory.GetFiles has no concept of "**" meaning "any depth" -- it only
        // supports */? wildcards within a single filename component, and a literal
        // '/' in the pattern never matches a real filename. Without this, the
        // "**/*.json" example this tool's own description/schema advertise would
        // silently return "No files found" even when matching files exist deeper in
        // the tree -- indistinguishable from a genuinely empty result, with nothing
        // signaling that the pattern itself was the problem. "**/" specifically
        // means "at any depth", so treat it as an explicit recursive request too.
        if (pattern.StartsWith("**/", StringComparison.Ordinal))
        {
            pattern = pattern[3..];
            recursive = true;
        }

        path = Path.GetFullPath(path);

        if (!Directory.Exists(path))
            return Task.FromResult(ToolResult.Error($"Directory not found: {path}"));

        try
        {
            // EnumerationOptions rather than the SearchOption overload: a single
            // unreadable directory deep in a recursive walk otherwise throws
            // UnauthorizedAccessException from Directory.GetDirectories/GetFiles and
            // aborts the entire listing, returning only an error instead of the
            // files that *are* readable. IgnoreInaccessible (on by default here)
            // skips the offending entry and keeps walking. AttributesToSkip is
            // cleared so hidden/system files still show, matching the old behavior.
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            };
            var entries = new List<string>();

            // Directories
            foreach (var dir in Directory.GetDirectories(path, "*", enumOptions))
            {
                if (PathFilter.ShouldSkip(dir, path))
                    continue;
                entries.Add($"[DIR] {Path.GetRelativePath(path, dir)}/");
            }

            // Files matching pattern
            foreach (var file in Directory.GetFiles(path, pattern, enumOptions))
            {
                if (PathFilter.ShouldSkip(file, path))
                    continue;
                var size = new FileInfo(file).Length;
                entries.Add($"      {Path.GetRelativePath(path, file)} ({FormatSize(size)})");
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

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
