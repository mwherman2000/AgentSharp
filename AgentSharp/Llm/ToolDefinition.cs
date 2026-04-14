using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSharp.Llm;

/// <summary>
/// A tool definition sent to the LLM so it knows what tools are available.
/// </summary>
public record ToolDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("input_schema")]
    public required JsonElement InputSchema { get; init; }
}
