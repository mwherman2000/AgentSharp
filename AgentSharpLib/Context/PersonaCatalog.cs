namespace AgentSharpLib.Context;

/// <summary>
/// An immutable set of personas, assembled from three places in this order:
/// the built-in files embedded in AgentSharpLib; the user folder (<c>~/.agentsharp/superprompts</c>), whose files
/// add personas or replace built-in ones; and the project folder (<c>.agentsharp/superprompts</c> in the working
/// directory), whose files may only add new personas. A project cannot replace a persona, because a repository you
/// clone could otherwise change what a persona you trust says. A file that cannot be read or is invalid is skipped
/// and reported in <see cref="Warnings"/>.
/// </summary>
public sealed class PersonaCatalog
{
    private const string ResourcePrefix = "Superprompts/";
    private readonly Dictionary<string, Persona> _lookup;

    private PersonaCatalog(List<Persona> personas, List<string> warnings, IReadOnlyList<Persona>? retired = null)
    {
        Personas = personas.OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
        Warnings = warnings;
        _lookup = new Dictionary<string, Persona>(StringComparer.Ordinal);
        foreach (var p in Personas)
        {
            _lookup[p.Key] = p;
            foreach (var a in p.Aliases) _lookup[a] = p;
        }
        // Retired personas resolve only where no live persona has the name.
        Retired = retired ?? [];
        foreach (var p in Retired)
        {
            _lookup.TryAdd(p.Key, p);
            foreach (var a in p.Aliases) _lookup.TryAdd(a, p);
        }
        Keys = Personas.Select(p => p.Key).ToArray();
        HandoffKeys = Personas.Where(p => p.IsHandoffTarget).Select(p => p.Key).ToArray();
    }

    /// <summary>All personas, default first.</summary>
    public IReadOnlyList<Persona> Personas { get; }
    /// <summary>The keys of <see cref="Personas"/>, default first.</summary>
    public IReadOnlyList<string> Keys { get; }
    /// <summary>The keys of the personas the conversation can be handed to.</summary>
    public IReadOnlyList<string> HandoffKeys { get; }
    /// <summary>Files that were skipped, and why. Empty when everything loaded.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Personas whose file was deleted or became invalid while the app ran. They still resolve (see <see cref="Find"/>),
    /// so a conversation already using one carries on with its last good version, but they are not in
    /// <see cref="Personas"/>, <see cref="Keys"/> or <see cref="HandoffKeys"/>, so they are not offered or handed to.
    /// A persona stops being retired when a file with its name comes back.
    /// </summary>
    public IReadOnlyList<Persona> Retired { get; }

    /// <summary>A copy of this catalog that also keeps <paramref name="retired"/> resolvable.</summary>
    internal PersonaCatalog WithRetired(IReadOnlyList<Persona> retired) =>
        new(Personas.ToList(), Warnings.ToList(), retired);

    /// <summary>The persona with this key or alias (case-insensitive), or null. Includes <see cref="Retired"/> ones.</summary>
    public Persona? Find(string name) => _lookup.GetValueOrDefault(name.Trim().ToLowerInvariant());

    private static readonly Lazy<PersonaCatalog> BuiltInOnly = new(() => Load(null, null));

    /// <summary>The built-in personas only.</summary>
    public static PersonaCatalog BuiltIn => BuiltInOnly.Value;

    /// <summary>Builds a catalog from the built-in files plus the files in the two folders (either may be null or missing).</summary>
    public static PersonaCatalog Load(string? userDirectory, string? projectDirectory)
    {
        var warnings = new List<string>();
        var byKey = new Dictionary<string, Persona>(StringComparer.Ordinal);

        var assembly = typeof(PersonaCatalog).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            Add(byKey, warnings, Path.GetFileNameWithoutExtension(resource), reader.ReadToEnd(),
                PersonaSource.BuiltIn, resource, canReplace: false);
        }
        LoadFolder(userDirectory, PersonaSource.User, canReplace: true, byKey, warnings);
        LoadFolder(projectDirectory, PersonaSource.Project, canReplace: false, byKey, warnings);
        return new PersonaCatalog(byKey.Values.ToList(), warnings);
    }

    private static void LoadFolder(string? directory, PersonaSource source, bool canReplace,
        Dictionary<string, Persona> byKey, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                text = reader.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{path}: could not be read ({ex.Message}).");
                continue;
            }
            Add(byKey, warnings, Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), text, source, path, canReplace);
        }
    }

    private static void Add(Dictionary<string, Persona> byKey, List<string> warnings, string key, string text,
        PersonaSource source, string path, bool canReplace)
    {
        if (!PersonaFile.TryParse(key, text, source, path, out var persona, out var error))
        {
            warnings.Add($"{path}: {error}");
            return;
        }
        var existing = byKey.GetValueOrDefault(key);
        if (existing is not null && !canReplace)
        {
            warnings.Add($"{path}: '{key}' already exists ({existing.Source}) and a {source} file cannot replace it.");
            return;
        }
        // A key or alias must not collide with any other persona's key or alias.
        foreach (var name in persona!.Aliases.Prepend(key))
        {
            var clash = byKey.Values.FirstOrDefault(o => o.Key != key && (o.Key == name || o.Aliases.Contains(name)));
            if (clash is not null)
            {
                warnings.Add($"{path}: '{name}' is already used by '{clash.Key}'.");
                return;
            }
        }
        var order = persona.Order != PersonaFile.OrderUnset ? persona.Order : existing?.Order ?? PersonaFile.DefaultNewOrder;
        byKey[key] = persona with { Order = order };
    }
}
