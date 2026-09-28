using System.Text.Json;
using AgentSharp.Tests.Agent;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

namespace AgentSharp.Tests.Safety;

public class ApprovalGateTests
{
    private sealed class FakePrompt : IApprovalPrompt
    {
        private readonly ApprovalResult _answer;
        public FakePrompt(ApprovalResult answer) => _answer = answer;
        public List<ApprovalRequest> Requests { get; } = new();

        public Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_answer);
        }
    }

    private static FakeTool Tool(string name, ToolRiskLevel risk) =>
        new(name, risk, _ => ToolResult.Success("ok"));

    [Theory]
    [InlineData(ToolRiskLevel.ReadOnly)]
    [InlineData(ToolRiskLevel.Write)]
    public async Task NonDestructiveTools_AreApprovedWithoutPrompting(ToolRiskLevel risk)
    {
        var prompt = new FakePrompt(ApprovalResult.Deny);
        var gate = new ApprovalGate(prompt);

        Assert.True(await gate.CheckApprovalAsync(Tool("t", risk), "summary"));
        Assert.Empty(prompt.Requests);
    }

    [Fact]
    public async Task DestructiveTool_WithNoPrompt_IsDenied()
    {
        var gate = new ApprovalGate();

        Assert.False(await gate.CheckApprovalAsync(Tool("rm", ToolRiskLevel.Destructive), "summary"));
    }

    [Theory]
    [InlineData(ApprovalResult.Allow, true)]
    [InlineData(ApprovalResult.AlwaysAllow, true)]
    [InlineData(ApprovalResult.Deny, false)]
    public async Task DestructiveTool_FollowsPromptAnswer(ApprovalResult answer, bool expected)
    {
        var gate = new ApprovalGate(new FakePrompt(answer));

        Assert.Equal(expected, await gate.CheckApprovalAsync(Tool("rm", ToolRiskLevel.Destructive), "summary"));
    }

    [Fact]
    public async Task AlwaysAllow_SkipsPromptForSameToolAfterward()
    {
        var prompt = new FakePrompt(ApprovalResult.AlwaysAllow);
        var gate = new ApprovalGate(prompt);
        var tool = Tool("rm", ToolRiskLevel.Destructive);

        await gate.CheckApprovalAsync(tool, "first");
        Assert.True(await gate.CheckApprovalAsync(tool, "second"));
        Assert.Single(prompt.Requests);
    }

    [Fact]
    public async Task Allow_StillPromptsNextTime()
    {
        var prompt = new FakePrompt(ApprovalResult.Allow);
        var gate = new ApprovalGate(prompt);
        var tool = Tool("rm", ToolRiskLevel.Destructive);

        await gate.CheckApprovalAsync(tool, "first");
        await gate.CheckApprovalAsync(tool, "second");
        Assert.Equal(2, prompt.Requests.Count);
    }

    [Fact]
    public async Task RunShell_PassesClassifierDangerReasonToPrompt()
    {
        var prompt = new FakePrompt(ApprovalResult.Deny);
        var gate = new ApprovalGate(prompt);
        var input = JsonSerializer.SerializeToElement(new { command = "rm -rf /" });

        await gate.CheckApprovalAsync(Tool("run_shell", ToolRiskLevel.Destructive), "Run: rm -rf /", input);

        var request = Assert.Single(prompt.Requests);
        Assert.Equal("run_shell", request.ToolName);
        Assert.Equal(ToolRiskLevel.Destructive, request.RiskLevel);
        Assert.Equal("Run: rm -rf /", request.InputSummary);
        Assert.NotNull(request.DangerReason);
    }
}
