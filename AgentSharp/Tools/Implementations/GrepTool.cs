using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Searches file contents for a pattern (regex supported).
/// Returns matching lines with file paths and line numbers.
/// </summary>
public class GrepTool : ToolBase
{
    public override string Name => "grep";
    public override string Description =>
        "Search file contents for a pattern (supports regex). Returns matching lines with file paths " +
        "and line numbers. Use file_pattern to filter which files to search (e.g., '*.cs').";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            pattern = new { type = "string", description = "Regex pattern to search for" },
            path = new { type = "string", description = "Directory to search in. Default: current directory" },
            file_pattern = new { type = "string", description = "Glob pattern for files to search (e.g., '*.cs', '*.json'). Default: '*'" },
            case_sensitive = new { type = "boolean", description = "Case-sensitive search. Default: true" },
            context_lines = new { type = "integer", description = "Number of context lines to show around matches. Default: 0" }
        },
        required = new[] { "pattern" }
    });

    public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var pattern = GetRequiredString(input, "pattern");
        var searchPath = GetOptionalString(input, "path") ?? ".";
        var filePattern = GetOptionalString(input, "file_pattern") ?? "*";
        var caseSensitive = GetOptionalBool(input, "case_sensitive", true);
        var contextLines = GetOptionalInt(input, "context_lines", 0);

        searchPath = Path.GetFullPath(searchPath);

        if (!Directory.Exists(searchPath))
            return Task.FromResult(ToolResult.Error($"Directory not found: {searchPath}"));

        try
        {
            var regexOptions = RegexOptions.Compiled;
            if (!caseSensitive)
                regexOptions |= RegexOptions.IgnoreCase;

            var regex = new Regex(pattern, regexOptions);
            var results = new List<string>();
            int totalMatches = 0;
            int filesSearched = 0;
            int filesMatched = 0;
            const int maxResults = 200;

            foreach (var file in Directory.EnumerateFiles(searchPath, filePattern, SearchOption.AllDirectories))
            {
                if (ShouldSkip(file, searchPath))
                    continue;

                if (IsBinaryFile(file))
                    continue;

                filesSearched++;

                try
                {
                    var lines = File.ReadAllLines(file);
                    var fileHasMatch = false;
                    var relativePath = Path.GetRelativePath(searchPath, file);

                    for (int i = 0; i < lines.Length && totalMatches < maxResults; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            if (!fileHasMatch)
                            {
                                results.Add($"\n{relativePath}:");
                                fileHasMatch = true;
                                filesMatched++;
                            }

                            // Context before
                            for (int j = Math.Max(0, i - contextLines); j < i; j++)
                                results.Add($"  {j + 1}  {lines[j]}");

                            // Match line
                            results.Add($"  {i + 1}: {lines[i]}");

                            // Context after
                            for (int j = i + 1; j <= Math.Min(lines.Length - 1, i + contextLines); j++)
                                results.Add($"  {j + 1}  {lines[j]}");

                            totalMatches++;
                        }
                    }
                }
                catch
                {
                    // Skip files we can't read (binary, locked, etc.)
                }

                if (totalMatches >= maxResults)
                    break;
            }

            if (totalMatches == 0)
                return Task.FromResult(ToolResult.Success(
                    $"No matches found for '{pattern}' in {filesSearched} files."));

            var summary = $"Found {totalMatches} matches in {filesMatched} files (searched {filesSearched} files):";
            if (totalMatches >= maxResults)
                summary += " [truncated at 200 results]";

            return Task.FromResult(ToolResult.Success(summary + "\n" + string.Join("\n", results)));
        }
        catch (RegexParseException)
        {
            return Task.FromResult(ToolResult.Error($"Invalid regex pattern: {pattern}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Error searching: {ex.Message}"));
        }
    }

    private static bool ShouldSkip(string filePath, string basePath)
    {
        var relative = Path.GetRelativePath(basePath, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is ".git" or "node_modules" or "bin" or "obj" or ".vs" or ".idea");
    }

    private static bool IsBinaryFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".exe" or ".dll" or ".pdb" or ".zip" or ".tar" or ".gz"
            or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico"
            or ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx"
            or ".mp3" or ".mp4" or ".avi" or ".mov"
            or ".woff" or ".woff2" or ".ttf" or ".eot";
    }
}
