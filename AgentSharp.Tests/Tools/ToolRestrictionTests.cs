using System.Text.Json;
using AgentSharp.Tests.Agent;
using AgentSharpLib.Agent;
using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;
using AgentSharpLib.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

/// <summary>Which tools a persona may use: the tools:/deny: header lines, the tool-level "only these personas" rule, and where each is enforced.</summary>
public class ToolRestrictionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agentsharp_toolrestrict_" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private PersonaCatalog CatalogWith(string key, string headerLines)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, key + ".md"), $"---\nname: Test Pando\n{headerLines}---\nYou are a test persona.");
        return PersonaCatalog.Load(_dir, null);
    }

    private static Persona Parse(string header)
    {
        Assert.True(PersonaFile.TryParse("t", $"---\nname: T\n{header}---\nBody", PersonaSource.User, "t.md", out var p, out var err), err);
        return p!;
    }

    // ---- the header

    [Fact]
    public void NoToolsLines_MeansNoRestriction()
    {
        Assert.Null(Parse("").Tools);
    }

    [Fact]
    public void ToolsAndDeny_AreParsed_LowerCased_AndDeduplicated()
    {
        var p = Parse("tools: Read_File, grep, grep\ndeny: run_shell\n");

        Assert.Equal(["read_file", "grep"], p.Tools!.Allow);
        Assert.Equal(["run_shell"], p.Tools.Deny);
    }

    [Fact]
    public void AnEmptyToolsLine_MeansNoTools()
    {
        var policy = Parse("tools:\n").Tools!;

        Assert.NotNull(policy.Allow);
        Assert.Empty(policy.Allow!);
        Assert.False(policy.Permits("read_file"));
        Assert.True(policy.Permits("handoff"));
    }

    [Fact]
    public void ABadToolName_IsRejected()
    {
        Assert.False(PersonaFile.TryParse("t", "---\nname: T\ntools: read file!\n---\nBody", PersonaSource.User, "t.md", out _, out var error));
        Assert.Contains("not a valid tool name", error);
    }

    [Fact]
    public void Policy_DenyWins_AndHandoffIsNeverRemoved()
    {
        var policy = new ToolPolicy(["read_file", "run_shell"], ["run_shell", "handoff"]);

        Assert.True(policy.Permits("read_file"));
        Assert.False(policy.Permits("run_shell"));   // in both lists: deny wins
        Assert.False(policy.Permits("write_file"));  // not in the allow-list
        Assert.True(policy.Permits("handoff"));      // always available, even when denied by name
        Assert.True(policy.Permits("READ_FILE"));
    }

    [Fact]
    public void ChangingTheToolsLines_IsAHeaderEdit_SoTheFileToolsAsk()
    {
        var path = Path.Combine(_dir, "p.md");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "---\nname: P\ntools: read_file\n---\nBody");
        var edit = JsonSerializer.SerializeToElement(new { old_string = "tools: read_file", new_string = "tools: read_file, run_shell" });
        var promptEdit = JsonSerializer.SerializeToElement(new { old_string = "Body", new_string = "More" });

        Assert.True(PersonaFileEdit.ChangesHeader("edit_file", path, edit));
        Assert.False(PersonaFileEdit.ChangesHeader("edit_file", path, promptEdit));
    }

    // ---- the scope

    private sealed class OnlyTool(string name, params string[] personas) : ToolBase
    {
        public override string Name => name;
        public override string Description => "d";
        public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;
        public override IReadOnlyList<string>? OnlyPersonas => personas;
        protected override JsonElement BuildInputSchema() => SchemaFrom(new { type = "object" });
        public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default) => Task.FromResult(ToolResult.Success("ran"));
    }

    private static FakeTool Plain(string name) => new(name, ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ran " + name));

    [Fact]
    public void CreateDigital_IsOnlyForTheAlchemist()
    {
        var tool = new CreateDigitalTool();
        var scope = new ToolScope { Persona = "andy" };

        Assert.False(scope.Permits(tool));
        Assert.Contains("alchemist", scope.Denial(tool));

        scope.Persona = "alchemist";
        Assert.True(scope.Permits(tool));

        scope.Persona = "";   // a custom system prompt is not the alchemist
        Assert.False(scope.Permits(tool));
    }

    [Fact]
    public void ThePersonasPolicy_DecidesWhichToolsItMayUse()
    {
        var catalog = CatalogWith("reader", "tools: read_file, grep\n");
        var scope = new ToolScope(() => catalog) { Persona = "reader" };

        Assert.True(scope.Permits(Plain("read_file")));
        Assert.False(scope.Permits(Plain("run_shell")));
        Assert.Contains("Test Pando", scope.Denial(Plain("run_shell")));

        scope.Persona = "andy";   // a built-in persona has no policy
        Assert.True(scope.Permits(Plain("run_shell")));
    }

    [Fact]
    public void ANotRestrictedScope_AllowsAnythingButAnOnlyTool()
    {
        var scope = new ToolScope();   // no persona set yet

        Assert.True(scope.Permits(Plain("run_shell")));
        Assert.False(scope.Permits(new OnlyTool("secret", "alchemist")));
    }

    // ---- the loop

    private static LlmResponse ToolCall(string name) => new()
    {
        Message = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = [new ToolUseBlock { Id = "c1", Name = name, Input = JsonSerializer.SerializeToElement(new { }) }]
        },
        StopReason = "tool_use",
        InputTokens = 1,
        OutputTokens = 1,
    };

    private static LlmResponse Text(string t) => new() { Message = ChatMessage.AssistantText(t), StopReason = "end_turn", InputTokens = 1, OutputTokens = 1 };

    [Fact]
    public async Task TheModelIsOnlySentTheToolsThePersonaMayUse()
    {
        var catalog = CatalogWith("reader", "tools: read_file\n");
        var tools = new ToolRegistry();
        tools.Register(Plain("read_file"));
        tools.Register(Plain("run_shell"));
        tools.Register(new OnlyTool("secret", "alchemist"));
        var llm = new FakeLlmClient().Enqueue(Text("ok"));
        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "sys", toolScope: new ToolScope(() => catalog) { Persona = "reader" });

        await loop.RunTurnNonStreamingAsync("hi");

        Assert.Equal(new[] { "read_file" }, llm.Requests[0].Tools!.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task ACallToAToolThePersonaWasNotOffered_IsRefused_AndTheToolDoesNotRun()
    {
        var catalog = CatalogWith("reader", "tools: read_file\n");
        var shell = Plain("run_shell");
        var tools = new ToolRegistry();
        tools.Register(shell);
        var llm = new FakeLlmClient().Enqueue(ToolCall("run_shell")).Enqueue(Text("done"));
        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "sys", toolScope: new ToolScope(() => catalog) { Persona = "reader" });

        await loop.RunTurnNonStreamingAsync("hi");

        Assert.Empty(shell.Calls);
        var result = Assert.IsType<ToolResultBlock>(Assert.Single(loop.History.Messages[2].Content));
        Assert.True(result.IsError);
        Assert.Contains("not available", result.Content);
    }

    [Fact]
    public async Task AnOnlyTool_CannotBeCalledByAnotherPersona_ButCanByTheOne_ItNames()
    {
        var tool = new OnlyTool("secret", "alchemist");
        var tools = new ToolRegistry();
        tools.Register(tool);

        var scope = new ToolScope { Persona = "andy" };
        var llm = new FakeLlmClient().Enqueue(ToolCall("secret")).Enqueue(Text("done"));
        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "sys", toolScope: scope);
        await loop.RunTurnNonStreamingAsync("hi");
        Assert.True(Assert.IsType<ToolResultBlock>(Assert.Single(loop.History.Messages[2].Content)).IsError);

        scope.Persona = "alchemist";
        var llm2 = new FakeLlmClient().Enqueue(ToolCall("secret")).Enqueue(Text("done"));
        var loop2 = new AgentLoop(llm2, tools, new ApprovalGate(), "sys", toolScope: scope);
        await loop2.RunTurnNonStreamingAsync("hi");
        var ok = Assert.IsType<ToolResultBlock>(Assert.Single(loop2.History.Messages[2].Content));
        Assert.False(ok.IsError);
        Assert.Equal("ran", ok.Content);
    }

    [Fact]
    public async Task ASubAgent_GetsOnlyTheToolsTheDelegatingPersonaMayUse()
    {
        var catalog = CatalogWith("reader", "tools: read_file\n");
        var tools = new ToolRegistry();
        tools.Register(Plain("read_file"));
        tools.Register(Plain("run_shell"));
        var llm = new FakeStreamingLlmClient();   // records the request; with no scripted reply the run ends in an error, which we ignore
        var orchestrator = new AgentOrchestrator(llm, tools, new ApprovalGate(), "sys",
            toolScope: new ToolScope(() => catalog) { Persona = "reader" });

        var agent = orchestrator.Spawn("w", "task");
        try { await agent.RunAsync("task"); } catch (Exception) { /* no scripted reply */ }

        Assert.Equal(new[] { "read_file" }, llm.Requests[0].Tools!.Select(t => t.Name).ToArray());
    }

    // ---- create_digital with limits

    [Fact]
    public async Task CreateDigital_CanWriteAToolLimit_AndItReadsBackAsAPolicy()
    {
        var user = Path.Combine(_dir, "user");
        PersonaCatalog published = PersonaCatalog.BuiltIn;
        var tool = new CreateDigitalTool(user, Path.Combine(_dir, "project"), () => published, c => published = c);

        var result = await tool.ExecuteAsync(JsonSerializer.SerializeToElement(new
        {
            nickname = "zz-reader",
            name = "Reader Pando",
            role = "reads and explains files",
            prompt = "You are Reader Pando.",
            tools = new[] { "read_file", "grep" },
            deny = new[] { "run_shell" },
        }));

        Assert.False(result.IsError, result.Output);
        var policy = published.Find("zz-reader")!.Tools!;
        Assert.Equal(["read_file", "grep"], policy.Allow);
        Assert.Equal(["run_shell"], policy.Deny);
    }

    [Fact]
    public async Task CreateDigital_RejectsABadToolName_AndWritesNothing()
    {
        var user = Path.Combine(_dir, "user");
        var tool = new CreateDigitalTool(user, Path.Combine(_dir, "project"), () => PersonaCatalog.BuiltIn, _ => { });

        var result = await tool.ExecuteAsync(JsonSerializer.SerializeToElement(new
        {
            nickname = "zz-bad", name = "B", role = "r", prompt = "You are B.", tools = new[] { "read file!" },
        }));

        Assert.True(result.IsError);
        Assert.False(File.Exists(Path.Combine(user, "zz-bad.md")));
    }
}
