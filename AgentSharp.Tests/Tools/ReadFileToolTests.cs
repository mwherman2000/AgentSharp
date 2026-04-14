using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class ReadFileToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ReadFileTool _tool;

    public ReadFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _tool = new ReadFileTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ReadsFile_WithLineNumbers()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "line 1\nline 2\nline 3");

        var input = JsonDocument.Parse($$$"""{"path": "{{{filePath.Replace("\\", "\\\\")}}}"}""").RootElement;
        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("1\tline 1", result.Output);
        Assert.Contains("2\tline 2", result.Output);
        Assert.Contains("3\tline 3", result.Output);
    }

    [Fact]
    public async Task ReturnsError_WhenFileNotFound()
    {
        var input = JsonDocument.Parse("""{"path": "/nonexistent/file.txt"}""").RootElement;
        var result = await _tool.ExecuteAsync(input);

        Assert.True(result.IsError);
        Assert.Contains("File not found", result.Output);
    }

    [Fact]
    public async Task RespectsOffset_AndLimit()
    {
        var filePath = Path.Combine(_tempDir, "big.txt");
        var lines = Enumerable.Range(1, 100).Select(i => $"line {i}");
        await File.WriteAllLinesAsync(filePath, lines);

        var input = JsonDocument.Parse(
            $$$"""{"path": "{{{filePath.Replace("\\", "\\\\")}}}", "offset": 10, "limit": 5}""").RootElement;
        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("11\tline 11", result.Output);
        Assert.Contains("15\tline 15", result.Output);
        Assert.DoesNotContain("16\t", result.Output);
    }

    [Fact]
    public void HasCorrectMetadata()
    {
        Assert.Equal("read_file", _tool.Name);
        Assert.Equal(AgentSharp.Tools.ToolRiskLevel.ReadOnly, _tool.RiskLevel);
    }
}
