using AgentSharp.Memory;

namespace AgentSharp.Tests.Memory;

public class MemoryManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryManager _manager;

    public MemoryManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _manager = new MemoryManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Read_ReturnsNull_WhenNoMemoryFileExists()
    {
        Assert.Null(_manager.Read());
    }

    [Fact]
    public async Task AppendAsync_CreatesFile_AndIncludesEntry()
    {
        await _manager.AppendAsync("User prefers terse commit messages.");

        var content = _manager.Read();
        Assert.NotNull(content);
        Assert.Contains("User prefers terse commit messages.", content);
    }

    [Fact]
    public async Task AppendAsync_Twice_AppendsBothEntries()
    {
        await _manager.AppendAsync("First fact.");
        await _manager.AppendAsync("Second fact.");

        var content = _manager.Read();
        Assert.Contains("First fact.", content);
        Assert.Contains("Second fact.", content);
    }

    [Fact]
    public void GetForSystemPrompt_ReturnsNull_WhenNoMemoryFileExists()
    {
        Assert.Null(_manager.GetForSystemPrompt());
    }

    [Fact]
    public async Task GetForSystemPrompt_ReturnsContent_WhenUnderLimit()
    {
        await _manager.AppendAsync("A short fact.");

        var forPrompt = _manager.GetForSystemPrompt();
        Assert.NotNull(forPrompt);
        Assert.Contains("A short fact.", forPrompt);
    }

    [Fact]
    public async Task GetForSystemPrompt_Truncates_WhenOverLimit()
    {
        await _manager.AppendAsync(new string('x', 3000));

        var forPrompt = _manager.GetForSystemPrompt();
        Assert.NotNull(forPrompt);
        Assert.Contains("[Memory truncated...]", forPrompt);
    }
}
