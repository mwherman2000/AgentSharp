namespace AgentSharpLib.Context;

/// <summary>
/// The personas in use by this process. Starts as the built-in personas; <see cref="Watch"/> adds the user and
/// project folders and keeps them current. <see cref="SystemPromptBuilder"/> reads <see cref="Current"/>.
/// </summary>
public static class PersonaRegistry
{
    private static volatile PersonaCatalog _current = PersonaCatalog.BuiltIn;

    /// <summary>The persona set in effect now. Replaced as a whole, so it is safe to read from any thread.</summary>
    public static PersonaCatalog Current => _current;

    /// <summary>Raised, possibly on a thread-pool thread, with the old and new sets after the registry is replaced.</summary>
    public static event Action<PersonaCatalog, PersonaCatalog>? Changed;

    /// <summary>The folder for the current user's persona files, <c>~/.agentsharp/superprompts</c>.</summary>
    public static string DefaultUserDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentsharp", "superprompts");

    /// <summary>The folder for a project's persona files, <c>.agentsharp/superprompts</c> under <paramref name="projectDirectory"/>.</summary>
    public static string ProjectDirectory(string projectDirectory) => Path.Combine(projectDirectory, ".agentsharp", "superprompts");

    /// <summary>
    /// True if <paramref name="path"/> is inside a persona folder: any <c>.agentsharp/superprompts</c> directory, which
    /// covers the user folder and every project folder. Relative paths are resolved against the current directory, as
    /// the file tools do, and the comparison ignores case. The file tools use this to ask the user before they write
    /// a persona file, because a persona's prompt controls the conversation once it is used or handed to.
    /// </summary>
    public static bool IsPersonaFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        string full;
        try { full = Path.GetFullPath(path.Trim()); }
        catch (Exception) { return false; }

        var parts = full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Needs something after the folder name: the folder itself is not a file to write.
        for (var i = 0; i + 2 < parts.Length; i++)
            if (string.Equals(parts[i], ".agentsharp", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[i + 1], "superprompts", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Loads the persona files from the user folder and the project folder now, and again whenever one changes,
    /// until the returned watcher is disposed. The user folder is created if it does not exist (the project folder is not).
    /// </summary>
    public static PersonaWatcher Watch(string? projectDirectory = null, string? userDirectory = null)
    {
        userDirectory ??= DefaultUserDirectory;
        try { Directory.CreateDirectory(userDirectory); }
        catch (Exception) { /* a read-only profile just has no user folder */ }
        return new PersonaWatcher(userDirectory,
            projectDirectory is null ? null : ProjectDirectory(projectDirectory), Set);
    }

    private static readonly object SetLock = new();

    /// <summary>
    /// Replaces the set. A persona that was in the old set and is missing from the new one (its file was deleted or
    /// became invalid) is kept as <see cref="PersonaCatalog.Retired"/>, so a conversation already using it keeps
    /// working on its last good version instead of failing on the next prompt. Nothing is locked on disk.
    /// </summary>
    /// <summary>
    /// <see cref="Watch"/> plus the notices both apps print: files skipped at startup and after each reload, and
    /// what changed ("added x; changed y; removed z"). <paramref name="info"/> and <paramref name="warning"/>
    /// receive plain text and may be called from a thread-pool thread.
    /// </summary>
    public static PersonaWatcher WatchAndReport(string? projectDirectory, Action<string> info, Action<string> warning)
    {
        var watcher = Watch(projectDirectory);
        foreach (var w in Current.Warnings)
            warning($"Superprompt file skipped: {w}");
        Changed += (old, now) =>
        {
            if (now.DescribeChangesFrom(old) is { } what)
                info($"Superprompts reloaded: {what}");
            foreach (var w in now.NewWarningsSince(old))
                warning($"Superprompt file skipped: {w}");
        };
        return watcher;
    }

    internal static void Set(PersonaCatalog catalog)
    {
        PersonaCatalog old;
        lock (SetLock)
        {
            old = _current;
            var live = catalog.Personas.Select(p => p.Key).ToHashSet();
            var retired = old.Personas.Concat(old.Retired)
                .Where(p => !live.Contains(p.Key))
                .GroupBy(p => p.Key).Select(g => g.First())
                .ToList();
            _current = retired.Count == 0 ? catalog : catalog.WithRetired(retired);
            catalog = _current;
        }
        Changed?.Invoke(old, catalog);
    }
}
