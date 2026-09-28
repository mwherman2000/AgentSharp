using AgentSharpLib.Tools;

namespace AgentSharpLib.Safety;

/// <summary>What <see cref="ApprovalGate"/> asks a human to decide on.</summary>
/// <param name="DangerReason">Why a shell command was classified as risky, or null.</param>
public sealed record ApprovalRequest(string ToolName, ToolRiskLevel RiskLevel, string InputSummary, string? DangerReason);

/// <summary>
/// Asks a human whether a Destructive tool call may run. <see cref="ApprovalGate"/>
/// owns the policy (risk levels, session "always allow", serializing concurrent
/// prompts); an implementation only presents the request and returns the answer.
/// It should return <see cref="ApprovalResult.Deny"/> -- not block -- when no human
/// can answer (e.g. stdin redirected).
/// </summary>
public interface IApprovalPrompt
{
    Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct);
}
