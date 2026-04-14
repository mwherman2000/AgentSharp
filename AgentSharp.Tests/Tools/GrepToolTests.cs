using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class GrepToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GrepTool _tool;

    public GrepToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _tool = new GrepTool();

        // Create test files
        File.WriteAllText(Path.Combine(_tempDir, "hello.cs"), "public class Hello\n{\n    void World() { }\n}");
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "public class Test\n{\n    void Hello() { }\n}");
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "# Hello World\nThis is a test.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task FindsMatchesAcrossFiles()
    {
        var input = JsonDocument.Parse($$$"""
            {"pattern": "Hello", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("Hello", result.Output);
        Assert.Contains("hello.cs", result.Output);
    }

    [Fact]
    public async Task SupportsRegex()
    {
        var input = JsonDocument.Parse($$$"""
            {"pattern": "void \\w+\\(\\)", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("World", result.Output);
    }

    [Fact]
    public async Task FiltersFilesByPattern()
    {
        var input = JsonDocument.Parse($$$"""
            {"pattern": "Hello", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}", "file_pattern": "*.md"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("readme.md", result.Output);
        Assert.DoesNotContain("hello.cs", result.Output);
    }

    [Fact]
    public async Task ReturnsNoResults_WhenNoMatch()
    {
        var input = JsonDocument.Parse($$$"""
            {"pattern": "ZZZZNOTFOUND", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("No matches", result.Output);
    }

    [Fact]
    public async Task ReturnsError_ForInvalidRegex()
    {
        var input = JsonDocument.Parse($$$"""
            {"pattern": "[invalid(", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.True(result.IsError);
        Assert.Contains("Invalid regex", result.Output);
    }
}
