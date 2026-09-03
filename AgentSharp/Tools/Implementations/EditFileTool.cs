using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Performs exact string replacement in a file.
/// This is the diff/patch style editing tool inspired by Claude Code's Edit tool.
/// The old_string must match exactly (including whitespace/indentation).
/// </summary>
public class EditFileTool : ToolBase
{
    public override string Name => "edit_file";
    public override string Description =>
        "Perform an exact string replacement in a file. Provide the exact text to find (old_string) " +
        "and the text to replace it with (new_string). The old_string must match exactly, including " +
        "whitespace and indentation. The edit will fail if old_string is not found or matches multiple locations.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Write;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Path to the file to edit" },
            old_string = new { type = "string", description = "The exact text to find and replace. Must be unique in the file." },
            new_string = new { type = "string", description = "The text to replace old_string with" }
        },
        required = new[] { "path", "old_string", "new_string" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = GetRequiredString(input, "path");
        var oldString = GetRequiredString(input, "old_string");
        var newString = GetRequiredString(input, "new_string");

        path = Path.GetFullPath(path);

        if (!File.Exists(path))
            return ToolResult.Error($"File not found: {path}");

        // An empty old_string makes CountOccurrences loop forever: IndexOf("", index)
        // always returns index unchanged, so the scan position never advances. Reject
        // it up front rather than hanging -- GetRequiredString only rejects a missing
        // property, not an empty string, so this can genuinely reach here.
        if (oldString.Length == 0)
            return ToolResult.Error("old_string cannot be empty.");

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);

            // Count occurrences
            var count = CountOccurrences(content, oldString);

            if (count == 0)
                return ToolResult.Error(
                    "old_string not found in file. Make sure the text matches exactly, " +
                    "including whitespace and indentation. Use read_file to check the current content.");

            if (count > 1)
                return ToolResult.Error(
                    $"old_string found {count} times in the file. It must be unique. " +
                    "Provide more surrounding context to make it unique.");

            var newContent = content.Replace(oldString, newString);
            await File.WriteAllTextAsync(path, newContent, ct);

            return ToolResult.Success($"Successfully edited {path}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Error editing file: {ex.Message}");
        }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
