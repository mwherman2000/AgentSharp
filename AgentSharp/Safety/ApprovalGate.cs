using AgentSharp.Tools;
using Spectre.Console;

namespace AgentSharp.Safety;

/// <summary>
/// Approval response from the user.
/// </summary>
public enum ApprovalResult
{
    Allow,
    Deny,
    AlwaysAllow
}

/// <summary>
/// Manages tool execution approval based on risk levels.
/// Implements the safety gate pattern from Claude Code:
/// - ReadOnly tools: auto-approved
/// - Write tools: auto-approved (logged)
/// - Destructive tools: require explicit user confirmation
///
/// Users can grant "always allow" for specific tools to reduce friction.
/// </summary>
public class ApprovalGate
{
    private readonly HashSet<string> _alwaysAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ShellCommandClassifier _shellClassifier = new();

    /// <summary>
    /// Check if a tool execution requires approval and, if so, prompt the user.
    /// Returns true if the tool should be executed, false if denied.
    /// </summary>
    public async Task<bool> CheckApprovalAsync(ITool tool, string inputSummary)
    {
        // ReadOnly tools are always auto-approved
        if (tool.RiskLevel == ToolRiskLevel.ReadOnly)
            return true;

        // Check if user has granted "always allow" for this tool
        if (_alwaysAllowed.Contains(tool.Name))
            return true;

        // Write tools: auto-approve but log
        if (tool.RiskLevel == ToolRiskLevel.Write)
        {
            AnsiConsole.MarkupLine($"[dim]  [[auto-approved]] {Markup.Escape(tool.Name)}[/]");
            return true;
        }

        // Destructive tools: always prompt
        return await PromptForApproval(tool, inputSummary);
    }

    /// <summary>
    /// Check if a shell command is dangerous and requires extra approval.
    /// </summary>
    public bool IsShellCommandDangerous(string command) =>
        _shellClassifier.IsDangerous(command);

    private Task<bool> PromptForApproval(ITool tool, string inputSummary)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]Approval Required[/]").RuleStyle("yellow"));
        AnsiConsole.MarkupLine($"[yellow]Tool:[/] [bold]{Markup.Escape(tool.Name)}[/]");
        AnsiConsole.MarkupLine($"[yellow]Risk:[/] [red]{tool.RiskLevel}[/]");
        AnsiConsole.MarkupLine($"[yellow]Action:[/] {Markup.Escape(inputSummary)}");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Allow this tool execution?[/]")
                .AddChoices("Allow", "Deny", "Always Allow (this session)"));

        switch (choice)
        {
            case "Allow":
                return Task.FromResult(true);
            case "Always Allow (this session)":
                _alwaysAllowed.Add(tool.Name);
                AnsiConsole.MarkupLine($"[green]  {Markup.Escape(tool.Name)} will be auto-approved for this session.[/]");
                return Task.FromResult(true);
            default:
                AnsiConsole.MarkupLine("[red]  Denied.[/]");
                return Task.FromResult(false);
        }
    }
}
