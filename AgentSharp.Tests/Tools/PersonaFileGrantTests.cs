using System.Text.Json;
using AgentSharp.Tests.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

namespace AgentSharp.Tests.Tools;

/// <summary>
/// "Allow edits to this persona file for the session": a grant covers edits to the prompt only. An edit that changes the
/// header (name, aliases, role, handoff, order) is asked about again, and so is any other persona file.
/// </summary>
public class PersonaFileGrantTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentsharp_grant_" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _folder;

    private const string Original = "---\nname: Tess Pando\naliases: tea\nrole: tea expert\nhandoff: false\n---\nYou are Tess, a tea expert.\nBe brief.";

    public PersonaFileGrantTests()
    {
        _folder = Path.Combine(_root, ".agentsharp", "superprompts");
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private string Persona(string name, string text = Original)
    {
        var path = Path.Combine(_folder, name + ".md");
        File.WriteAllText(path, text);
        return path;
    }

    private static JsonElement Write(string path, string content) => JsonSerializer.SerializeToElement(new { path, content });
    private static JsonElement Edit(string path, string oldText, string newText) =>
        JsonSerializer.SerializeToElement(new { path, old_string = oldText, new_string = newText });

    private static FakeTool Tool(string name) => new(name, ToolRiskLevel.Write, _ => ToolResult.Success("ok"));

    private sealed class Prompt(ApprovalResult answer) : IApprovalPrompt
    {
        public List<ApprovalRequest> Requests { get; } = [];
        public Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(answer);
        }
    }

    // ---- the grant ----

    [Fact]
    public async Task ThePromptForAPersonaFile_OffersAPerFileGrant_WithALabelNamingTheFile()
    {
        var prompt = new Prompt(ApprovalResult.Allow);
        var gate = new ApprovalGate(prompt);

        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(Persona("tess"), Original + "!"));

        var request = Assert.Single(prompt.Requests);
        Assert.True(request.OfferAlways);
        Assert.Contains("tess.md", request.AlwaysLabel);
        Assert.Contains("header changes still ask", request.AlwaysLabel);
    }

    [Fact]
    public async Task AfterAGrant_PromptOnlyEdits_AreNotAsked_ForWriteFileAndEditFile()
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));   // asked: grants this file
        Assert.Single(prompt.Requests);

        var newBody = Original.Replace("Be brief.", "Be very brief.");
        Assert.True(await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, newBody)));
        Assert.True(await gate.CheckApprovalAsync(Tool("edit_file"), "e", Edit(path, "Be brief.", "Be witty.")));

        Assert.Single(prompt.Requests);
    }

    [Theory]
    [InlineData("handoff: false", "handoff: true")]
    [InlineData("role: tea expert", "role: anything")]
    [InlineData("name: Tess Pando", "name: Evil Pando")]
    [InlineData("aliases: tea", "aliases: tea, andy2")]
    public async Task AfterAGrant_AnEditToTheHeader_IsAskedAgain(string from, string to)
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));
        prompt.Requests.Clear();

        var viaWrite = await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original.Replace(from, to)));
        var viaEdit = await gate.CheckApprovalAsync(Tool("edit_file"), "e", Edit(path, from, to));

        Assert.True(viaWrite && viaEdit);
        Assert.Equal(2, prompt.Requests.Count);
        Assert.All(prompt.Requests, r => Assert.Contains("changes the persona's header", r.DangerReason));
    }

    [Fact]
    public async Task AGrantCoversOnlyThatFile()
    {
        var tess = Persona("tess");
        var other = Persona("other");
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(tess, Original + "\nMore."));
        prompt.Requests.Clear();

        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(other, Original + "\nMore."));

        Assert.Single(prompt.Requests);
    }

    [Fact]
    public async Task TheGrantIsKeyedOnTheResolvedPath_SoDotDotAndCaseDoNotEscapeOrDodgeIt()
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));
        prompt.Requests.Clear();

        var roundabout = Path.Combine(_folder, "..", "superprompts", "TESS.md");
        Assert.True(await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(roundabout, Original + "\nMore still.")));

        Assert.Empty(prompt.Requests);
    }

    [Fact]
    public async Task ADeniedPrompt_GrantsNothing()
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.Deny);
        var gate = new ApprovalGate(prompt);

        Assert.False(await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore.")));
        Assert.False(await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore.")));

        Assert.Equal(2, prompt.Requests.Count);
    }

    [Fact]
    public async Task AllowOnce_GrantsNothing()
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.Allow);
        var gate = new ApprovalGate(prompt);

        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));

        Assert.Equal(2, prompt.Requests.Count);
    }

    [Fact]
    public async Task AGrant_DoesNotMakeOtherWriteFileCallsAutoApprovedBeyondThatFile_AndNeverGrantsTheWholeTool()
    {
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(Persona("tess"), Original + "\nMore."));
        prompt.Requests.Clear();

        // An ordinary file is auto-approved as before (no prompt); a different persona file still asks.
        Assert.True(await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(Path.Combine(_root, "notes.md"), "x")));
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(Path.Combine(_folder, "brand-new.md"), Original));

        Assert.Single(prompt.Requests);
    }

    [Fact]
    public async Task AFileThatNoLongerExists_IsAskedAbout_EvenWithAGrant()
    {
        var path = Persona("tess");
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original + "\nMore."));
        prompt.Requests.Clear();
        File.Delete(path);

        await gate.CheckApprovalAsync(Tool("write_file"), "w", Write(path, Original));

        Assert.Single(prompt.Requests);
    }

    [Fact]
    public async Task CreateDigital_StillOffersNoAlways()
    {
        var prompt = new Prompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        var tool = new FakeTool("create_digital", ToolRiskLevel.Destructive, _ => ToolResult.Success("ok"));

        await gate.CheckApprovalAsync(tool, "c");
        await gate.CheckApprovalAsync(tool, "c");

        Assert.Equal(2, prompt.Requests.Count);
        Assert.All(prompt.Requests, r => Assert.False(r.OfferAlways));
    }

    // ---- the header rule on its own ----

    [Fact]
    public void ChangesHeader_IsFalse_OnlyWhenTheHeaderIsCertainlyUnchanged()
    {
        var path = Persona("tess");

        Assert.False(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, Original.Replace("Be brief.", "x"))));
        Assert.False(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "Be brief.", "Be bold.")));
        Assert.True(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, Original.Replace("false", "true"))));
    }

    [Fact]
    public void ChangesHeader_Asks_WhenItCannotTell()
    {
        var path = Persona("tess");

        Assert.True(PersonaFileEdit.ChangesHeader("write_file", Path.Combine(_folder, "missing.md"), Write(path, Original)));
        Assert.True(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, "no header at all")));
        Assert.True(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, "---\nname: X\nno closing line")));
        Assert.True(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "not in the file", "x")));
        Assert.True(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "e", "E")));   // matches many places
        Assert.True(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "", "x")));
        Assert.True(PersonaFileEdit.ChangesHeader("run_shell", path, Write(path, Original)));
        Assert.True(PersonaFileEdit.ChangesHeader("write_file", path, JsonSerializer.SerializeToElement(new { path })));
    }

    [Fact]
    public void ChangesHeader_Asks_WhenTheCurrentFileHasNoValidHeader()
    {
        var path = Persona("broken", "just text");

        Assert.True(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, Original)));
    }

    [Fact]
    public void ChangesHeader_IgnoresLineEndings()
    {
        var path = Persona("crlf", Original.Replace("\n", "\r\n"));

        Assert.False(PersonaFileEdit.ChangesHeader("write_file", path, Write(path, Original.Replace("Be brief.", "Be bold."))));
        Assert.False(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "Be brief.", "Be bold.")));
    }

    [Fact]
    public void ChangesHeader_NoticesAnEditThatRemovesTheClosingLine()
    {
        var path = Persona("tess");

        Assert.True(PersonaFileEdit.ChangesHeader("edit_file", path, Edit(path, "handoff: false\n---\n", "handoff: false\n")));
    }
}
