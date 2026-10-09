using System.Collections.Concurrent;
using AgentSharpLib.Llm;
using AgentSharpLib.Output;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

namespace AgentSharpLib.Agent.MultiAgent;

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
    private readonly int _maxTokens;
    private readonly int _maxIterations;
    private readonly int _maxConcurrentSubAgents;
    private readonly IAgentOutput _output;
    private readonly SessionUsage? _sessionUsage;
    private readonly ConcurrentDictionary<string, SubAgent> _agents = new();

    /// <summary>Default cap on how many sub-agents RunParallelAsync actually runs at
    /// once. Nothing else limits fan-out -- the model decides how many tasks to hand
    /// it -- and each sub-agent is a full AgentLoop capable of its own maxIterations
    /// round-trips, so an unbounded Task.WhenAll over dozens of tasks (plausible for a
    /// "research N sources in parallel" style request) risks a burst of simultaneous
    /// LLM/HTTP calls large enough to trip provider rate limits, with no throttle at
    /// all on the resulting cost.</summary>
    public const int DefaultMaxConcurrentSubAgents = 5;

    public IReadOnlyCollection<SubAgent> ActiveAgents => _agents.Values.ToList();

    public AgentOrchestrator(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt,
        int maxTokens = AgentLoop.DefaultMaxTokens,
        int maxIterations = AgentLoop.DefaultMaxIterations,
        int maxConcurrentSubAgents = DefaultMaxConcurrentSubAgents,
        IAgentOutput? output = null,
        SessionUsage? sessionUsage = null)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
        _maxIterations = maxIterations;
        _maxConcurrentSubAgents = maxConcurrentSubAgents;
        _output = output ?? NullAgentOutput.Instance;
        _sessionUsage = sessionUsage;
    }

    /// <summary>
    /// Spawn a single sub-agent to work on a task.
    /// Returns immediately with the sub-agent reference.
    /// </summary>
    public SubAgent Spawn(string name, string task)
    {
        var agent = new SubAgent(name, task, _llm, _tools, _approval, _systemPrompt, _maxTokens, _maxIterations, _output, _sessionUsage);
        _agents[agent.Id] = agent;
        return agent;
    }

    /// <summary>
    /// Run a single sub-agent and wait for its result.
    /// </summary>
    public async Task<string> RunSingleAsync(string name, string task, CancellationToken ct = default)
    {
        var (result, _) = await RunSingleInternalAsync(name, task, ct);
        return result;
    }

    /// <summary>
    /// Spawns and runs a single sub-agent, returning the SubAgent reference alongside
    /// its result so callers that need the agent's final Id/Status (e.g.
    /// RunSequentialAsync) don't have to guess which agent just ran.
    /// </summary>
    private async Task<(string result, SubAgent agent)> RunSingleInternalAsync(string name, string task, CancellationToken ct)
    {
        var agent = Spawn(name, task);

        _output.SubAgentStarted(name, task, inBatch: false);

        var result = await agent.RunAsync(task, ct);

        _output.SubAgentFinished(name, agent.Status, inBatch: false);

        return (result, agent);
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

        _output.SubAgentBatchStarted(taskList.Count, _maxConcurrentSubAgents);

        var agents = taskList.Select(t => (agent: Spawn(t.name, t.task), t.task)).ToList();

        // Throttled, not fully concurrent: each sub-agent is a full AgentLoop capable
        // of its own maxIterations round-trips, so letting an unbounded Task.WhenAll
        // start all of them at once risks a burst of simultaneous LLM/HTTP calls large
        // enough to trip provider rate limits, with no cap on the resulting cost.
        using var throttle = new SemaphoreSlim(_maxConcurrentSubAgents);

        var runTasks = agents.Select(async a =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                _output.SubAgentStarted(a.agent.Name, a.task, inBatch: true);
                var result = await a.agent.RunAsync(a.task, ct);
                _output.SubAgentFinished(a.agent.Name, a.agent.Status, inBatch: true);
                return new SubAgentResult(a.agent.Name, a.agent.Id, result, a.agent.Status);
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(runTasks);

        _output.SubAgentBatchFinished(taskList.Count);
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

            var (result, agent) = await RunSingleInternalAsync(name, fullTask, ct);
            results.Add(new SubAgentResult(name, agent.Id, result, agent.Status));
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
}

/// <summary>
/// Result from a completed sub-agent.
/// </summary>
public record SubAgentResult(string Name, string AgentId, string Output, SubAgentStatus Status)
{
    public bool IsSuccess => Status == SubAgentStatus.Completed;
}
