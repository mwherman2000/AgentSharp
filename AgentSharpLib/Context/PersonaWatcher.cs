namespace AgentSharpLib.Context;

/// <summary>
/// Loads a <see cref="PersonaCatalog"/> from the user and project folders when it is created, then watches both
/// folders and loads it again, after a short pause so an editor's several writes count as one, whenever a persona
/// file is added, changed, renamed or deleted. Each result is passed to <c>onLoaded</c> (the first one on the
/// calling thread, later ones on a thread-pool thread). A folder that does not exist when the watcher starts is not watched.
/// </summary>
public sealed class PersonaWatcher : IDisposable
{
    private readonly string? _userDirectory;
    private readonly string? _projectDirectory;
    private readonly Action<PersonaCatalog> _onLoaded;
    private readonly TimeSpan _debounce;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Timer _timer;
    private readonly object _lock = new();
    private bool _disposed;

    public PersonaWatcher(string? userDirectory, string? projectDirectory, Action<PersonaCatalog> onLoaded, TimeSpan? debounce = null)
    {
        _userDirectory = userDirectory;
        _projectDirectory = projectDirectory;
        _onLoaded = onLoaded;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(300);
        _timer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);

        Reload();
        foreach (var dir in new[] { userDirectory, projectDirectory })
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                _watchers.Add(Watch(dir));
    }

    private FileSystemWatcher Watch(string directory)
    {
        var w = new FileSystemWatcher(directory, "*.md")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        w.Created += (_, _) => Schedule();
        w.Changed += (_, _) => Schedule();
        w.Deleted += (_, _) => Schedule();
        w.Renamed += (_, _) => Schedule();
        w.Error += (_, _) => Schedule();
        return w;
    }

    private void Schedule()
    {
        lock (_lock)
            if (!_disposed) _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
    }

    private void Reload()
    {
        lock (_lock)
            if (_disposed) return;
        try
        {
            _onLoaded(PersonaCatalog.Load(_userDirectory, _projectDirectory));
        }
        catch (Exception)
        {
            // A failed reload keeps the previous catalog; the next file event tries again.
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        foreach (var w in _watchers) w.Dispose();
        _timer.Dispose();
    }
}
