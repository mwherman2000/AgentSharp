namespace AgentSharp.Memory;

/// <summary>
/// Manages persistent memory via MEMORY.md file.
/// This is a simplified version of Claude Code's memory system.
/// Stores key facts, preferences, and context that persist across sessions.
/// </summary>
public class MemoryManager
{
    private readonly string _memoryPath;

    // One MemoryManager instance is shared by every concurrent sub-agent (they all
    // register the same MemoryTool wrapping the same MemoryManager), so two
    // simultaneous `remember` calls could otherwise race on the File.Exists
    // check-then-write below -- one agent's WriteAllTextAsync (creating the file)
    // could clobber the file the other just created via AppendAllTextAsync, losing
    // an entry. Serializing the whole read-check-write keeps every append intact.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public MemoryManager(string? projectDir = null)
    {
        projectDir ??= Directory.GetCurrentDirectory();
        _memoryPath = Path.Combine(projectDir, "MEMORY.md");
    }

    /// <summary>
    /// Read the current memory contents.
    /// Returns null if no memory file exists.
    /// </summary>
    public string? Read()
    {
        if (!File.Exists(_memoryPath))
            return null;

        return File.ReadAllText(_memoryPath);
    }

    /// <summary>
    /// Append a memory entry.
    /// </summary>
    public async Task AppendAsync(string entry)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var formattedEntry = $"\n## [{timestamp}]\n{entry}\n";

        await _writeLock.WaitAsync();
        try
        {
            if (!File.Exists(_memoryPath))
            {
                await File.WriteAllTextAsync(_memoryPath,
                    $"# Agent Memory\n\nPersistent memory for AgentSharp sessions.\n{formattedEntry}");
            }
            else
            {
                await File.AppendAllTextAsync(_memoryPath, formattedEntry);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Get memory contents formatted for the system prompt.
    /// </summary>
    public string? GetForSystemPrompt()
    {
        var content = Read();
        if (content is null) return null;

        // Truncate if too long to avoid consuming too much context
        const int maxLength = 2000;
        if (content.Length > maxLength)
            content = content[..maxLength] + "\n\n[Memory truncated...]";

        return content;
    }
}
