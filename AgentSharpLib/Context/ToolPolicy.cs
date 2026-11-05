namespace AgentSharpLib.Context;

/// <summary>
/// Which tools a persona may use, from the <c>tools:</c> and <c>deny:</c> lines of its file. A policy only ever
/// narrows what a persona gets: with neither line the persona has every tool the session has.
/// <para><see cref="Allow"/> is the allow-list (null: every tool); <see cref="Deny"/> is removed from whatever
/// that leaves, and wins when a tool is in both. The <c>handoff</c> tool is never restricted, so a persona that is
/// given a narrow set can still hand the conversation on or back.</para>
/// </summary>
public sealed record ToolPolicy(IReadOnlyList<string>? Allow, IReadOnlyList<string> Deny)
{
    /// <summary>Tools no policy can remove, so a restricted persona can always give the conversation back.</summary>
    public static readonly IReadOnlyList<string> AlwaysAvailable = ["handoff"];

    /// <summary>No restriction.</summary>
    public static readonly ToolPolicy Unrestricted = new(null, []);

    /// <summary>True if the policy removes nothing.</summary>
    public bool IsUnrestricted => Allow is null && Deny.Count == 0;

    /// <summary>True if a persona with this policy may use the tool called <paramref name="toolName"/>.</summary>
    public bool Permits(string toolName)
    {
        if (AlwaysAvailable.Contains(toolName, StringComparer.OrdinalIgnoreCase)) return true;
        if (Deny.Contains(toolName, StringComparer.OrdinalIgnoreCase)) return false;
        return Allow is null || Allow.Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>One line for people: "only read_file, grep", "all except run_shell", "no tools".</summary>
    public string Describe()
    {
        if (IsUnrestricted) return "all tools";
        var parts = new List<string>();
        if (Allow is not null)
            parts.Add(Allow.Count == 0 ? "no tools" : "only " + string.Join(", ", Allow));
        if (Deny.Count > 0)
            parts.Add((Allow is null ? "all except " : "not ") + string.Join(", ", Deny));
        return string.Join("; ", parts);
    }
}
