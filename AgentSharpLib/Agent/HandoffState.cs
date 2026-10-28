namespace AgentSharpLib.Agent;

/// <summary>
/// Who handed a conversation to whom, as saved with a session so a loaded conversation can
/// still be returned: <see cref="Stack"/> holds the personas that handed it over, the most
/// recent last, and <see cref="Persona"/> is the persona in charge now.
/// </summary>
public sealed record HandoffState(IReadOnlyList<string> Stack, string Persona);
