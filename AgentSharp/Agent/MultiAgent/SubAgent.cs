using AgentSharp.Context;
using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Tools;

namespace AgentSharp.Agent.MultiAgent;

/// <summary>
/// A sub-agent runs an independent agent loop with its own conversation history,
/// isolated from the parent agent's context. This mirrors Claude Code's "fork" model.
///
/// Key design decisions (from Claude Code's architecture):
/// - Sub-agents get their own conversation history (isolation)
/// - Sub-agents share the same LLM client and tools (efficiency)
/// - Sub-agents CANNOT spawn nested sub-agents (prevents recursion)
/// - Results are returned as a single string to the parent
/// - Sub-agents have their own system prompt (can be specialized)
/// </summary>
public class SubAgent
{
    public string Id { get; }
    public string Name { get; }
    public SubAgentStatus Status { get; private set; }
    public string? Result { get; private set; }
    public string? Error { get; private set; }

    private readonly AgentLoop _loop;
    private readonly CancellationTokenSource _cts;

    public SubAgent(
        string name,
        string task,
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Name = name;
        Status = SubAgentStatus.Pending;
        _cts = new CancellationTokenSource();

        // Build a specialized system prompt for the sub-agent
        var subAgentPrompt = $"""
            {systemPrompt}

            # Sub-Agent Context
            You are a sub-agent named "{name}" working on a specific task delegated by the parent agent.
            Focus exclusively on the assigned task. Be thorough but concise in your response.
            You do NOT have the ability to spawn additional sub-agents.

            Your assigned task:
            {task}
            """;

        // Create isolated agent loop -- no sub-agent tool available (prevents recursion)
        var isolatedTools = new ToolRegistry();
        foreach (var tool in tools.All)
        {
            // Filter out the sub-agent tool to prevent recursive spawning
            if (tool.Name != "sub_agent")
                isolatedTools.Register(tool);
        }

        _loop = new AgentLoop(llm, isolatedTools, approval, subAgentPrompt);
    }

    /// <summary>
    /// Run the sub-agent's task. Returns the final text response.
    /// </summary>
    public async Task<string> RunAsync(string task, CancellationToken parentCt = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentCt, _cts.Token);

        try
        {
            Status = SubAgentStatus.Running;
            Result = Program.SyncMode
                ? await _loop.RunTurnNonStreamingAsync(task, linkedCts.Token)
                : await _loop.RunTurnStreamingAsync(task, linkedCts.Token);
            Status = SubAgentStatus.Completed;
            return Result;
        }
        catch (OperationCanceledException)
        {
            Status = SubAgentStatus.Cancelled;
            Error = "Sub-agent was cancelled.";
            return Error;
        }
        catch (Exception ex)
        {
            Status = SubAgentStatus.Failed;
            Error = $"Sub-agent failed: {ex.Message}";
            return Error;
        }
    }

    /// <summary>
    /// Cancel the sub-agent.
    /// </summary>
    public void Cancel()
    {
        _cts.Cancel();
    }
}

public enum SubAgentStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
