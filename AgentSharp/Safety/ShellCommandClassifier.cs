using System.Text.RegularExpressions;

namespace AgentSharp.Safety;

/// <summary>
/// Classifies shell commands as safe or dangerous.
/// Inspired by Claude Code's command classification system.
/// Checks for patterns known to be destructive or risky.
/// </summary>
public partial class ShellCommandClassifier
{
    private static readonly string[] DangerousPatterns =
    [
        @"\brm\s+(-[a-zA-Z]*r[a-zA-Z]*\s+|.*-[a-zA-Z]*f)",  // rm -rf, rm -f
        @"\brm\s+-",                                           // rm with flags
        @"\bsudo\b",                                            // sudo anything
        @"\bchmod\b",                                           // permission changes
        @"\bchown\b",                                           // ownership changes
        @"\bmkfs\b",                                            // format filesystem
        @"\bdd\b",                                              // disk destroyer
        @"\b(shutdown|reboot|halt|poweroff)\b",                 // system control
        @">\s*/dev/sd",                                         // write to disk device
        @"\bcurl\b.*\|\s*(ba)?sh",                              // curl | sh
        @"\bwget\b.*\|\s*(ba)?sh",                              // wget | sh
        @"\bDROP\s+(TABLE|DATABASE)\b",                         // SQL drops
        @"\bDELETE\s+FROM\b",                                   // SQL deletes
        @"\bgit\s+push\s+.*--force",                            // force push
        @"\bgit\s+reset\s+--hard",                              // hard reset
        @"\bgit\s+clean\s+-[a-zA-Z]*f",                         // git clean -f
        @"\bkill\s+-9",                                         // force kill
        @"\bkillall\b",                                         // kill all processes
        @":\(\)\{.*\}",                                         // fork bomb
        @"\bformat\b",                                          // format command
        @"\bnpm\s+publish\b",                                   // npm publish
        @"\bdocker\s+rm\b",                                     // docker remove
        @"\bdocker\s+system\s+prune\b",                         // docker prune
    ];

    private static readonly Regex[] CompiledPatterns = DangerousPatterns
        .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    /// <summary>
    /// Check if a shell command is dangerous.
    /// </summary>
    public bool IsDangerous(string command)
    {
        return CompiledPatterns.Any(p => p.IsMatch(command));
    }

    /// <summary>
    /// Get the specific danger reason for a command, if any.
    /// </summary>
    public string? GetDangerReason(string command)
    {
        if (CompiledPatterns.Any(p => p.IsMatch(command)))
        {
            if (command.Contains("rm ", StringComparison.OrdinalIgnoreCase))
                return "File deletion command detected";
            if (command.Contains("sudo", StringComparison.OrdinalIgnoreCase))
                return "Elevated privilege command detected";
            if (command.Contains("DROP", StringComparison.OrdinalIgnoreCase) ||
                command.Contains("DELETE FROM", StringComparison.OrdinalIgnoreCase))
                return "Destructive SQL command detected";
            if (command.Contains("--force", StringComparison.OrdinalIgnoreCase) ||
                command.Contains("--hard", StringComparison.OrdinalIgnoreCase))
                return "Destructive git command detected";
            if (command.Contains("| sh", StringComparison.OrdinalIgnoreCase) ||
                command.Contains("| bash", StringComparison.OrdinalIgnoreCase))
                return "Remote code execution pattern detected";
            return "Potentially dangerous command detected";
        }
        return null;
    }
}
