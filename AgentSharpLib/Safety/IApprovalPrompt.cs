using AgentSharpLib.Tools;

namespace AgentSharpLib.Safety;

/// <summary>What <see cref="ApprovalGate"/> asks a human to decide on.</summary>
/// <param name="DangerReason">Why the call was classified as risky, or null.</param>
/// <param name="OfferAlways">False when this call is asked about every time, so the prompt should not offer
/// "always allow" (an answer of AlwaysAllow is treated as allow-once).</param>
public sealed record ApprovalRequest(string ToolName, ToolRiskLevel RiskLevel, string InputSummary, string? DangerReason, bool OfferAlways = true);

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
