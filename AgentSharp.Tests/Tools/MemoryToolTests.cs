using System.Text.Json;
using AgentSharp.Memory;
using AgentSharp.Tools;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class MemoryToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryManager _memory;
    private readonly MemoryTool _tool;

    public MemoryToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _memory = new MemoryManager(_tempDir);
        _tool = new MemoryTool(_memory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_SavesEntryToMemory()
    {
        var input = JsonDocument.Parse("""{"entry": "User prefers dark mode."}""").RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        var saved = _memory.Read();
        Assert.NotNull(saved);
        Assert.Contains("User prefers dark mode.", saved);
    }

    [Fact]
    public void IsWrite()
    {
        Assert.Equal(ToolRiskLevel.Write, _tool.RiskLevel);
    }
}
