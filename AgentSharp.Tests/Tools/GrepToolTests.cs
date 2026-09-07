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
    public async Task SearchesRecursively_IntoSubdirectories()
    {
        var nested = Path.Combine(_tempDir, "deep", "deeper");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "buried.cs"), "// UNIQUE_NEEDLE here");

        var input = JsonDocument.Parse($$$"""
            {"pattern": "UNIQUE_NEEDLE", "path": "{{{_tempDir.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("buried.cs", result.Output);
    }

    [Fact]
    public async Task SearchesFiles_WhenTargetDirLivesUnderABinPath()
    {
        // Same regression as ListFilesTool: a search rooted inside bin/obj (where
        // AgentSharp runs from) must still see the files there.
        var work = Path.Combine(_tempDir, "bin", "Debug", "net8.0", "assets");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "gen.py"), "TOKEN_MARKER = 1");

        var input = JsonDocument.Parse($$$"""
            {"pattern": "TOKEN_MARKER", "path": "{{{work.Replace("\\", "\\\\")}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("gen.py", result.Output);
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
