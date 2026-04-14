using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class EditFileToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EditFileTool _tool;

    public EditFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _tool = new EditFileTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ReplacesExactString()
    {
        var filePath = Path.Combine(_tempDir, "edit.cs");
        await File.WriteAllTextAsync(filePath, "public class Foo\n{\n    int x = 1;\n}");

        var input = JsonDocument.Parse($$$"""
            {
                "path": "{{{filePath.Replace("\\", "\\\\")}}}",
                "old_string": "int x = 1;",
                "new_string": "int x = 42;"
            }
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);
        Assert.False(result.IsError);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("int x = 42;", content);
        Assert.DoesNotContain("int x = 1;", content);
    }

    [Fact]
    public async Task ReturnsError_WhenStringNotFound()
    {
        var filePath = Path.Combine(_tempDir, "edit2.cs");
        await File.WriteAllTextAsync(filePath, "hello world");

        var input = JsonDocument.Parse($$$"""
            {
                "path": "{{{filePath.Replace("\\", "\\\\")}}}",
                "old_string": "not here",
                "new_string": "replacement"
            }
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);
        Assert.True(result.IsError);
        Assert.Contains("not found", result.Output);
    }

    [Fact]
    public async Task ReturnsError_WhenMultipleMatches()
    {
        var filePath = Path.Combine(_tempDir, "edit3.cs");
        await File.WriteAllTextAsync(filePath, "var x = 1;\nvar y = 1;\nvar z = 1;");

        var input = JsonDocument.Parse($$$"""
            {
                "path": "{{{filePath.Replace("\\", "\\\\")}}}",
                "old_string": "= 1;",
                "new_string": "= 2;"
            }
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);
        Assert.True(result.IsError);
        Assert.Contains("3 times", result.Output);
    }

    [Fact]
    public async Task ReturnsError_WhenFileNotFound()
    {
        var input = JsonDocument.Parse("""
            {
                "path": "/nonexistent/file.txt",
                "old_string": "a",
                "new_string": "b"
            }
            """).RootElement;

        var result = await _tool.ExecuteAsync(input);
        Assert.True(result.IsError);
        Assert.Contains("File not found", result.Output);
    }
}
