namespace AgentSharp.Memory;

/// <summary>
/// Manages persistent memory via MEMORY.md file.
/// This is a simplified version of Claude Code's memory system.
/// Stores key facts, preferences, and context that persist across sessions.
/// </summary>
public class MemoryManager
{
    private readonly string _memoryPath;

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
