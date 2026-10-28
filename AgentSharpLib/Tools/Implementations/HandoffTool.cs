using System.Text.Json;
using AgentSharpLib.Context;

namespace AgentSharpLib.Tools.Implementations;

/// <summary>
/// Where <see cref="HandoffTool"/> leaves a handoff request until the turn that made it is
/// over. The tool runs inside the agent loop, so swapping the persona there would orphan the
/// tool call in the history; <see cref="AgentSession"/> takes the request after the turn and
/// applies it before the next prompt.
/// </summary>
public sealed class HandoffQueue
{
    /// <summary>Canonical name of the persona now in charge; set by the session.</summary>
    public string CurrentPersona { get; set; } = "";

    /// <summary>The persona that handed the conversation over, which <c>return</c> goes back
    /// to; null when nothing has been handed over. Set by the session.</summary>
    public string? ReturnTo { get; set; }

    /// <summary>The persona and reason waiting to be applied, or null.</summary>
    public (string Persona, string? Reason)? Pending { get; private set; }

    internal void Request(string persona, string? reason) => Pending = (persona, reason);

    internal (string Persona, string? Reason)? Take()
    {
        var pending = Pending;
        Pending = null;
        return pending;
    }
}

/// <summary>
/// Lets the model hand the conversation, with its full history, to another persona when
/// that persona would do the job better. Takes effect on the user's next prompt.
/// Built by <see cref="AgentBuilder"/> with the session's <see cref="HandoffQueue"/>, so it
/// has no parameterless constructor and is not auto-discovered.
/// </summary>
public sealed class HandoffTool : ToolBase
{
    private readonly HandoffQueue _queue;

    public HandoffTool(HandoffQueue queue) => _queue = queue;

    public override string Name => "handoff";
    public override string Description =>
        "Hand this conversation, with its full history, to another persona, or return it to the persona that " +
        "handed it to you. The switch takes effect on the user's next prompt, so answer the current prompt " +
        "first and tell the user you are handing over; call this at most once per turn. " +
        "Use it when the user asks for a persona by name, or when another persona would clearly serve the " +
        "request better than you; otherwise just answer. " +
        $"The persona argument is one of {string.Join(", ", SystemPromptBuilder.HandoffPersonas)}, or \"{SystemPromptBuilder.ReturnTarget}\" " +
        "(only after the conversation was handed to you) to give it back when your part is done or you cannot help.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            persona = new
            {
                type = "string",
                @enum = SystemPromptBuilder.HandoffPersonas.Append(SystemPromptBuilder.ReturnTarget).ToArray(),
                description = "The persona to hand the conversation to, or \"return\" to give it back to the persona that handed it to you"
            },
            reason = new { type = "string", description = "One sentence on what the user needs and why this persona suits it (or why you are returning); shown to the user and to that persona" }
        },
        required = new[] { "persona", "reason" }
    });

    public override Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var persona = GetRequiredString(input, "persona");
        var reason = GetOptionalString(input, "reason");

        if (_queue.Pending is { } queued)
            return Task.FromResult(ToolResult.Error($"A handoff to {queued.Persona} is already queued for the next prompt."));

        if (persona.Trim().Equals(SystemPromptBuilder.ReturnTarget, StringComparison.OrdinalIgnoreCase))
        {
            if (_queue.ReturnTo is null)
                return Task.FromResult(ToolResult.Error("Nothing to return to: this conversation was not handed to you by another persona."));
            _queue.Request(SystemPromptBuilder.ReturnTarget, reason);
            return Task.FromResult(ToolResult.Success(
                $"Return to {_queue.ReturnTo} queued; it takes effect on the user's next prompt. " +
                "Finish answering the current prompt and tell the user you are handing back. Do not call handoff again."));
        }

        string key;
        try
        {
            key = SystemPromptBuilder.ResolveKey(persona);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(ToolResult.Error(Unavailable(persona)));
        }

        if (!SystemPromptBuilder.HandoffPersonas.Contains(key))
            return Task.FromResult(ToolResult.Error(Unavailable(persona)));
        if (key == _queue.CurrentPersona)
            return Task.FromResult(ToolResult.Error($"You already are {key}. Pick a different persona, or just answer."));

        _queue.Request(key, reason);
        return Task.FromResult(ToolResult.Success(
            $"Handoff to {key} queued; it takes effect on the user's next prompt. " +
            "Finish answering the current prompt and tell the user you are handing over. Do not call handoff again."));
    }

    private static string Unavailable(string persona) =>
        $"Cannot hand off to '{persona}'. Valid personas: {string.Join(", ", SystemPromptBuilder.HandoffPersonas)}.";
}
