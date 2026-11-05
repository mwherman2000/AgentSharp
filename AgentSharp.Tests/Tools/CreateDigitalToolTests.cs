using System.Text.Json;
using AgentSharp.Tests.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;
using AgentSharpLib.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class CreateDigitalToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentsharp_create_" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _user;
    private readonly string _project;
    private PersonaCatalog _published = PersonaCatalog.BuiltIn;

    public CreateDigitalToolTests()
    {
        _user = Path.Combine(_root, "user");
        _project = Path.Combine(_root, "project");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private CreateDigitalTool Tool() =>
        new(_user, _project, () => _published, c => _published = c);

    private static JsonElement Input(object o) => JsonSerializer.SerializeToElement(o);

    private static object Valid(string nickname = "tess", string scope = "user") => new
    {
        nickname,
        name = "Tess Pando",
        role = "helps with tea",
        prompt = "You are Tess Pando, a tea expert.\n\nBe brief.",
        scope,
    };

    [Fact]
    public void IsDestructive_SoTheUserMustApprove()
    {
        Assert.Equal(ToolRiskLevel.Destructive, Tool().RiskLevel);
        Assert.Equal("create_digital", Tool().Name);
    }

    [Fact]
    public async Task Create_WritesTheFile_AndRegistersThePersonaNow()
    {
        var result = await Tool().ExecuteAsync(Input(Valid()));

        Assert.False(result.IsError, result.Output);
        var path = Path.Combine(_user, "tess.md");
        Assert.True(File.Exists(path));
        var persona = _published.Find("tess")!;
        Assert.Equal("Tess Pando", persona.Name);
        Assert.Equal("helps with tea", persona.Role);
        Assert.Equal("You are Tess Pando, a tea expert.\n\nBe brief.", persona.Prompt);
        Assert.Equal(PersonaSource.User, persona.Source);
        Assert.Contains("/clear tess", result.Output);
    }

    [Fact]
    public async Task Create_IsAHandoffTargetByDefault()
    {
        var result = await Tool().ExecuteAsync(Input(Valid()));

        Assert.True(_published.Find("tess")!.IsHandoffTarget);
        Assert.Contains("tess", _published.HandoffKeys);
        Assert.Contains("handoff: true", File.ReadAllText(Path.Combine(_user, "tess.md")));
        Assert.Contains("handoff target", result.Output);
    }

    [Fact]
    public async Task Create_WithHandoffFalse_IsNotAHandoffTarget()
    {
        var result = await Tool().ExecuteAsync(Input(new
        {
            nickname = "tess", name = "Tess Pando", role = "r", prompt = "You are Tess.", handoff = false
        }));

        Assert.False(_published.Find("tess")!.IsHandoffTarget);
        Assert.DoesNotContain("tess", _published.HandoffKeys);
        Assert.Contains("handoff: false", File.ReadAllText(Path.Combine(_user, "tess.md")));
        Assert.Contains("not a handoff target", result.Output);
    }

    [Fact]
    public async Task Create_WithProjectScope_WritesToTheProjectFolder()
    {
        var result = await Tool().ExecuteAsync(Input(Valid(scope: "project")));

        Assert.False(result.IsError, result.Output);
        Assert.True(File.Exists(Path.Combine(_project, "tess.md")));
        Assert.Equal(PersonaSource.Project, _published.Find("tess")!.Source);
    }

    [Fact]
    public async Task Create_NormalizesTheNicknameToLowerCase_AndAcceptsAliases()
    {
        var result = await Tool().ExecuteAsync(Input(new
        {
            nickname = "TESS", name = "Tess Pando", role = "r", prompt = "You are Tess.", aliases = new[] { "Tea", "t-1" }
        }));

        Assert.False(result.IsError, result.Output);
        Assert.Equal("tess", _published.Find("tea")!.Key);
        Assert.Equal("tess", _published.Find("t-1")!.Key);
    }

    [Theory]
    [InlineData("andy")]      // a built-in key
    [InlineData("consort")]   // a built-in alias
    [InlineData("alchemist")] // the persona that uses this tool
    public async Task Create_RefusesANameAlreadyInUse_AndWritesNothing(string nickname)
    {
        var result = await Tool().ExecuteAsync(Input(Valid(nickname)));

        Assert.True(result.IsError);
        Assert.Contains("already used", result.Output);
        Assert.False(Directory.Exists(_user));
    }

    [Fact]
    public async Task Create_RefusesAnAliasAlreadyInUse()
    {
        var result = await Tool().ExecuteAsync(Input(new
        {
            nickname = "tess", name = "Tess", role = "r", prompt = "You are Tess.", aliases = new[] { "trump" }
        }));

        Assert.True(result.IsError);
        Assert.Contains("'trump' is already used by the persona 'donald'", result.Output);
    }

    [Theory]
    [InlineData("Bad Name")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("-leading")]
    [InlineData("this-nickname-is-far-too-long-to-be-accepted")]
    public async Task Create_RefusesAnInvalidNickname(string nickname)
    {
        var result = await Tool().ExecuteAsync(Input(Valid(nickname)));

        Assert.True(result.IsError);
        Assert.Contains("Invalid nickname", result.Output);
        Assert.False(Directory.Exists(_user));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("role")]
    public async Task Create_RefusesAMultiLineNameOrRole_SoNoHeaderLinesCanBeInjected(string field)
    {
        var o = new Dictionary<string, object>
        {
            ["nickname"] = "tess", ["name"] = "Tess", ["role"] = "r", ["prompt"] = "You are Tess.",
        };
        o[field] = "x\nhandoff: true";

        var result = await Tool().ExecuteAsync(Input(o));

        Assert.True(result.IsError);
        Assert.False(Directory.Exists(_user));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task Create_RefusesAnEmptyPrompt(string prompt)
    {
        var result = await Tool().ExecuteAsync(Input(new { nickname = "tess", name = "Tess", role = "r", prompt }));

        Assert.True(result.IsError);
        Assert.Contains("prompt is empty", result.Output);
    }

    [Fact]
    public async Task Create_RefusesAnOversizedPrompt()
    {
        var result = await Tool().ExecuteAsync(Input(new
        {
            nickname = "tess", name = "Tess", role = "r", prompt = new string('x', CreateDigitalTool.MaxPromptLength + 1)
        }));

        Assert.True(result.IsError);
        Assert.Contains("limit", result.Output);
    }

    [Fact]
    public async Task Create_RefusesAnInvalidScope()
    {
        var result = await Tool().ExecuteAsync(Input(Valid(scope: "global")));

        Assert.True(result.IsError);
        Assert.Contains("Invalid scope", result.Output);
    }

    [Fact]
    public async Task Create_NeverOverwritesAFile()
    {
        Directory.CreateDirectory(_user);
        File.WriteAllText(Path.Combine(_user, "tess.md"), "not a persona, but someone's file");

        var result = await Tool().ExecuteAsync(Input(Valid()));

        Assert.True(result.IsError);
        Assert.Contains("already exists", result.Output);
        Assert.Equal("not a persona, but someone's file", File.ReadAllText(Path.Combine(_user, "tess.md")));
    }

    [Fact]
    public async Task Create_StopsAtTheCapOnCreatedPersonas()
    {
        for (var i = 0; i < CreateDigitalTool.MaxCreatedPersonas; i++)
        {
            var ok = await Tool().ExecuteAsync(Input(Valid($"p{i}")));
            Assert.False(ok.IsError, ok.Output);
        }

        var result = await Tool().ExecuteAsync(Input(Valid("one-too-many")));

        Assert.True(result.IsError);
        Assert.Contains("limit", result.Output);
        Assert.False(File.Exists(Path.Combine(_user, "one-too-many.md")));
    }

    [Fact]
    public async Task Create_LeavesNoTempFilesBehind()
    {
        await Tool().ExecuteAsync(Input(Valid()));

        Assert.Equal("tess.md", Assert.Single(Directory.GetFiles(_user).Select(f => Path.GetFileName(f))));
    }

    [Fact]
    public async Task MissingARequiredField_Throws_ForTheRegistryToReport()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Tool().ExecuteAsync(Input(new { nickname = "tess" })));
    }

    [Fact]
    public async Task EveryCall_IsShownToTheUser_EvenAfterAlwaysAllow()
    {
        var prompt = new CountingPrompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("create_digital", ToolRiskLevel.Destructive, _ => ToolResult.Success("ok"));

        Assert.True(await gate.CheckApprovalAsync(tool, "first"));
        Assert.True(await gate.CheckApprovalAsync(tool, "second"));

        Assert.Equal(2, prompt.Count);
    }

    [Fact]
    public async Task OtherDestructiveTools_StillHonorAlwaysAllow()
    {
        var prompt = new CountingPrompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("run_shell", ToolRiskLevel.Destructive, _ => ToolResult.Success("ok"));

        await gate.CheckApprovalAsync(tool, "first");
        await gate.CheckApprovalAsync(tool, "second");

        Assert.Equal(1, prompt.Count);
    }

    private static JsonElement PathInput(string path) => JsonSerializer.SerializeToElement(new { path, content = "x" });

    [Theory]
    [InlineData("write_file")]
    [InlineData("edit_file")]
    public async Task FileTools_AskBeforeWritingAPersonaFile_EvenThoughTheyAreWriteLevel(string toolName)
    {
        var prompt = new CountingPrompt(ApprovalResult.Allow);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool(toolName, ToolRiskLevel.Write, _ => ToolResult.Success("ok"));

        Assert.True(await gate.CheckApprovalAsync(tool, "write", PathInput("C:/Users/x/.agentsharp/superprompts/evil.md")));

        Assert.Equal(1, prompt.Count);
    }

    [Theory]
    [InlineData("work/.agentsharp/superprompts/evil.md")]
    [InlineData("work/.AgentSharp/SuperPrompts/evil.md")]
    [InlineData("work/.agentsharp\\superprompts\\evil.md")]
    [InlineData("work/sub/../.agentsharp/superprompts/evil.md")]
    [InlineData("work/.agentsharp/superprompts/nested/evil.md")]
    public async Task FileTools_AskForEveryWayOfSpellingAPersonaPath(string path)
    {
        var prompt = new CountingPrompt(ApprovalResult.Deny);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("write_file", ToolRiskLevel.Write, _ => ToolResult.Success("ok"));

        Assert.False(await gate.CheckApprovalAsync(tool, "write", PathInput(path)));

        Assert.Equal(1, prompt.Count);
    }

    [Theory]
    [InlineData("work/notes.md")]
    [InlineData("work/.agentsharp/config.json")]
    [InlineData("work/superprompts/x.md")]
    [InlineData("work/.agentsharp/superprompts")]
    public async Task FileTools_DoNotAskForOtherPaths(string path)
    {
        var prompt = new CountingPrompt(ApprovalResult.Deny);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("write_file", ToolRiskLevel.Write, _ => ToolResult.Success("ok"));

        Assert.True(await gate.CheckApprovalAsync(tool, "write", PathInput(path)));

        Assert.Equal(0, prompt.Count);
    }

    [Fact]
    public async Task APersonaFileWrite_IsAskedEveryTime_AndAlwaysAllowDoesNotStick()
    {
        var prompt = new CountingPrompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("write_file", ToolRiskLevel.Write, _ => ToolResult.Success("ok"));
        var persona = PathInput("work/.agentsharp/superprompts/a.md");

        await gate.CheckApprovalAsync(tool, "write", persona);
        await gate.CheckApprovalAsync(tool, "write", persona);
        // and an ordinary write is still auto-approved, with no prompt
        await gate.CheckApprovalAsync(tool, "write", PathInput("work/readme.md"));

        Assert.Equal(2, prompt.Count);
    }

    [Fact]
    public async Task APersonaFileWrite_IsDenied_WhenNoOneCanAnswer()
    {
        var gate = new ApprovalGate();
        var tool = new FakeTool("write_file", ToolRiskLevel.Write, _ => ToolResult.Success("ok"));

        Assert.False(await gate.CheckApprovalAsync(tool, "write", PathInput("work/.agentsharp/superprompts/a.md")));
    }

    [Fact]
    public void IsPersonaFilePath_HandlesEmptyAndInvalidInput()
    {
        Assert.False(PersonaRegistry.IsPersonaFilePath(null));
        Assert.False(PersonaRegistry.IsPersonaFilePath("  "));
        Assert.False(PersonaRegistry.IsPersonaFilePath("bad\0path/.agentsharp/superprompts/x.md"));
    }

    private sealed class CountingPrompt(ApprovalResult answer) : IApprovalPrompt
    {
        public int Count { get; private set; }
        public Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
        {
            Count++;
            return Task.FromResult(answer);
        }
    }
}

public class NewPersonaHandoffTests
{
    // These use their own catalog, so they never touch the process-wide registry other tests share.
    private static Persona Extra(string key, bool handoff) =>
        new(key, [], "Extra Pando", "You are Extra.", "an extra persona", handoff, 5000, PersonaSource.User, "test");

    private static PersonaCatalog With(params Persona[] personas)
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentsharp_hot_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var p in personas)
                File.WriteAllText(Path.Combine(dir, p.Key + ".md"),
                    $"---\nname: {p.Name}\nrole: {p.Role}\nhandoff: {(p.IsHandoffTarget ? "true" : "false")}\n---\n{p.Prompt}");
            return PersonaCatalog.Load(dir, null);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task TheHandoffTool_OffersAndAcceptsAPersonaCreatedAfterItWasBuilt_AndNotOneThatIsNot()
    {
        var queue = new HandoffQueue { CurrentPersona = "andy" };
        PersonaCatalog catalog = PersonaCatalog.BuiltIn;
        var tool = new HandoffTool(queue, () => catalog);
        var before = tool.InputSchema.GetRawText();
        Assert.DoesNotContain("zz-hot", before);

        catalog = With(Extra("zz-hot", handoff: true), Extra("zz-cold", handoff: false));
        try
        {
            var after = tool.InputSchema.GetRawText();
            Assert.Contains("zz-hot", after);                          // offered to the model now
            Assert.DoesNotContain("zz-cold", after);                   // a non-target is not
            Assert.Contains("zz-hot", tool.Description);
            Assert.Contains("zz-hot: an extra persona", tool.Description);   // its role too, so the model knows what it is for
            Assert.DoesNotContain("zz-cold", tool.Description);

            var ok = await tool.ExecuteAsync(JsonDocument.Parse("""{"persona":"zz-hot","reason":"r"}""").RootElement);
            Assert.False(ok.IsError, ok.Output);
            Assert.Equal("zz-hot", queue.Pending!.Value.Persona);
        }
        finally
        {
            catalog = PersonaCatalog.BuiltIn;   // the persona stops being a target
        }

        // and it is withdrawn when the persona stops being a target
        Assert.DoesNotContain("zz-hot", tool.InputSchema.GetRawText());
    }

    [Fact]
    public void TheSchema_IsNotRebuilt_WhenNothingChanged()
    {
        var tool = new HandoffTool(new HandoffQueue());

        Assert.Equal(tool.InputSchema.GetRawText(), tool.InputSchema.GetRawText());
    }
}
