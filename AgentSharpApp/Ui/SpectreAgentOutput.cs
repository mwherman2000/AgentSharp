using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Output;
using Spectre.Console;

namespace AgentSharpApp.Ui;

/// <summary>
/// Renders the library's <see cref="IAgentOutput"/> events to the terminal with
/// Spectre.Console -- the colors and layout the CLI has always used.
/// </summary>
public sealed class SpectreAgentOutput : IAgentOutput
{
    /// <summary>
    /// AnsiConsole.Write(string) forwards to the composite-format overload, which
    /// treats the text as a format string and throws FormatException the moment it
    /// contains a brace (e.g. code deltas) -- Text() writes the content literally
    /// instead. Embedding a raw '\n' inside a single Text segment also doesn't
    /// reliably move the cursor to column 0 under VT processing, so this splits on
    /// newlines and emits each break via AnsiConsole.WriteLine explicitly.
    /// </summary>
    public void Text(string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) AnsiConsole.WriteLine();
            if (lines[i].Length > 0) AnsiConsole.Write(new Text(lines[i]));
        }
    }

    public void LineBreak() => AnsiConsole.WriteLine();

    public void Trace(string text) => Console.WriteLine(text);

    public void Info(string message) =>
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(message)}[/]");

    public void Warning(string message) =>
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");

    public void Error(string message) =>
        AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(message)}");

    public void TransientError(Exception ex)
    {
        AnsiConsole.MarkupLine($"\n[red]Error:[/] [dim]{Markup.Escape(ex.GetType().FullName ?? ex.GetType().Name)}: {Markup.Escape(ex.Message)}[/]");
        if (ex.InnerException is { } innerEx)
            AnsiConsole.MarkupLine($"[dim]  Inner: {Markup.Escape(innerEx.GetType().FullName ?? innerEx.GetType().Name)}: {Markup.Escape(innerEx.Message)}[/]");
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");
    }

    public void ToolAutoApproved(string toolName) =>
        AnsiConsole.MarkupLine($"[dim]  [[auto-approved]] {Markup.Escape(toolName)}[/]");

    public void ToolSkipped(string toolTypeName, string reason) =>
        AnsiConsole.MarkupLine($"[dim yellow]Skipped tool '{Markup.Escape(toolTypeName)}': {Markup.Escape(reason)}[/]");

    public void SubAgentStarted(string name, string task, bool inBatch)
    {
        if (inBatch)
        {
            AnsiConsole.MarkupLine($"  [dim]Starting: {Markup.Escape(name)}[/]");
            return;
        }
        AnsiConsole.MarkupLine($"[cyan]Spawning sub-agent:[/] [bold]{Markup.Escape(name)}[/]");
        AnsiConsole.MarkupLine($"[dim]Task: {Markup.Escape(Truncate(task, 100))}[/]");
    }

    public void SubAgentFinished(string name, SubAgentStatus status, bool inBatch)
    {
        var ok = status == SubAgentStatus.Completed;
        AnsiConsole.MarkupLine((inBatch, ok) switch
        {
            (true, true) => $"  [green]Done: {Markup.Escape(name)}[/]",
            (true, false) => $"  [red]Failed: {Markup.Escape(name)}[/]",
            (false, true) => $"[green]Sub-agent '{Markup.Escape(name)}' completed.[/]",
            (false, false) => $"[red]Sub-agent '{Markup.Escape(name)}' {status}.[/]",
        });
    }

    public void SubAgentBatchStarted(int count, int maxConcurrent) =>
        AnsiConsole.MarkupLine($"[cyan]Spawning {count} sub-agents (max {maxConcurrent} running at once)...[/]");

    public void SubAgentBatchFinished(int count) =>
        AnsiConsole.MarkupLine($"[cyan]All {count} sub-agents finished.[/]");

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";
}
