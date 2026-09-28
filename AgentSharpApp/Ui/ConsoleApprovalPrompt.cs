using AgentSharpLib.Safety;
using Spectre.Console;

namespace AgentSharpApp.Ui;

/// <summary>
/// Asks the person at the terminal to approve a Destructive tool call with a
/// single a/d/s keypress. <see cref="ApprovalGate"/> serializes calls, so two
/// concurrent sub-agents never share one prompt or one keypress.
/// </summary>
public sealed class ConsoleApprovalPrompt : IApprovalPrompt
{
    public async Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
    {
        // No interactive console to answer with a/d/s (one-shot or autonomous run,
        // or stdin redirected): the KeyAvailable poll below would otherwise either
        // throw InvalidOperationException or spin forever on a keypress that can
        // never arrive. Deny cleanly instead: a denied result feeds back to the
        // model as an ordinary tool error it can adapt to, and the user can re-run
        // interactively or grant "always allow" up front.
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Auto-denied[/] [bold]{Markup.Escape(request.ToolName)}[/] " +
                $"[dim]-- {Markup.Escape(request.RiskLevel.ToString())} tool needs approval but no interactive console is attached.[/]");
            return ApprovalResult.Deny;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]Approval Required[/]").RuleStyle("yellow"));
        AnsiConsole.MarkupLine($"[yellow]Tool:[/] [bold]{Markup.Escape(request.ToolName)}[/]");
        AnsiConsole.MarkupLine($"[yellow]Risk:[/] [red]{request.RiskLevel}[/]");
        if (request.DangerReason is not null)
            AnsiConsole.MarkupLine($"[red]Warning:[/] {Markup.Escape(request.DangerReason)}");
        AnsiConsole.MarkupLine($"[yellow]Action:[/] {Markup.Escape(request.InputSummary)}");
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
            bool keyAvailable;
            try
            {
                keyAvailable = Console.KeyAvailable;
            }
            catch (InvalidOperationException)
            {
                // The console became unreadable after the IsInputRedirected
                // check above (e.g. stdin closed mid-run). Treat it the same
                // way -- deny rather than throw out of the approval gate.
                AnsiConsole.MarkupLine("[yellow]  Denied -- console input is no longer available.[/]");
                return ApprovalResult.Deny;
            }

            if (keyAvailable)
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
                return ApprovalResult.Allow;
            case ConsoleKey.S:
                AnsiConsole.MarkupLine($"[green]  {Markup.Escape(request.ToolName)} will be auto-approved for this session.[/]");
                return ApprovalResult.AlwaysAllow;
            default:
                AnsiConsole.MarkupLine("[red]  Denied.[/]");
                return ApprovalResult.Deny;
        }
    }
}
