using System.Text.Json;
using AgentSharp.Agent.MultiAgent;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Tool that allows the LLM to spawn sub-agents for delegated tasks.
/// This is the simplified equivalent of Claude Code's "Task" / "Agent" tool.
///
/// The LLM can spawn sub-agents in two modes:
/// 1. Single: Spawn one sub-agent for a specific task
/// 2. Parallel: Spawn multiple sub-agents for independent tasks
///
/// Sub-agents run in isolated conversation contexts and cannot
/// spawn their own sub-agents (prevents infinite recursion).
/// </summary>
public class SubAgentTool : ToolBase
{
    private readonly AgentOrchestrator _orchestrator;

    public override string Name => "sub_agent";
    public override string Description =>
        "Spawn a sub-agent to work on a delegated task in an isolated context. " +
        "Use this when a task can be broken into independent subtasks that benefit from " +
        "separate context windows. Each sub-agent gets its own conversation and cannot " +
        "spawn further sub-agents. For multiple independent tasks, provide an array of tasks " +
        "to run them in parallel.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Write;

    public SubAgentTool(AgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            task = new
            {
                type = "string",
                description = "A clear, self-contained description of what the sub-agent should do. " +
                              "Include all necessary context since the sub-agent has no access to your conversation."
            },
            name = new
            {
                type = "string",
                description = "A short descriptive name for this sub-agent (e.g., 'test-runner', 'code-reviewer')"
            },
            tasks = new
            {
                type = "array",
                description = "For parallel execution: array of {name, task} objects. " +
                              "Use this instead of 'task' when you have multiple independent tasks.",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Sub-agent name" },
                        task = new { type = "string", description = "Task description" }
                    },
                    required = new[] { "name", "task" }
                }
            },
            mode = new
            {
                type = "string",
                @enum = new[] { "single", "parallel", "sequential" },
                description = "Execution mode. 'single' (default): one sub-agent. " +
                              "'parallel': run all tasks concurrently. " +
                              "'sequential': run tasks one after another, each seeing the previous result."
            }
        },
        required = Array.Empty<string>()
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var mode = GetOptionalString(input, "mode") ?? "single";

        try
        {
            return mode switch
            {
                "single" => await RunSingleAsync(input, ct),
                "parallel" => await RunParallelAsync(input, ct),
                "sequential" => await RunSequentialAsync(input, ct),
                _ => ToolResult.Error($"Unknown mode: {mode}. Use 'single', 'parallel', or 'sequential'.")
            };
        }
        catch (OperationCanceledException)
        {
            // Propagate so the parent agent loop stops the turn instead of
            // reporting cancellation to the LLM as a failed sub-agent call.
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Sub-agent error: {ex.Message}");
        }
    }

    private async Task<ToolResult> RunSingleAsync(JsonElement input, CancellationToken ct)
    {
        var task = GetOptionalString(input, "task");
        var name = GetOptionalString(input, "name") ?? "sub-agent";

        if (string.IsNullOrWhiteSpace(task))
            return ToolResult.Error("'task' is required for single mode.");

        var result = await _orchestrator.RunSingleAsync(name, task, ct);
        return ToolResult.Success($"Sub-agent '{name}' result:\n\n{result}");
    }

    private async Task<ToolResult> RunParallelAsync(JsonElement input, CancellationToken ct)
    {
        var tasks = ParseTaskList(input);
        if (tasks is null || tasks.Count == 0)
            return ToolResult.Error("'tasks' array is required for parallel mode.");

        var results = await _orchestrator.RunParallelAsync(tasks, ct);
        return FormatResults(results);
    }

    private async Task<ToolResult> RunSequentialAsync(JsonElement input, CancellationToken ct)
    {
        var tasks = ParseTaskList(input);
        if (tasks is null || tasks.Count == 0)
            return ToolResult.Error("'tasks' array is required for sequential mode.");

        var results = await _orchestrator.RunSequentialAsync(tasks, ct);
        return FormatResults(results);
    }

    private static List<(string name, string task)>? ParseTaskList(JsonElement input)
    {
        if (!input.TryGetProperty("tasks", out var tasksArray) ||
            tasksArray.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<(string, string)>();
        foreach (var item in tasksArray.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "agent" : "agent";
            var task = item.TryGetProperty("task", out var t) ? t.GetString() ?? "" : "";
            if (!string.IsNullOrWhiteSpace(task))
                result.Add((name, task));
        }
        return result;
    }

    private static ToolResult FormatResults(IReadOnlyList<SubAgentResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Completed {results.Count} sub-agent tasks:\n");

        foreach (var r in results)
        {
            var status = r.IsSuccess ? "SUCCESS" : "FAILED";
            sb.AppendLine($"--- [{status}] {r.Name} ---");
            sb.AppendLine(r.Output);
            sb.AppendLine();
        }

        var allSuccess = results.All(r => r.IsSuccess);
        return allSuccess
            ? ToolResult.Success(sb.ToString())
            : ToolResult.Error(sb.ToString());
    }
}
