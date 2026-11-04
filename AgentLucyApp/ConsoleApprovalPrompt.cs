using AgentSharpLib.Safety;

namespace AgentLucyApp;

/// <summary>
/// Asks at the terminal before the agent runs a Destructive tool (e.g. a shell command).
/// Answers, as in the main app: a = allow once, d = deny, s = always allow this tool for the session
/// (not offered for create_digital, which is always asked). Whole words work too.
/// </summary>
internal sealed class ConsoleApprovalPrompt(Func<string> agentName) : IApprovalPrompt
{
    public Task<ApprovalResult> PromptAsync(ApprovalRequest request, CancellationToken ct)
    {
        // Nobody can answer (piped input): deny rather than hang.
        if (Console.IsInputRedirected)
            return Task.FromResult(ApprovalResult.Deny);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine($"{agentName()} wants to run {request.ToolName} ({request.RiskLevel}):");
        Console.WriteLine($"  {request.InputSummary}");
        if (request.DangerReason is not null)
            Console.WriteLine($"  Warning: {request.DangerReason}");
        Console.Write(request.ToolName == "create_digital"
            ? "Allow this tool execution? (a = allow, d = deny): "
            : "Allow this tool execution? (a = allow, d = deny, s = always allow this session): ");
        Console.ResetColor();

        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return Task.FromResult(answer switch
        {
            "a" or "allow" or "y" or "yes" => ApprovalResult.Allow,
            "s" or "always" when request.ToolName != "create_digital" => ApprovalResult.AlwaysAllow,
            _ => ApprovalResult.Deny
        });
    }
}
