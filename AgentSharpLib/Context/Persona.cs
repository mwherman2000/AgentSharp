namespace AgentSharpLib.Context;

/// <summary>Where a persona's definition came from. Later sources may override earlier ones (see <see cref="PersonaCatalog"/>).</summary>
public enum PersonaSource
{
    /// <summary>A file embedded in AgentSharpLib (the <c>Superprompts</c> folder of the source tree).</summary>
    BuiltIn,
    /// <summary>A file in the user folder, <c>~/.agentsharp/superprompts</c>.</summary>
    User,
    /// <summary>A file in the project folder, <c>.agentsharp/superprompts</c> under the working directory.</summary>
    Project,
}

/// <summary>One persona (superprompt): the name it is selected by, who it is, what it is for, and its prompt.</summary>
/// <param name="Key">The --Superprompt name, taken from the file name without its extension.</param>
/// <param name="Aliases">Other accepted spellings of the key.</param>
/// <param name="Name">The display name, such as "Andy Pando".</param>
/// <param name="Prompt">The base system prompt: the file's text after its header.</param>
/// <param name="Role">One line on what the persona is for; shown to the other personas so they can judge when to hand over.</param>
/// <param name="IsHandoffTarget">Whether the conversation can be handed to this persona mid-conversation.</param>
/// <param name="Order">Sort position; the lowest is the default persona.</param>
/// <param name="Source">Which folder the definition came from.</param>
/// <param name="Path">The file it was read from, or the embedded resource name.</param>
/// <param name="Tools">Which tools it may use (the file's <c>tools:</c> and <c>deny:</c> lines); null means all of them.</param>
public sealed record Persona(
    string Key,
    IReadOnlyList<string> Aliases,
    string Name,
    string Prompt,
    string Role,
    bool IsHandoffTarget,
    int Order,
    PersonaSource Source,
    string Path,
    ToolPolicy? Tools = null);
