using System.Collections.Concurrent;
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
    // ConcurrentDictionary used as a thread-safe set (value is unused) -- one
    // ApprovalGate is shared by every concurrent sub-agent (AgentOrchestrator hands
    // the same instance to each SubAgent), so two agents granting/checking "always
    // allow" for the same or different tools at the same time must not race on a
    // plain HashSet.
    private readonly ConcurrentDictionary<string, byte> _alwaysAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ShellCommandClassifier _shellClassifier = new();

    // Guards only the interactive prompt itself (console rendering + Console.ReadKey),
    // not the whole approval check -- so a call that's auto-approved or already in
    // _alwaysAllowed never blocks behind another agent's unrelated live prompt.
    // Without this, two concurrent sub-agents both hitting a Destructive tool at
    // nearly the same time could interleave their prompts on the same console and
    // both read from the same Console.ReadKey call, so a user pressing 'a' for what's
    // on screen for one agent could actually approve a completely different pending
    // command from the other.
    private readonly SemaphoreSlim _promptLock = new(1, 1);

    /// <summary>
    /// Check if a tool execution requires approval and, if so, prompt the user.
    /// Returns true if the tool should be executed, false if denied.
    /// </summary>
    public async Task<bool> CheckApprovalAsync(ITool tool, string inputSummary, JsonElement input = default, CancellationToken ct = default)
    {
        // ReadOnly tools are always auto-approved
        if (tool.RiskLevel == ToolRiskLevel.ReadOnly)
            return true;

        // Check if user has granted "always allow" for this tool
        if (_alwaysAllowed.ContainsKey(tool.Name))
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

        return await PromptForApproval(tool, inputSummary, dangerReason, ct);
    }

    /// <summary>
    /// Check if a shell command is dangerous and requires extra approval.
    /// </summary>
    public bool IsShellCommandDangerous(string command) =>
        _shellClassifier.IsDangerous(command);

    private async Task<bool> PromptForApproval(ITool tool, string inputSummary, string? dangerReason, CancellationToken ct)
    {
        await _promptLock.WaitAsync();
        try
        {
            // Another concurrent agent may have just been granted "always allow" for
            // this same tool while this call was waiting for the lock -- re-check so
            // we don't prompt redundantly for something already approved.
            if (_alwaysAllowed.ContainsKey(tool.Name))
                return true;

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[yellow]Approval Required[/]").RuleStyle("yellow"));
            AnsiConsole.MarkupLine($"[yellow]Tool:[/] [bold]{Markup.Escape(tool.Name)}[/]");
            AnsiConsole.MarkupLine($"[yellow]Risk:[/] [red]{tool.RiskLevel}[/]");
            if (dangerReason is not null)
                AnsiConsole.MarkupLine($"[red]Warning:[/] {Markup.Escape(dangerReason)}");
            AnsiConsole.MarkupLine($"[yellow]Action:[/] {Markup.Escape(inputSummary)}");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[yellow]Allow this tool execution?[/] (a = allow, d = deny, s = always allow this session)");

            // Console.ReadKey has no cancellable overload, so a plain blocking call
            // here would swallow Ctrl+C: OnCancelKeyPress cancels the turn's token,
            // but that has nothing to interrupt a synchronous ReadKey, leaving the
            // prompt stuck until an actual a/d/s keypress. Polling KeyAvailable lets
            // us observe cancellation between polls instead.
            ConsoleKey key;
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    key = Console.ReadKey(intercept: true).Key;
                    if (key == ConsoleKey.A || key == ConsoleKey.D || key == ConsoleKey.S)
                        break;
                }
                else
                {
                    await Task.Delay(50, ct);
                }
            }

            switch (key)
            {
                case ConsoleKey.A:
                    AnsiConsole.MarkupLine("[green]  Allowed.[/]");
                    return true;
                case ConsoleKey.S:
                    _alwaysAllowed[tool.Name] = 0;
                    AnsiConsole.MarkupLine($"[green]  {Markup.Escape(tool.Name)} will be auto-approved for this session.[/]");
                    return true;
                default:
                    AnsiConsole.MarkupLine("[red]  Denied.[/]");
                    return false;
            }
        }
        finally
        {
            _promptLock.Release();
        }
    }
}
