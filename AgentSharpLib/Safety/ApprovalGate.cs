using System.Collections.Concurrent;
using System.Text.Json;
using AgentSharpLib.Output;
using AgentSharpLib.Tools;

namespace AgentSharpLib.Safety;

/// <summary>
/// Approval response from the user.
/// </summary>
public enum ApprovalResult
{
    Allow,
    Deny,
    AlwaysAllow
}

/// <summary>
/// Manages tool execution approval based on risk levels.
/// Implements the safety gate pattern from Claude Code:
/// - ReadOnly tools: auto-approved
/// - Write tools: auto-approved (logged)
/// - Destructive tools: require explicit user confirmation
///
/// Users can grant "always allow" for specific tools to reduce friction.
/// </summary>
public class ApprovalGate
{
    // ConcurrentDictionary used as a thread-safe set (value is unused) -- one
    // ApprovalGate is shared by every concurrent sub-agent (AgentOrchestrator hands
    // the same instance to each SubAgent), so two agents granting/checking "always
    // allow" for the same or different tools at the same time must not race on a
    // plain HashSet.
    private readonly ConcurrentDictionary<string, byte> _alwaysAllowed = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Tools whose every call is shown to the user, whatever they answered before: an "always allow" answer
    /// is treated as "allow this once". create_digital makes a persona from a prompt the model wrote, and a
    /// pre-approval would let injected text create personas without anyone seeing them.
    /// </summary>
    private static readonly HashSet<string> AlwaysAsk = new(StringComparer.OrdinalIgnoreCase) { "create_digital" };

    private readonly ShellCommandClassifier _shellClassifier = new();
    private readonly IApprovalPrompt? _prompt;
    private readonly IAgentOutput _output;

    // Guards only the interactive prompt itself (console rendering + Console.ReadKey),
    // not the whole approval check -- so a call that's auto-approved or already in
    // _alwaysAllowed never blocks behind another agent's unrelated live prompt.
    // Without this, two concurrent sub-agents both hitting a Destructive tool at
    // nearly the same time could interleave their prompts on the same console and
    // both read from the same Console.ReadKey call, so a user pressing 'a' for what's
    // on screen for one agent could actually approve a completely different pending
    // command from the other.
    private readonly SemaphoreSlim _promptLock = new(1, 1);

    /// <param name="prompt">Asks a human about Destructive tool calls. Null means no
    /// one can answer, so every Destructive call is denied.</param>
    /// <param name="output">Where auto-approval/denial notices go.</param>
    public ApprovalGate(IApprovalPrompt? prompt = null, IAgentOutput? output = null)
    {
        _prompt = prompt;
        _output = output ?? NullAgentOutput.Instance;
    }

    /// <summary>
    /// Check if a tool execution requires approval and, if so, prompt the user.
    /// Returns true if the tool should be executed, false if denied.
    /// </summary>
    public async Task<bool> CheckApprovalAsync(ITool tool, string inputSummary, JsonElement input = default, CancellationToken ct = default)
    {
        // ReadOnly tools are always auto-approved
        if (tool.RiskLevel == ToolRiskLevel.ReadOnly)
            return true;

        // Writing a persona file is asked about every time, whatever the tool's own risk level or an earlier
        // "always allow": the persona's prompt would control the conversation once it is used or handed to,
        // so it must not appear without the user seeing it. create_digital asks too; this covers the file tools.
        if (PersonaFileWritten(tool, input) is { } personaPath)
        {
            // The user may have allowed edits to this one file for the session. That covers edits to the prompt only:
            // one that changes the header (name, aliases, role, handoff, order) is asked about again.
            var granted = _fileGrants.ContainsKey(personaPath);
            if (granted && !Context.PersonaFileEdit.ChangesHeader(tool.Name, personaPath, input))
                return true;
            return await PromptForApproval(tool, inputSummary,
                granted
                    ? "This edit changes the persona's header (name, aliases, role, handoff, order, tools or deny), so it is asked about even though edits to this file were allowed."
                    : "This writes a persona (superprompt) file. A persona's prompt controls the conversation once it is used or handed to.",
                ct, askEveryTime: true, fileGrantKey: personaPath);
        }

        // Check if user has granted "always allow" for this tool
        if (_alwaysAllowed.ContainsKey(tool.Name))
            return true;

        // Write tools: auto-approve but log
        if (tool.RiskLevel == ToolRiskLevel.Write)
        {
            _output.ToolAutoApproved(tool.Name);
            return true;
        }

        // Destructive tools: always prompt. For shell commands, run the classifier
        // so the prompt can explain *why* the command is risky instead of just
        // showing the generic "Destructive" risk level.
        var dangerReason = tool.Name == "run_shell" &&
            input.ValueKind == JsonValueKind.Object &&
            input.TryGetProperty("command", out var cmd) &&
            cmd.ValueKind == JsonValueKind.String
                ? _shellClassifier.GetDangerReason(cmd.GetString()!)
                : null;

        return await PromptForApproval(tool, inputSummary, dangerReason, ct);
    }

    /// <summary>
    /// Check if a shell command is dangerous and requires extra approval.
    /// </summary>
    public bool IsShellCommandDangerous(string command) =>
        _shellClassifier.IsDangerous(command);

    /// <summary>The file tools that take a <c>path</c> and change what is at it.</summary>
    private static readonly HashSet<string> FileWriters = new(StringComparer.OrdinalIgnoreCase) { "write_file", "edit_file" };

    /// <summary>Persona files the user allowed prompt-only edits to for this session, by full path.</summary>
    private readonly ConcurrentDictionary<string, byte> _fileGrants = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The full path of the persona file a file-writing tool call would change, or null if it is not one.</summary>
    private static string? PersonaFileWritten(ITool tool, JsonElement input) =>
        FileWriters.Contains(tool.Name)
        && input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty("path", out var path)
        && path.ValueKind == JsonValueKind.String
            ? Context.PersonaRegistry.PersonaFilePath(path.GetString())
            : null;

    private async Task<bool> PromptForApproval(ITool tool, string inputSummary, string? dangerReason, CancellationToken ct,
        bool askEveryTime = false, string? fileGrantKey = null)
    {
        // No one to ask (one-shot/autonomous host, or a library consumer that didn't
        // supply a prompt): deny cleanly. A denied result feeds back to the model as
        // an ordinary tool error it can adapt to -- run_shell is always Destructive,
        // so blocking here instead would hang every un-babysat run that needs a shell.
        if (_prompt is null)
        {
            _output.Warning($"Auto-denied {tool.Name} -- {tool.RiskLevel} tool needs approval but no approval prompt is available.");
            return false;
        }

        await _promptLock.WaitAsync();
        try
        {
            // Another concurrent agent may have just been granted "always allow" for
            // this same tool while this call was waiting for the lock -- re-check so
            // we don't prompt redundantly for something already approved.
            if (!askEveryTime && _alwaysAllowed.ContainsKey(tool.Name))
                return true;

            var answer = await _prompt.PromptAsync(
                new ApprovalRequest(tool.Name, tool.RiskLevel, inputSummary, dangerReason,
                    OfferAlways: fileGrantKey is not null || (!askEveryTime && !AlwaysAsk.Contains(tool.Name)),
                    AlwaysLabel: fileGrantKey is null ? null
                        : $"allow edits to this file's prompt for this session (header changes still ask): {Path.GetFileName(fileGrantKey)}"), ct);

            // "Always allow" is honored for most tools, but not for tools that must be approved every time.
            if (answer == ApprovalResult.AlwaysAllow)
            {
                if (fileGrantKey is not null)
                    _fileGrants[fileGrantKey] = 0; // this one file, prompt-only edits; never the whole tool
                else if (!askEveryTime && !AlwaysAsk.Contains(tool.Name))
                    _alwaysAllowed[tool.Name] = 0;
            }

            return answer != ApprovalResult.Deny;
        }
        finally
        {
            _promptLock.Release();
        }
    }
}
