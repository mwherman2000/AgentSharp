using System.Text.Json;
using AgentSharp.Tools;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// A minimal ITool for testing AgentLoop's tool-execution loop without depending on
/// a real tool implementation. Records every input it was called with.
/// </summary>
internal class FakeTool : ITool
{
    private readonly Func<JsonElement, ToolResult> _execute;

    public FakeTool(string name, ToolRiskLevel riskLevel, Func<JsonElement, ToolResult> execute)
    {
        Name = name;
        RiskLevel = riskLevel;
        _execute = execute;
    }

    public string Name { get; }
    public string Description => "A fake tool for testing.";
    public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object" });
    public ToolRiskLevel RiskLevel { get; }
    public List<JsonElement> Calls { get; } = new();

    public Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        Calls.Add(input);
        return Task.FromResult(_execute(input));
    }
}
