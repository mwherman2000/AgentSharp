using AgentSharpLib.Context;

namespace AgentSharpLib.Tools;

/// <summary>
/// Decides which tools the persona now in charge may use. The session sets <see cref="Persona"/> whenever the
/// persona changes (start, /clear, handoff, return, load); the agent loop asks <see cref="Permits"/> twice: when it
/// builds the tool list it sends to the model, and again when the model calls a tool, because a model can call a tool
/// it was never offered. Sub-agents get only the tools the delegating persona may use.
/// <para>Two sources restrict a tool: the persona's own <see cref="ToolPolicy"/> (<c>tools:</c> / <c>deny:</c> in its
/// file) and the tool itself (<see cref="ITool.OnlyPersonas"/>, as with <c>create_digital</c>). The persona's policy is
/// looked up at each call, so editing a persona file narrows a running conversation as soon as the file is reloaded.</para>
/// </summary>
public sealed class ToolScope
{
    private readonly Func<PersonaCatalog> _catalog;

    /// <summary>A scope over the live persona registry.</summary>
    public ToolScope() : this(null) { }

    /// <summary>For tests: look the persona up in another catalog. Null takes the live registry.</summary>
    internal ToolScope(Func<PersonaCatalog>? catalog) => _catalog = catalog ?? (() => PersonaRegistry.Current);

    /// <summary>Canonical key of the persona in charge; empty when there is none (a custom system prompt); null before the session sets it.</summary>
    public string? Persona { get; set; }

    /// <summary>True if the persona in charge may use <paramref name="tool"/>.</summary>
    public bool Permits(ITool tool) => Denial(tool) is null;

    /// <summary>Why the persona in charge may not use <paramref name="tool"/>, or null if it may.</summary>
    public string? Denial(ITool tool)
    {
        var only = tool.OnlyPersonas;
        if (only is not null && (string.IsNullOrEmpty(Persona) || !only.Contains(Persona, StringComparer.OrdinalIgnoreCase)))
            return $"The tool '{tool.Name}' can only be used by {string.Join(", ", only)}.";

        if (string.IsNullOrEmpty(Persona)) return null;
        var catalog = _catalog();
        var persona = catalog.Find(Persona) ?? catalog.Retired.FirstOrDefault(p => p.Key == Persona);
        if (persona?.Tools is { } policy && !policy.Permits(tool.Name))
            return $"The tool '{tool.Name}' is not available to {persona.Name} ({policy.Describe()}).";
        return null;
    }
}
