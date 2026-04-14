using System.Text.Json;
using AgentSharp.Tools;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Tools;

public class ToolRegistryTests
{
    private class FakeTool : ToolBase
    {
        public override string Name => "fake_tool";
        public override string Description => "A fake tool for testing";
        public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

        protected override JsonElement BuildInputSchema() => SchemaFrom(new
        {
            type = "object",
            properties = new
            {
                message = new { type = "string" }
            }
        });

        public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
        {
            var msg = GetOptionalString(input, "message") ?? "default";
            return Task.FromResult(ToolResult.Success($"Executed: {msg}"));
        }
    }

    private class FailingTool : ToolBase
    {
        public override string Name => "failing_tool";
        public override string Description => "A tool that always throws";
        public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;
        protected override JsonElement BuildInputSchema() => SchemaFrom(new { type = "object" });

        public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Tool exploded!");
        }
    }

    [Fact]
    public void Register_AddsTool()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool());

        Assert.Single(registry.All);
        Assert.NotNull(registry.Get("fake_tool"));
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownTool()
    {
        var registry = new ToolRegistry();
        Assert.Null(registry.Get("nonexistent"));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool());

        Assert.NotNull(registry.Get("FAKE_TOOL"));
        Assert.NotNull(registry.Get("Fake_Tool"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_ForUnknownTool()
    {
        var registry = new ToolRegistry();
        var input = JsonDocument.Parse("{}").RootElement;

        var result = await registry.ExecuteAsync("nonexistent", input);

        Assert.True(result.IsError);
        Assert.Contains("Unknown tool", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesRegisteredTool()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool());
        var input = JsonDocument.Parse("""{"message": "hello"}""").RootElement;

        var result = await registry.ExecuteAsync("fake_tool", input);

        Assert.False(result.IsError);
        Assert.Equal("Executed: hello", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_CatchesExceptions_ReturnsError()
    {
        var registry = new ToolRegistry();
        registry.Register(new FailingTool());
        var input = JsonDocument.Parse("{}").RootElement;

        var result = await registry.ExecuteAsync("failing_tool", input);

        Assert.True(result.IsError);
        Assert.Contains("Tool exploded!", result.Output);
    }

    [Fact]
    public void GetDefinitions_ReturnsAllTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool());
        registry.Register(new FailingTool());

        var defs = registry.GetDefinitions();

        Assert.Equal(2, defs.Count);
        Assert.Contains(defs, d => d.Name == "fake_tool");
        Assert.Contains(defs, d => d.Name == "failing_tool");
    }

    [Fact]
    public void DiscoverTools_FindsToolsInAssembly()
    {
        var registry = new ToolRegistry();
        registry.DiscoverTools(typeof(AgentSharp.Tools.Implementations.ReadFileTool).Assembly);

        // Should discover all parameterless tools (not SubAgentTool which requires constructor args)
        Assert.True(registry.All.Count >= 7, $"Expected at least 7 tools, found {registry.All.Count}");
        Assert.NotNull(registry.Get("read_file"));
        Assert.NotNull(registry.Get("write_file"));
        Assert.NotNull(registry.Get("grep"));
        Assert.NotNull(registry.Get("think"));
    }
}
