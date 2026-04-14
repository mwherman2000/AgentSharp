using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class WriteFileToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WriteFileTool _tool;

    public WriteFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _tool = new WriteFileTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task WritesNewFile()
    {
        var filePath = Path.Combine(_tempDir, "new.txt");
        var input = JsonDocument.Parse($$$"""
            {"path": "{{{filePath.Replace("\\", "\\\\")}}}", "content": "hello world"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.True(File.Exists(filePath));
        Assert.Equal("hello world", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task CreatesParentDirectories()
    {
        var filePath = Path.Combine(_tempDir, "sub", "dir", "deep.txt");
        var input = JsonDocument.Parse($$$"""
            {"path": "{{{filePath.Replace("\\", "\\\\")}}}", "content": "nested"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        var filePath = Path.Combine(_tempDir, "existing.txt");
        await File.WriteAllTextAsync(filePath, "old content");

        var input = JsonDocument.Parse($$$"""
            {"path": "{{{filePath.Replace("\\", "\\\\")}}}", "content": "new content"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Equal("new content", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public void HasCorrectMetadata()
    {
        Assert.Equal("write_file", _tool.Name);
        Assert.Equal(AgentSharp.Tools.ToolRiskLevel.Write, _tool.RiskLevel);
    }
}
