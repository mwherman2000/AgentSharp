namespace AgentSharp.Tools;

/// <summary>
/// Risk classification for tools. Determines whether user approval is required.
/// Modeled after the three-tier system found in Claude Code and its reimplementations.
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>
    /// No side effects. Auto-approved, no user prompt.
    /// Examples: read_file, list_files, grep, think
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Creates or modifies files. Logged, optionally prompts user.
    /// Examples: write_file, edit_file
    /// </summary>
    Write,

    /// <summary>
    /// Shell execution, deletions, network access. Always requires user confirmation.
    /// Examples: run_shell, delete operations
    /// </summary>
    Destructive
}
