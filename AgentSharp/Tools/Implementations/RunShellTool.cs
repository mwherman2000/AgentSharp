using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Executes a shell command and returns its output.
/// Always classified as Destructive -- requires user approval.
/// Supports timeout to prevent runaway processes.
/// </summary>
public partial class RunShellTool : ToolBase
{
    public override string Name => "run_shell";
    public override string Description =>
        "Execute a shell command and return its stdout and stderr. " +
        "Commands run in the current working directory. " +
        "Use timeout_ms to limit execution time (default: 30 seconds). " +
        "This tool requires user approval before execution.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.Destructive;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            command = new { type = "string", description = "The shell command to execute" },
            working_directory = new { type = "string", description = "Working directory for the command. Default: current directory" },
            timeout_ms = new { type = "integer", description = "Timeout in milliseconds. Default: 30000 (30 seconds)" }
        },
        required = new[] { "command" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = GetRequiredString(input, "command");
        var workingDir = GetOptionalString(input, "working_directory") ?? Directory.GetCurrentDirectory();
        // Reported back in every result so a model can see the shell's actual
        // starting directory. The file tools resolve relative paths against this
        // same directory -- but a `cd` *inside* the command string moves the shell
        // somewhere else for the rest of the command, so a file written by
        // write_file("x") and then looked for by a cd'd script won't line up.
        // Surfacing both halves lets the model spot that instead of concluding the
        // write silently failed and retrying it forever.
        var workingDirDisplay = Path.GetFullPath(workingDir);
        // Clamped to a sane minimum so a bad/negative timeout_ms from the model can't
        // reach CancellationTokenSource.CancelAfter, which throws ArgumentOutOfRangeException
        // for any value < -1 -- previously that exception fell into the generic catch
        // below, which returns an error but never kills the process already started
        // on line below, orphaning it with nothing left tracking it.
        var timeoutMs = Math.Max(GetOptionalInt(input, "timeout_ms", 30_000), 1);

        Process? process = null;
        try
        {
            // Determine shell based on OS
            var (shell, shellArgs) = GetShellCommand(command);

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Read stdout and stderr concurrently
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }

                // Distinguish the caller's cancellation (Ctrl+C) from this method's
                // own timeout (cts.CancelAfter): only the former should abort the
                // turn -- the latter is a normal, reportable tool failure.
                if (ct.IsCancellationRequested)
                    throw;

                return ToolResult.Error(
                    $"Working directory: {workingDirDisplay}\nCommand timed out after {timeoutMs}ms");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var output = new System.Text.StringBuilder();
            output.AppendLine($"Working directory: {workingDirDisplay}");
            output.AppendLine($"Exit code: {process.ExitCode}");

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                output.AppendLine("\n--- stdout ---");
                output.AppendLine(TruncateOutput(stdout));
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                output.AppendLine("\n--- stderr ---");
                output.AppendLine(TruncateOutput(stderr));
            }

            return process.ExitCode == 0
                ? ToolResult.Success(output.ToString())
                : ToolResult.Error(output.ToString());
        }
        catch (OperationCanceledException)
        {
            // Propagate so the parent agent loop stops the turn instead of
            // reporting cancellation to the LLM as a failed command.
            throw;
        }
        catch (Exception ex)
        {
            // Belt and suspenders alongside the timeout-path Kill() above: any other
            // unexpected failure after Start() (e.g. a stdout/stderr read fault) must
            // not leave the child process running with nothing left tracking it.
            // HasExited itself throws if the process was never started, so the check
            // and the kill both need to be inside the swallowing try.
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            }
            return ToolResult.Error($"Error executing command: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Determine the appropriate shell and arguments for the current OS.
    /// Windows: prefers bash (Git Bash, WSL) then falls back to cmd.exe
    /// macOS: uses /bin/zsh (default shell since Catalina)
    /// Linux: uses /bin/bash
    /// </summary>
    internal static (string shell, string args) GetShellCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            var bashPath = FindBashOnWindows();
            if (bashPath is not null)
            {
                var escaped = NormalizeNullDevice(command, usesPosixShell: true).Replace("\"", "\\\"");
                return (bashPath, $"-c \"{escaped}\"");
            }
            return ("cmd.exe", $"/c {NormalizeNullDevice(command, usesPosixShell: false)}");
        }

        // macOS/Linux shells only understand /dev/null; cmd.exe's "nul" would
        // otherwise be created as a literal file if the model emits Windows syntax.
        var posixCommand = NormalizeNullDevice(command, usesPosixShell: true).Replace("\"", "\\\"");

        if (OperatingSystem.IsMacOS())
        {
            // macOS default shell is zsh since Catalina (10.15)
            if (File.Exists("/bin/zsh"))
                return ("/bin/zsh", $"-c \"{posixCommand}\"");
            return ("/bin/bash", $"-c \"{posixCommand}\"");
        }

        // Linux and others
        return ("/bin/bash", $"-c \"{posixCommand}\"");
    }

    /// <summary>
    /// Rewrites the null-device target of a redirection (e.g. "&gt; nul" or "&gt; /dev/null")
    /// to match the device name understood by the shell that will actually run the command.
    /// Only touches redirection targets (preceded by '&gt;' or '&amp;'), so a literal file
    /// named "nul" referenced elsewhere in the command (e.g. "rm nul") is left alone.
    /// </summary>
    internal static string NormalizeNullDevice(string command, bool usesPosixShell)
    {
        return usesPosixShell
            ? NullDeviceRedirectToNul().Replace(command, "$1/dev/null")
            : NullDeviceRedirectToDevNull().Replace(command, "$1nul");
    }

    [GeneratedRegex(@"([>&]\s*)nul\b", RegexOptions.IgnoreCase)]
    private static partial Regex NullDeviceRedirectToNul();

    [GeneratedRegex(@"([>&]\s*)/dev/null\b", RegexOptions.IgnoreCase)]
    private static partial Regex NullDeviceRedirectToDevNull();

    private static string? FindBashOnWindows()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Git\bin\bash.exe"),
            @"C:\Windows\System32\bash.exe", // WSL
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Keeps the head *and* the tail of over-long output rather than just the head.
    /// Build errors, failing test summaries and stack traces put the line that
    /// actually explains the failure at the very end -- a head-only cut hands the
    /// model a wall of progress noise with the real error dropped, and it can't ask
    /// for the rest. Splitting the budget keeps both the start and that trailing
    /// diagnostic.
    /// </summary>
    internal static string TruncateOutput(string output, int maxLength = 10_000)
    {
        if (output.Length <= maxLength)
            return output;

        var headLen = maxLength * 3 / 5;
        var tailLen = maxLength - headLen;
        var omitted = output.Length - headLen - tailLen;
        return output[..headLen]
            + $"\n\n[... {omitted} characters omitted -- showing first {headLen} and last {tailLen} ...]\n\n"
            + output[^tailLen..];
    }
}
