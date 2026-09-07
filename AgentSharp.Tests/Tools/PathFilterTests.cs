using AgentSharp.Tools;

namespace AgentSharp.Tests.Tools;

public class PathFilterTests
{
    // PathFilter is pure string logic -- no file access -- but the segments must be
    // separated the way the current OS separates them, so anchor on a real temp path.
    private static readonly string Base = Path.GetTempPath();

    private static string P(params string[] parts) => Path.Combine(Base, Path.Combine(parts));

    [Fact]
    public void SkipsBuildAndVcsDirs_BelowANormalRoot()
    {
        var root = P("src", "proj");

        Assert.True(PathFilter.ShouldSkip(P("src", "proj", "bin", "Debug", "app.json"), root));
        Assert.True(PathFilter.ShouldSkip(P("src", "proj", "obj", "x.cs"), root));
        Assert.True(PathFilter.ShouldSkip(P("src", "proj", "node_modules", "pkg", "index.js"), root));
        Assert.True(PathFilter.ShouldSkip(P("src", "proj", ".git", "HEAD"), root));
    }

    [Fact]
    public void DoesNotSkip_OrdinaryFiles()
    {
        var root = P("src", "proj");

        Assert.False(PathFilter.ShouldSkip(P("src", "proj", "src", "Program.cs"), root));
        Assert.False(PathFilter.ShouldSkip(P("src", "proj", "README.md"), root));
    }

    [Fact]
    public void DoesNotSkipAnything_WhenTheRootItselfIsInsideASkippedDir()
    {
        // The exact shape from the stuck-agent report: AgentSharp runs from its own
        // bin/Debug/net8.0 and the agent reads and writes scratch files right there.
        // A file it just wrote must stay visible to its own list_files/grep.
        var root = P("repo", "AgentSharp", "bin", "Debug", "net8.0", "gtm_report_assets");

        Assert.False(PathFilter.ShouldSkip(P("repo", "AgentSharp", "bin", "Debug", "net8.0", "gtm_report_assets", "build_report.py"), root));
        Assert.False(PathFilter.ShouldSkip(P("repo", "AgentSharp", "bin", "Debug", "net8.0", "gtm_report_assets", "data", "report_data.json"), root));
        Assert.False(PathFilter.ShouldSkip(root, root));
    }

    [Fact]
    public void FiltersNothing_OnceTheRootIsInsideBuildOutput_IncludingNestedSkipDirs()
    {
        // Deliberately targeting a location inside build output means "show me
        // everything here" -- the filter switches fully off rather than trying to
        // second-guess which nested directories are still noise. Bounded anyway by
        // the tools' own result caps and binary-extension skip.
        var root = P("repo", "app", "bin", "Debug", "work");

        Assert.False(PathFilter.ShouldSkip(
            P("repo", "app", "bin", "Debug", "work", "node_modules", "left-pad", "index.js"), root));
    }
}
