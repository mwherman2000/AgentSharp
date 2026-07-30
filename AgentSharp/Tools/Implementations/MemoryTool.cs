using System.Text.Json;
using AgentSharp.Memory;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Lets the LLM record a fact, preference, or piece of context to persistent
/// memory (MEMORY.md). SystemPromptBuilder reads it back via
/// MemoryManager.GetForSystemPrompt() and injects it into the system prompt of
/// future sessions in this project -- this tool is the write side of that loop.
/// Requires a MemoryManager instance, so it's registered manually rather than
/// through ToolRegistry's parameterless auto-discovery.
/// </summary>
public class MemoryTool : ToolBase
{
    private readonly MemoryManager _memory;

    public override string Name => "remember";
    public override string Description =>
        "Record a fact, preference, or piece of context to persistent memory. " +
        "Saved entries are automatically included in the system prompt of future " +
        "sessions in this project. Use this for information worth remembering " +
        "across conversations (user preferences, project conventions, recurring " +
        "gotchas) -- not for details only relevant to the current task.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Write;

    public MemoryTool(MemoryManager memory)
    {
        _memory = memory;
    }

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            entry = new
            {
                type = "string",
                description = "The fact or context to remember, written so it still makes sense read out of context in a future session"
            }
        },
        required = new[] { "entry" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var entry = GetRequiredString(input, "entry");
        await _memory.AppendAsync(entry);
        return ToolResult.Success("Saved to memory.");
    }
}
