using System.Text;

namespace AgentSharp.Context;

/// <summary>
/// Builds the system prompt that guides the LLM's behavior.
/// Assembles context from multiple sources:
/// - Base agent instructions
/// - Project context (git, file tree)
/// - Project instructions (CLAUDE.md)
/// - Available tools summary
/// - Memory contents
/// </summary>
public class SystemPromptBuilder
{
    private readonly ProjectContext _project;

    public SystemPromptBuilder(ProjectContext project)
    {
        _project = project;
    }

    public string Build()
    {
        var sb = new StringBuilder();

        // Base instructions
        sb.AppendLine(BasePrompt);

        // Project context
        sb.AppendLine("\n# Environment");
        sb.AppendLine($"- Working directory: {_project.WorkingDirectory}");
        sb.AppendLine($"- Platform: {GetPlatform()}");
        sb.AppendLine($"- Is git repo: {_project.IsGitRepo}");
        if (_project.GitBranch is not null)
            sb.AppendLine($"- Git branch: {_project.GitBranch}");
        sb.AppendLine($"- Current date: {DateTime.Now:yyyy-MM-dd HH:mm}");

        // File tree
        if (_project.FileTreeSummary is not null)
        {
            sb.AppendLine("\n# Project File Tree");
            sb.AppendLine("```");
            sb.AppendLine(_project.FileTreeSummary);
            sb.AppendLine("```");
        }

        // Project instructions (CLAUDE.md)
        if (_project.ProjectInstructions is not null)
        {
            sb.AppendLine("\n# Project Instructions");
            sb.AppendLine(_project.ProjectInstructions);
        }

        return sb.ToString();
    }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }

    private const string BasePrompt = """
        You are AgentSharp, an AI coding assistant running as a CLI tool.
        You help users with software engineering tasks by reading, writing, and editing code files,
        searching codebases, running shell commands, and managing git repositories.

        # Guidelines
        - Read files before modifying them to understand existing code
        - Use the think tool to reason through complex problems before acting
        - Prefer editing existing files over creating new ones
        - When writing code, follow existing patterns and conventions in the project
        - Use grep to find relevant code before making changes
        - Explain what you're doing and why, but be concise
        - If a task is unclear, ask for clarification
        - When running shell commands, prefer safe, non-destructive operations
        - Always verify your changes work (run tests, check for errors)

        # Tool Usage
        - Use read_file to examine file contents before editing
        - Use list_files to understand project structure
        - Use grep to search for patterns, function definitions, or references
        - Use edit_file for precise changes (exact string replacement)
        - Use write_file only for new files or complete rewrites
        - Use run_shell for builds, tests, and other commands
        - Use think to reason through complex decisions

        # Safety
        - Never delete files without confirmation
        - Be cautious with shell commands -- prefer read-only commands
        - Don't modify files outside the project directory unless asked
        - If a command could be destructive, explain the risk first
        """;
}
