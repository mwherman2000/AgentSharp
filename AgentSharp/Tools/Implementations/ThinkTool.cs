using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// A "thinking" tool that forces the LLM to reason step-by-step.
/// This is a no-op tool -- it doesn't execute anything.
/// The LLM uses it to structure its reasoning before taking action.
///
/// Inspired by Claude Code's "think" tool and the extended thinking pattern.
/// The tool simply echoes back the thought, giving the model a way to
/// "think out loud" within the tool-use framework.
/// </summary>
public class ThinkTool : ToolBase
{
    public override string Name => "think";
    public override string Description =>
        "Use this tool to think through a problem step-by-step before taking action. " +
        "Write out your reasoning, analysis, and plan. This helps you make better decisions " +
        "about which tools to use and in what order. The thought is recorded but no action is taken.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            thought = new { type = "string", description = "Your step-by-step reasoning about the current task" }
        },
        required = new[] { "thought" }
    });

    public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var thought = GetRequiredString(input, "thought");
        return Task.FromResult(ToolResult.Success($"Thought recorded. Continue with your plan."));
    }
}
