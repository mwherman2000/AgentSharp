using System.Text.Json;
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
    public async Task<bool> CheckApprovalAsync(ITool tool, string inputSummary, JsonElement input = default)
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

        // Destructive tools: always prompt. For shell commands, run the classifier
        // so the prompt can explain *why* the command is risky instead of just
        // showing the generic "Destructive" risk level.
        var dangerReason = tool.Name == "run_shell" &&
            input.ValueKind == JsonValueKind.Object &&
            input.TryGetProperty("command", out var cmd) &&
            cmd.ValueKind == JsonValueKind.String
                ? _shellClassifier.GetDangerReason(cmd.GetString()!)
                : null;

        return await PromptForApproval(tool, inputSummary, dangerReason);
    }

    /// <summary>
    /// Check if a shell command is dangerous and requires extra approval.
    /// </summary>
    public bool IsShellCommandDangerous(string command) =>
        _shellClassifier.IsDangerous(command);

    private Task<bool> PromptForApproval(ITool tool, string inputSummary, string? dangerReason)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]Approval Required[/]").RuleStyle("yellow"));
        AnsiConsole.MarkupLine($"[yellow]Tool:[/] [bold]{Markup.Escape(tool.Name)}[/]");
        AnsiConsole.MarkupLine($"[yellow]Risk:[/] [red]{tool.RiskLevel}[/]");
        if (dangerReason is not null)
            AnsiConsole.MarkupLine($"[red]Warning:[/] {Markup.Escape(dangerReason)}");
        AnsiConsole.MarkupLine($"[yellow]Action:[/] {Markup.Escape(inputSummary)}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Allow this tool execution?[/] (a = allow, d = deny, s = always allow this session)");

        ConsoleKey key;
        do
        {
            key = Console.ReadKey(intercept: true).Key;
        } while (key != ConsoleKey.A && key != ConsoleKey.D && key != ConsoleKey.S);

        switch (key)
        {
            case ConsoleKey.A:
                AnsiConsole.MarkupLine("[green]  Allowed.[/]");
                return Task.FromResult(true);
            case ConsoleKey.S:
                _alwaysAllowed.Add(tool.Name);
                AnsiConsole.MarkupLine($"[green]  {Markup.Escape(tool.Name)} will be auto-approved for this session.[/]");
                return Task.FromResult(true);
            default:
                AnsiConsole.MarkupLine("[red]  Denied.[/]");
                return Task.FromResult(false);
        }
    }
}
