using System.Text.Json;
using AgentSharpLib.Llm;

namespace AgentSharpLib.Tools;

/// <summary>
/// Interface for all tools in the agent.
/// Implement this interface and the tool will be auto-discovered via assembly scanning.
///
/// Each tool defines:
/// - Name: identifier the LLM uses to call it
/// - Description: helps the LLM understand when to use it
/// - InputSchema: JSON Schema defining the expected input parameters
/// - RiskLevel: determines whether user approval is required
/// - ExecuteAsync: performs the actual work
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }
    ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// The personas (canonical keys) that may use this tool, or null for every persona. A tool that only
    /// one persona should have, such as <c>create_digital</c> for the alchemist, names it here; <see cref="ToolScope"/>
    /// then keeps the tool out of every other persona's tool list and refuses its calls.
    /// </summary>
    IReadOnlyList<string>? OnlyPersonas => null;

    Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default);

    /// <summary>
    /// Convert this tool to a ToolDefinition for the LLM API.
    /// </summary>
    ToolDefinition ToDefinition() => new()
    {
        Name = Name,
        Description = Description,
        InputSchema = InputSchema
    };
}
