using System.Text;
using System.Text.Json;
using AgentSharpLib.Context;

namespace AgentSharpLib.Tools.Implementations;

/// <summary>
/// Creates a persona (a "digital" with its own prompt) as a file in the user folder or the project folder, and
/// registers it so it can be used in the same session. It has no judgment: it validates, writes and registers
/// what it is given. The Alchemist persona does the designing. It is <see cref="ToolRiskLevel.Destructive"/>, so
/// the user always sees the full persona and approves it before anything is written, and the session
/// "always allow" answer does not skip that (see <c>ApprovalGate</c>).
/// <para>A created persona starts as not a handoff target and never replaces an existing persona or file.
/// To let other personas hand off to it, the user sets <c>handoff: true</c> in its file.</para>
/// </summary>
public class CreateDigitalTool : ToolBase
{
    /// <summary>Most created (user and project) personas that may exist at once; each handoff target is described in every prompt.</summary>
    public const int MaxCreatedPersonas = 20;
    /// <summary>Longest prompt accepted, in characters.</summary>
    public const int MaxPromptLength = 100_000;

    private readonly string? _userDirectory;
    private readonly string? _projectDirectory;
    private readonly Func<PersonaCatalog> _current;
    private readonly Action<PersonaCatalog> _publish;

    /// <summary>The tool as the registry discovers it: the real user and project folders and the live registry.</summary>
    public CreateDigitalTool() : this(null, null, null, null) { }

    /// <summary>For tests: choose the folders and where the reloaded set goes. Null arguments take the real ones.</summary>
    internal CreateDigitalTool(string? userDirectory, string? projectDirectory,
        Func<PersonaCatalog>? current, Action<PersonaCatalog>? publish)
    {
        _userDirectory = userDirectory;
        _projectDirectory = projectDirectory;
        _current = current ?? (() => PersonaRegistry.Current);
        _publish = publish ?? PersonaRegistry.Set;
    }

    public override string Name => "create_digital";
    public override string Description =>
        "Create a new persona from a finished definition: writes it as a file and makes it available in this session. " +
        "The user is shown the complete persona and must approve it first. Use it only after the user has seen the full " +
        "prompt and agreed to it. The new persona is not a handoff target; the user can start it with /clear <nickname>. " +
        "It never replaces an existing persona.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Destructive;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            nickname = new { type = "string", description = "Short name used to select the persona: lower case letters, digits, '-' and '_', at most 32 characters. It becomes the file name. Must not be in use." },
            name = new { type = "string", description = "Display name on one line, for example \"Tess Pando\"" },
            role = new { type = "string", description = "One line on what the persona is for. The other personas read this when deciding whether to hand over to it." },
            prompt = new { type = "string", description = "The full persona prompt, starting with the role in the second person (\"You are ...\")" },
            aliases = new { type = "array", items = new { type = "string" }, description = "Optional other names that select the persona, with the same rules as the nickname" },
            scope = new { type = "string", @enum = new[] { "user", "project" }, description = "\"user\" (default) saves it for this user in every project; \"project\" saves it in this project's .agentsharp/superprompts folder" }
        },
        required = new[] { "nickname", "name", "role", "prompt" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var nickname = GetRequiredString(input, "nickname").Trim().ToLowerInvariant();
        var name = GetRequiredString(input, "name").Trim();
        var role = GetRequiredString(input, "role").Trim();
        var prompt = GetRequiredString(input, "prompt");
        var scope = (GetOptionalString(input, "scope") ?? "user").Trim().ToLowerInvariant();

        var aliases = new List<string>();
        if (input.TryGetProperty("aliases", out var aliasElement) && aliasElement.ValueKind == JsonValueKind.Array)
            foreach (var a in aliasElement.EnumerateArray())
                if (a.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(a.GetString()))
                    aliases.Add(a.GetString()!.Trim().ToLowerInvariant());

        var problem = Validate(nickname, name, role, prompt, scope, aliases);
        if (problem is not null)
            return ToolResult.Error(problem);

        var directory = scope == "project"
            ? _projectDirectory ?? PersonaRegistry.ProjectDirectory(Directory.GetCurrentDirectory())
            : _userDirectory ?? PersonaRegistry.DefaultUserDirectory;
        var path = Path.Combine(directory, nickname + ".md");
        if (File.Exists(path))
            return ToolResult.Error($"A file already exists at {path}. Choose another nickname; create_digital never overwrites.");

        var text = new StringBuilder()
            .Append("---\n")
            .Append("name: ").Append(name).Append('\n')
            .Append("aliases: ").Append(string.Join(", ", aliases)).Append('\n')
            .Append("role: ").Append(role).Append('\n')
            .Append("handoff: false\n")
            .Append("---\n")
            .Append(prompt.Replace("\r\n", "\n").Replace('\r', '\n'))
            .ToString();

        // Check that the file reads back as a persona before it is written.
        if (!PersonaFile.TryParse(nickname, text, scope == "project" ? PersonaSource.Project : PersonaSource.User, path, out _, out var error))
            return ToolResult.Error($"The persona file would not be valid: {error}");

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(tempPath, text, new UTF8Encoding(false), ct);
            File.Move(tempPath, path, overwrite: false);
        }
        catch (OperationCanceledException)
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            try { File.Delete(tempPath); } catch { }
            return ToolResult.Error($"Error writing the persona file: {ex.Message}");
        }

        // Register it now rather than waiting for the file watcher.
        var catalog = PersonaCatalog.Load(_userDirectory ?? PersonaRegistry.DefaultUserDirectory,
            _projectDirectory ?? PersonaRegistry.ProjectDirectory(Directory.GetCurrentDirectory()));
        _publish(catalog);

        if (catalog.Find(nickname) is not { } created || created.Path != path)
        {
            var why = catalog.Warnings.FirstOrDefault(w => w.StartsWith(path, StringComparison.Ordinal));
            return ToolResult.Error($"The file was written to {path} but the persona did not load{(why is null ? "." : $": {why}")}");
        }

        return ToolResult.Success(
            $"Created persona '{nickname}' ({name}) at {path}. It is available now: the user starts it with /clear {nickname}. " +
            "It is not a handoff target; to allow handoffs to it, the user sets handoff: true in the file.");
    }

    private string? Validate(string nickname, string name, string role, string prompt, string scope, List<string> aliases)
    {
        if (!PersonaFile.IsValidName(nickname))
            return $"Invalid nickname '{nickname}': use lower case letters, digits, '-' and '_', starting with a letter or digit, at most 32 characters.";
        if (scope is not ("user" or "project"))
            return $"Invalid scope '{scope}': use \"user\" or \"project\".";
        if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl))
            return "The name must be one line of at most 100 characters.";
        if (role.Length == 0 || role.Length > 300 || role.Any(char.IsControl))
            return "The role must be one line of at most 300 characters.";
        if (string.IsNullOrWhiteSpace(prompt))
            return "The prompt is empty.";
        if (prompt.Length > MaxPromptLength)
            return $"The prompt is {prompt.Length} characters; the limit is {MaxPromptLength}.";

        foreach (var alias in aliases)
            if (!PersonaFile.IsValidName(alias))
                return $"Invalid alias '{alias}': use lower case letters, digits, '-' and '_', at most 32 characters.";

        var catalog = _current();
        foreach (var n in aliases.Prepend(nickname))
            if (catalog.Find(n) is { } existing)
                return $"'{n}' is already used by the persona '{existing.Key}'. Choose another name.";

        var created = catalog.Personas.Count(p => p.Source != PersonaSource.BuiltIn);
        if (created >= MaxCreatedPersonas)
            return $"There are already {created} created personas; the limit is {MaxCreatedPersonas}. Remove one first.";
        return null;
    }
}
