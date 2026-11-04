using System.Text.Json;

namespace AgentSharpLib.Context;

/// <summary>
/// Works out whether a <c>write_file</c> or <c>edit_file</c> call on a persona file would change its header (the name,
/// aliases, role, handoff flag or order) or only its prompt. <see cref="Safety.ApprovalGate"/> uses it: a user who
/// allowed edits to one persona file for the session still gets asked when an edit touches the header, because the
/// header decides, for example, whether the model can hand the conversation to the persona.
/// </summary>
public static class PersonaFileEdit
{
    /// <summary>
    /// True if the call would change the header, or if that cannot be told (the file is missing or has no valid
    /// header, the new text has none, the tool is not a file writer, or an edit's old_string is missing or not
    /// unique). False only when the header is certainly unchanged, so the answer errs towards asking.
    /// </summary>
    public static bool ChangesHeader(string toolName, string fullPath, JsonElement input)
    {
        string current;
        try
        {
            if (!File.Exists(fullPath)) return true;
            current = File.ReadAllText(fullPath);
        }
        catch (Exception)
        {
            return true;
        }
        if (!PersonaFile.TrySplit(current, out var oldHeader, out _, out _))
            return true;

        var updated = toolName.ToLowerInvariant() switch
        {
            "write_file" => Str(input, "content"),
            "edit_file" => ApplyEdit(current, Str(input, "old_string"), Str(input, "new_string")),
            _ => null,
        };
        if (updated is null || !PersonaFile.TrySplit(updated, out var newHeader, out _, out _))
            return true;

        return !string.Equals(oldHeader, newHeader, StringComparison.Ordinal);
    }

    private static string? Str(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>The text after replacing the one occurrence of old with new, or null if old is empty or does not occur exactly once.</summary>
    private static string? ApplyEdit(string current, string? oldText, string? newText)
    {
        if (string.IsNullOrEmpty(oldText) || newText is null) return null;
        static string Norm(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');
        var text = Norm(current);
        var from = Norm(oldText);
        var first = text.IndexOf(from, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(from, first + from.Length, StringComparison.Ordinal) >= 0)
            return null;
        return text[..first] + Norm(newText) + text[(first + from.Length)..];
    }
}
