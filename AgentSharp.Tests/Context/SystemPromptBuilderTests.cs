using AgentSharp.Context;
using AgentSharp.Memory;

namespace AgentSharp.Tests.Context;

public class SystemPromptBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectContext _project;

    public SystemPromptBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _project = new ProjectContext(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Build_OmitsMemorySection_WhenNoMemoryProvided()
    {
        var prompt = new SystemPromptBuilder(_project).Build();

        Assert.DoesNotContain("# Memory", prompt);
    }

    [Fact]
    public void Build_OmitsMemorySection_WhenMemoryFileDoesNotExist()
    {
        var memory = new MemoryManager(_tempDir);

        var prompt = new SystemPromptBuilder(_project, memory).Build();

        Assert.DoesNotContain("# Memory", prompt);
    }

    [Fact]
    public async Task Build_IncludesMemoryContent_WhenMemoryFileExists()
    {
        var memory = new MemoryManager(_tempDir);
        await memory.AppendAsync("User prefers concise commit messages.");

        var prompt = new SystemPromptBuilder(_project, memory).Build();

        Assert.Contains("# Memory", prompt);
        Assert.Contains("User prefers concise commit messages.", prompt);
    }
}
