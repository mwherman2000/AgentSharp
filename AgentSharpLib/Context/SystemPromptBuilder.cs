using System.Text;
using AgentSharpLib.Memory;

namespace AgentSharpLib.Context;

/// <summary>
/// Builds the system prompt that guides the LLM's behavior.
/// Assembles context from multiple sources:
/// - Base agent instructions
/// - Project context (git, file tree)
/// - Project instructions (CLAUDE.md)
/// - Available tools summary
/// - Memory contents
/// </summary>
public class SystemPromptBuilder
{
    private readonly ProjectContext _project;
    private readonly MemoryManager? _memory;
    private readonly string _basePrompt;
    private readonly string _personaKey;
    private readonly bool _offerHandoff;
    private readonly string? _handoffContext;
    private readonly string? _returnTo;
    private readonly IReadOnlyList<string> _handoffStack;

    /// <summary>
    /// </summary>
    /// <param name="superPrompt">Selects which base persona/prompt to use, matching the
    /// CLI's --Superprompt argument (case-insensitive): one of <see cref="AvailableSuperPrompts"/>
    /// or an alias. Null or empty uses the default (the first, Andy).</param>
    /// <param name="offerHandoff">Add the list of other personas and when to call the
    /// <c>handoff</c> tool to hand the conversation to one of them.</param>
    /// <param name="handoffContext">Set when this persona has just taken the conversation
    /// over from another one: says who it came from and why, so the earlier turns in the
    /// history are not mistaken for this persona's own.</param>
    /// <param name="returnTo">The persona that handed the conversation to this one, if any.
    /// Adds a "return" option to the handoff section, to hand the conversation back to it.</param>
    /// <param name="handoffStack">The personas that handed the conversation over, oldest first (the
    /// most recent is <paramref name="returnTo"/>). Names them in the prompt so this persona knows who
    /// came before it, without being told which of them wrote which earlier turn.</param>
    public SystemPromptBuilder(ProjectContext project, MemoryManager? memory = null, string? superPrompt = null,
        bool offerHandoff = false, string? handoffContext = null, string? returnTo = null,
        IReadOnlyList<string>? handoffStack = null)
    {
        _returnTo = returnTo;
        _handoffStack = handoffStack ?? [];
        _project = project;
        _memory = memory;
        var persona = Resolve(superPrompt);
        _basePrompt = persona.Prompt;
        _personaKey = persona.Key;
        _offerHandoff = offerHandoff;
        _handoffContext = handoffContext;
    }

    // The personas live one per file (AgentSharpLib/Superprompts/<key>.md, embedded), plus any files in the
    // user and project superprompts folders; PersonaRegistry holds the current set and keeps it up to date.
    // The first persona (lowest order) is the default.
    private static PersonaCatalog Catalog => PersonaRegistry.Current;

    /// <summary>
    /// The valid --Superprompt names (aliases excluded), default first. Use this for help
    /// text and error messages rather than spelling the list out. Reflects persona files
    /// added or removed while the app runs.
    /// </summary>
    public static IReadOnlyList<string> AvailableSuperPrompts => Catalog.Keys;

    /// <summary>
    /// Resolves a --Superprompt name to its base prompt text. Unknown names throw so a
    /// typo on the command line is caught at startup rather than silently defaulting.
    /// </summary>
    public static string ResolveSuperPrompt(string? superPrompt) => Resolve(superPrompt).Prompt;

    /// <summary>
    /// Resolves a --Superprompt name to the persona's display name (e.g. "Andy"),
    /// for the welcome banner. Same defaulting and validation as <see cref="ResolveSuperPrompt"/>.
    /// </summary>
    public static string ResolveAgentName(string? superPrompt) => Resolve(superPrompt).Name;

    /// <summary>
    /// Resolves a --Superprompt name or alias to its canonical name (one of
    /// <see cref="AvailableSuperPrompts"/>), so two spellings of the same persona compare equal.
    /// Same defaulting and validation as <see cref="ResolveSuperPrompt"/>.
    /// </summary>
    public static string ResolveKey(string? superPrompt) => Resolve(superPrompt).Key;

    /// <summary>The key ("ray") of the persona with this full name ("Ray Pando"), or null if none has it.</summary>
    public static string? KeyForName(string? name) =>
        Catalog.Personas.Concat(Catalog.Retired).Where(p => string.Equals(p.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))
               .Select(p => p.Key).FirstOrDefault();

    private static Persona Resolve(string? superPrompt)
    {
        var catalog = Catalog;
        if (string.IsNullOrWhiteSpace(superPrompt))
            return catalog.Personas[0];

        return catalog.Find(superPrompt)
            ?? throw new ArgumentException(
                $"Unknown --Superprompt '{superPrompt}'. Valid options: {string.Join(", ", catalog.Keys)}.");
    }

    /// <summary>
    /// The personas a conversation can be handed to with the <c>handoff</c> tool: those whose file says
    /// <c>handoff: true</c>. A subset of <see cref="AvailableSuperPrompts"/>; the rest can still be selected
    /// with --Superprompt or /clear, just not handed to mid-conversation.
    /// </summary>
    public static IReadOnlyList<string> HandoffPersonas => Catalog.HandoffKeys;

    /// <summary>The value of the <c>handoff</c> tool's <c>persona</c> argument that returns the
    /// conversation to the persona that handed it over, rather than naming a persona.</summary>
    public const string ReturnTarget = "return";

    /// <summary>The "# Handoff" section: the other personas and when to hand over to one.</summary>
    private string BuildHandoffCatalog()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n# Handing the conversation to another persona");
        sb.AppendLine("The `handoff` tool passes this conversation, with its full history, to another persona. " +
                      "The switch takes effect on the user's next prompt, so finish answering this one first and say " +
                      "that you are handing over. Hand off when the user asks for a persona by name, or when your own " +
                      "answer would be clearly worse than theirs; otherwise just answer. Do not hand off to yourself, " +
                      "and do not bounce between personas: a persona that takes over for a task keeps it until the " +
                      "task is done.");
        sb.AppendLine("Personas you can hand off to:");
        foreach (var p in Catalog.Personas.Where(p => p.Key != _personaKey && p.IsHandoffTarget))
            sb.AppendLine($"- {p.Key}: {p.Role}");
        if (_returnTo is not null)
            sb.AppendLine($"- {ReturnTarget}: hand the conversation back to {_returnTo}, who handed it to you; " +
                          "use it when the task that needed you is done, you cannot help, or the user asks to go back");
        return sb.ToString();
    }

    public string Build()
    {
        var sb = new StringBuilder();

        // Base instructions
        sb.AppendLine(_basePrompt);

        // Project context
        sb.AppendLine("\n# Environment");
        sb.AppendLine($"- Working directory: {_project.WorkingDirectory}");
        sb.AppendLine($"- Platform: {GetPlatform()}");
        sb.AppendLine($"- Is git repo: {_project.IsGitRepo}");
        if (_project.GitBranch is not null)
            sb.AppendLine($"- Git branch: {_project.GitBranch}");
        sb.AppendLine($"- Current date: {LocalTime.Format(DateTime.Now, LocalTime.Minutes)}");

        // File tree
        if (_project.FileTreeSummary is not null)
        {
            sb.AppendLine("\n# Project File Tree");
            sb.AppendLine("```");
            sb.AppendLine(_project.FileTreeSummary);
            sb.AppendLine("```");
        }

        // Project instructions (CLAUDE.md)
        if (_project.ProjectInstructions is not null)
        {
            sb.AppendLine("\n# Project Instructions");
            sb.AppendLine(_project.ProjectInstructions);
        }

        // Handoff: who handed this conversation over, and who it can be handed to next.
        // Both go before memory, which ConversationTranscript.ExtractRecalledMemory expects last.
        if (_handoffContext is not null)
        {
            sb.AppendLine("\n# Handoff received");
            sb.AppendLine(_handoffContext);
        }
        if (_handoffStack.Count > 0)
        {
            // Shown even without a handoff note (a loaded conversation has the stack but not the note).
            // By nickname, the spelling the handoff tool takes (people see full names; the model sees these).
            var chain = string.Join(" > ", _handoffStack) + $" > {_personaKey} (you)";
            sb.AppendLine("\n# Handoff stack");
            sb.AppendLine($"This conversation has passed between personas. Handoff stack, oldest first: {chain}. " +
                          "Earlier assistant turns in the history may have been written by any of the others; " +
                          "you do not need to work out which turn was whose.");
        }
        if (_offerHandoff)
            sb.Append(BuildHandoffCatalog());

        // Persistent memory (written via the "remember" tool, read back here)
        var memoryContent = _memory?.GetForSystemPrompt();
        if (memoryContent is not null)
        {
            sb.AppendLine("\n# Memory");
            sb.AppendLine(memoryContent);
        }

        return sb.ToString();
    }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }
}
