using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class ListFilesToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ListFilesTool _tool;

    public ListFilesToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));
        File.WriteAllText(Path.Combine(_tempDir, "file1.cs"), "");
        File.WriteAllText(Path.Combine(_tempDir, "file2.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "subdir", "nested.cs"), "");
        _tool = new ListFilesTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ListsFilesInDirectory()
    {
        var input = JsonDocument.Parse($$$"""
            {"path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("file1.cs", result.Output);
        Assert.Contains("file2.txt", result.Output);
        Assert.Contains("subdir", result.Output);
    }

    [Fact]
    public async Task FiltersWithPattern()
    {
        var input = JsonDocument.Parse($$$"""
            {"path": "{{{_tempDir.Replace("\\", "\\\\")}}}", "pattern": "*.cs"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("file1.cs", result.Output);
        Assert.DoesNotContain("file2.txt", result.Output);
    }

    [Fact]
    public async Task ReturnsError_WhenDirNotFound()
    {
        var input = JsonDocument.Parse("""{"path": "/nonexistent/dir"}""").RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.True(result.IsError);
        Assert.Contains("Directory not found", result.Output);
    }
}
