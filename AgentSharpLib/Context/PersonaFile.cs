using System.Text.RegularExpressions;

namespace AgentSharpLib.Context;

/// <summary>
/// Reads a persona file: a header between two <c>---</c> lines, then the prompt.
/// <code>
/// ---
/// name: Andy Pando
/// aliases: a, andrew
/// role: go-to-market (GTM) expert advisor
/// handoff: true
/// order: 10
/// ---
/// You are Andy Pando, ...
/// </code>
/// The key is the file name without its extension. <c>name</c> is required; <c>role</c> defaults to the name,
/// <c>handoff</c> to false and <c>aliases</c> to none. <c>order</c> is optional (see <see cref="OrderUnset"/>).
/// Unknown header lines are ignored.
/// </summary>
public static partial class PersonaFile
{
    /// <summary>Marks a file with no <c>order</c> line: an override keeps the order of the persona it replaces, a new persona sorts last.</summary>
    public const int OrderUnset = int.MinValue;
    internal const int DefaultNewOrder = 1000;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,31}$")]
    private static partial Regex ValidName();

    /// <summary>True if <paramref name="name"/> can be a key or alias: lower case letters, digits, '-' and '_', at most 32 characters.</summary>
    public static bool IsValidName(string name) => ValidName().IsMatch(name);

    /// <summary>
    /// Splits a persona file into its header (the text between the two <c>---</c> lines) and its body (the prompt).
    /// Line endings are normalized to \n and a leading byte-order mark is dropped. False, with the reason in
    /// <paramref name="error"/>, if there is no header or it has no closing line.
    /// </summary>
    public static bool TrySplit(string text, out string header, out string body, out string error)
    {
        header = "";
        body = "";
        error = "";
        text = text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            error = "the file must start with a '---' header line.";
            return false;
        }
        // The closing delimiter is a line of exactly "---".
        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        while (end >= 0 && end + 4 < text.Length && text[end + 4] != '\n')
            end = text.IndexOf("\n---", end + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            error = "the header has no closing '---' line.";
            return false;
        }
        header = end > 4 ? text[4..end] : "";
        body = end + 5 <= text.Length ? text[(end + 5)..] : "";
        return true;
    }

    public static bool TryParse(string key, string text, PersonaSource source, string path, out Persona? persona, out string error)
    {
        persona = null;
        error = "";
        if (!IsValidName(key))
        {
            error = $"'{key}' is not a valid persona name (lower case letters, digits, '-' and '_'; at most 32 characters; the file name without its extension).";
            return false;
        }

        if (!TrySplit(text, out var header, out var body, out error))
            return false;

        string? name = null, role = null;
        var aliases = new List<string>();
        var handoff = false;
        var order = OrderUnset;
        foreach (var line in header.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) { error = $"header line '{line.Trim()}' is not 'key: value'."; return false; }
            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            switch (field)
            {
                case "name": name = value; break;
                case "role": role = value; break;
                case "aliases":
                    foreach (var a in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var alias = a.ToLowerInvariant();
                        if (!IsValidName(alias)) { error = $"alias '{a}' is not a valid name."; return false; }
                        if (alias != key && !aliases.Contains(alias)) aliases.Add(alias);
                    }
                    break;
                case "handoff":
                    if (!bool.TryParse(value, out handoff)) { error = $"handoff must be true or false, not '{value}'."; return false; }
                    break;
                case "order":
                    if (!int.TryParse(value, out order) || order == OrderUnset) { error = $"order must be a whole number, not '{value}'."; return false; }
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(name)) { error = "the header needs a 'name:' line."; return false; }
        if (string.IsNullOrWhiteSpace(body)) { error = "the file has no prompt after the header."; return false; }

        persona = new Persona(key, aliases, name, body, string.IsNullOrWhiteSpace(role) ? name : role,
            handoff, order, source, path);
        return true;
    }
}
