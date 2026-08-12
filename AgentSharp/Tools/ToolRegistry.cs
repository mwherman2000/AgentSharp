using System.Reflection;
using AgentSharp.Llm;

namespace AgentSharp.Tools;

/// <summary>
/// Registry of all available tools. Supports:
/// - Auto-discovery via assembly scanning (find all ITool implementations)
/// - Manual registration for testing or custom tools
/// - O(1) lookup by name
/// - Execution with error handling (errors returned as data, not exceptions)
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All registered tools.
    /// </summary>
    public IReadOnlyCollection<ITool> All => _tools.Values;

    /// <summary>
    /// Register a single tool.
    /// </summary>
    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// Auto-discover and register all ITool implementations in the given assembly.
    /// Skips abstract classes and interfaces.
    /// </summary>
    public void DiscoverTools(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        var toolTypes = assembly.GetTypes()
            .Where(t => typeof(ITool).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in toolTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is ITool tool)
                    Register(tool);
            }
            catch
            {
                // Skip tools that require constructor parameters --
                // they must be registered manually.
            }
        }
    }

    /// <summary>
    /// Get a tool by name. Returns null if not found.
    /// </summary>
    public ITool? Get(string name) => _tools.GetValueOrDefault(name);

    /// <summary>
    /// Execute a tool by name. Returns error result for unknown tools.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(string name, System.Text.Json.JsonElement input, CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return ToolResult.Error($"Unknown tool '{name}'. Available tools: {string.Join(", ", _tools.Keys)}");

        try
        {
            return await tool.ExecuteAsync(input, ct);
        }
        catch (OperationCanceledException)
        {
            // Let cancellation (e.g. Ctrl+C) propagate so the agent loop stops the
            // turn, instead of reporting it to the LLM as a failed tool call.
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Tool '{name}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get tool definitions for the LLM API.
    /// </summary>
    public IReadOnlyList<ToolDefinition> GetDefinitions()
        => _tools.Values.Select(t => t.ToDefinition()).ToList();
}
