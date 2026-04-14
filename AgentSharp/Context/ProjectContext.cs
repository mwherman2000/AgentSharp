using System.Diagnostics;

namespace AgentSharp.Context;

/// <summary>
/// Detects and gathers project context information:
/// - Git repository details (branch, status)
/// - CLAUDE.md / AGENTS.md project instructions
/// - File tree for the project
///
/// This context is injected into the system prompt so the LLM
/// understands the project it's working in.
/// </summary>
public class ProjectContext
{
    public string WorkingDirectory { get; }
    public string? GitBranch { get; private set; }
    public bool IsGitRepo { get; private set; }
    public string? ProjectInstructions { get; private set; }
    public string? FileTreeSummary { get; private set; }

    public ProjectContext(string? workingDirectory = null)
    {
        WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Gather all project context. Call this once at startup
    /// and again when the working directory changes.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(
            DetectGitAsync(ct),
            LoadProjectInstructionsAsync(ct),
            BuildFileTreeAsync(ct)
        );
    }

    private async Task DetectGitAsync(CancellationToken ct)
    {
        try
        {
            var result = await RunGitAsync("rev-parse --abbrev-ref HEAD", ct);
            if (result.exitCode == 0)
            {
                IsGitRepo = true;
                GitBranch = result.stdout.Trim();
            }
        }
        catch
        {
            IsGitRepo = false;
        }
    }

    private Task LoadProjectInstructionsAsync(CancellationToken ct)
    {
        // Look for project instruction files (in priority order)
        var candidates = new[] { "CLAUDE.md", "AGENTS.md", ".claude/instructions.md" };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(WorkingDirectory, candidate);
            if (File.Exists(path))
            {
                ProjectInstructions = File.ReadAllText(path);
                return Task.CompletedTask;
            }
        }

        ProjectInstructions = null;
        return Task.CompletedTask;
    }

    private Task BuildFileTreeAsync(CancellationToken ct)
    {
        try
        {
            var lines = new List<string>();
            BuildTree(WorkingDirectory, "", lines, maxDepth: 3, maxFiles: 100);
            FileTreeSummary = string.Join("\n", lines);
        }
        catch
        {
            FileTreeSummary = "[Unable to read file tree]";
        }
        return Task.CompletedTask;
    }

    private static void BuildTree(string dir, string indent, List<string> lines, int maxDepth, int maxFiles, int depth = 0)
    {
        if (depth >= maxDepth || lines.Count >= maxFiles)
            return;

        var dirName = Path.GetFileName(dir);
        if (dirName is ".git" or "node_modules" or "bin" or "obj" or ".vs" or ".idea" or "__pycache__")
            return;

        try
        {
            var dirs = Directory.GetDirectories(dir)
                .Where(d => !ShouldSkipDir(Path.GetFileName(d)))
                .OrderBy(d => d)
                .ToList();

            var files = Directory.GetFiles(dir)
                .OrderBy(f => f)
                .ToList();

            foreach (var subDir in dirs)
            {
                if (lines.Count >= maxFiles) break;
                lines.Add($"{indent}{Path.GetFileName(subDir)}/");
                BuildTree(subDir, indent + "  ", lines, maxDepth, maxFiles, depth + 1);
            }

            foreach (var file in files)
            {
                if (lines.Count >= maxFiles) break;
                lines.Add($"{indent}{Path.GetFileName(file)}");
            }
        }
        catch
        {
            // Skip directories we can't access
        }
    }

    private static bool ShouldSkipDir(string name) =>
        name is ".git" or "node_modules" or "bin" or "obj" or ".vs" or ".idea"
            or "__pycache__" or ".cache" or "packages";

    private async Task<(int exitCode, string stdout)> RunGitAsync(string args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, stdout);
    }
}
