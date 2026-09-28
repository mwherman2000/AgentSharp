using AgentSharpLib.Safety;

namespace AgentLucyApp;

/// <summary>
/// Asks at the terminal before Lucy runs a Destructive tool (e.g. a shell command).
/// Answers: y = allow once, n = deny, a = always allow this tool for the session.
/// </summary>
internal sealed class ConsoleApprovalPrompt : IApprovalPrompt
{
    public Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
    {
        // Nobody can answer (piped input): deny rather than hang.
        if (Console.IsInputRedirected)
            return Task.FromResult(ApprovalResult.Deny);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine($"Lucy wants to run {request.ToolName} ({request.RiskLevel}):");
        Console.WriteLine($"  {request.InputSummary}");
        if (request.DangerReason is not null)
            Console.WriteLine($"  Warning: {request.DangerReason}");
        Console.Write("Allow? [y]es / [n]o / [a]lways: ");
        Console.ResetColor();

        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return Task.FromResult(answer switch
        {
            "y" or "yes" => ApprovalResult.Allow,
            "a" or "always" => ApprovalResult.AlwaysAllow,
            _ => ApprovalResult.Deny
        });
    }
}
