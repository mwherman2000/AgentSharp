using System.Collections.Concurrent;
using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Tools;
using Spectre.Console;

namespace AgentSharp.Agent.MultiAgent;

/// <summary>
/// Orchestrates multiple sub-agents working on delegated tasks.
/// Inspired by Claude Code's multi-agent architecture:
///
/// - Parent agent decides what to delegate
/// - Orchestrator spawns sub-agents with isolated contexts
/// - Sub-agents run concurrently or sequentially
/// - Results are collected and returned to the parent
///
/// Three execution modes (from Claude Code):
/// 1. Fork: Independent sub-agent, fire-and-forget
/// 2. Sequential: Tasks run one after another
/// 3. Parallel: Tasks run concurrently, results collected
/// </summary>
public class AgentOrchestrator
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _tools;
    private readonly ApprovalGate _approval;
    private readonly string _systemPrompt;
    private readonly ConcurrentDictionary<string, SubAgent> _agents = new();

    public IReadOnlyCollection<SubAgent> ActiveAgents => _agents.Values.ToList();

    public AgentOrchestrator(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Spawn a single sub-agent to work on a task.
    /// Returns immediately with the sub-agent reference.
    /// </summary>
    public SubAgent Spawn(string name, string task)
    {
        var agent = new SubAgent(name, task, _llm, _tools, _approval, _systemPrompt);
        _agents[agent.Id] = agent;
        return agent;
    }

    /// <summary>
    /// Run a single sub-agent and wait for its result.
    /// </summary>
    public async Task<string> RunSingleAsync(string name, string task, CancellationToken ct = default)
    {
        var agent = Spawn(name, task);

        AnsiConsole.MarkupLine($"[cyan]Spawning sub-agent:[/] [bold]{Markup.Escape(name)}[/]");
        AnsiConsole.MarkupLine($"[dim]Task: {Markup.Escape(Truncate(task, 100))}[/]");

        var result = await agent.RunAsync(task, ct);

        AnsiConsole.MarkupLine(agent.Status == SubAgentStatus.Completed
            ? $"[green]Sub-agent '{Markup.Escape(name)}' completed.[/]"
            : $"[red]Sub-agent '{Markup.Escape(name)}' {agent.Status}.[/]");

        return result;
    }

    /// <summary>
    /// Run multiple sub-agents in parallel and collect all results.
    /// Each task is a (name, task) tuple.
    /// </summary>
    public async Task<IReadOnlyList<SubAgentResult>> RunParallelAsync(
        IEnumerable<(string name, string task)> tasks,
        CancellationToken ct = default)
    {
        var taskList = tasks.ToList();

        AnsiConsole.MarkupLine($"[cyan]Spawning {taskList.Count} sub-agents in parallel...[/]");

        var agents = taskList.Select(t => (agent: Spawn(t.name, t.task), t.task)).ToList();

        // Run all concurrently
        var runTasks = agents.Select(async a =>
        {
            AnsiConsole.MarkupLine($"  [dim]Starting: {Markup.Escape(a.agent.Name)}[/]");
            var result = await a.agent.RunAsync(a.task, ct);
            AnsiConsole.MarkupLine(a.agent.Status == SubAgentStatus.Completed
                ? $"  [green]Done: {Markup.Escape(a.agent.Name)}[/]"
                : $"  [red]Failed: {Markup.Escape(a.agent.Name)}[/]");
            return new SubAgentResult(a.agent.Name, a.agent.Id, result, a.agent.Status);
        }).ToList();

        var results = await Task.WhenAll(runTasks);

        AnsiConsole.MarkupLine($"[cyan]All {taskList.Count} sub-agents finished.[/]");
        return results;
    }

    /// <summary>
    /// Run multiple sub-agents sequentially, each seeing the previous result.
    /// Useful for pipeline-style tasks.
    /// </summary>
    public async Task<IReadOnlyList<SubAgentResult>> RunSequentialAsync(
        IEnumerable<(string name, string task)> tasks,
        CancellationToken ct = default)
    {
        var results = new List<SubAgentResult>();
        string? previousResult = null;

        foreach (var (name, task) in tasks)
        {
            // Append previous result context if available
            var fullTask = previousResult is not null
                ? $"{task}\n\nContext from previous step:\n{previousResult}"
                : task;

            var result = await RunSingleAsync(name, fullTask, ct);
            results.Add(new SubAgentResult(name, _agents.Values.Last().Id, result,
                _agents.Values.Last().Status));
            previousResult = result;
        }

        return results;
    }

    /// <summary>
    /// Cancel all running sub-agents.
    /// </summary>
    public void CancelAll()
    {
        foreach (var agent in _agents.Values.Where(a => a.Status == SubAgentStatus.Running))
            agent.Cancel();
    }

    /// <summary>
    /// Get a sub-agent by ID.
    /// </summary>
    public SubAgent? Get(string id) => _agents.GetValueOrDefault(id);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";
}

/// <summary>
/// Result from a completed sub-agent.
/// </summary>
public record SubAgentResult(string Name, string AgentId, string Output, SubAgentStatus Status)
{
    public bool IsSuccess => Status == SubAgentStatus.Completed;
}
