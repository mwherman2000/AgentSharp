namespace AgentSharp.Tools;

/// <summary>
/// Shared "don't bury the model in build output / VCS metadata" filter for the
/// directory-walking read tools (list_files, grep).
///
/// The skip list is only applied to directories met *incidentally* while walking
/// below the path the caller asked about. When the requested root is itself inside
/// one of these directories -- the normal case when AgentSharp runs from its own
/// bin/Debug/netX.0 and the agent reads and writes scratch files right there --
/// nothing is filtered. Without that carve-out a file the agent just wrote is
/// invisible to its own list_files/grep, and it can spin re-writing it (the
/// write_file -> "it's still not there" -> write_file loop this guards against).
/// </summary>
internal static class PathFilter
{
    private static readonly string[] SkipNames =
        { ".git", "node_modules", "bin", "obj", ".vs", ".idea" };

    /// <summary>
    /// True when <paramref name="fullPath"/> lies under a skipped directory that is
    /// strictly below <paramref name="rootFullPath"/>. Always false when
    /// <paramref name="rootFullPath"/> is itself within a skipped directory -- the
    /// caller deliberately navigated there. Both arguments must be absolute paths.
    /// </summary>
    public static bool ShouldSkip(string fullPath, string rootFullPath)
    {
        if (ContainsSkippedSegment(rootFullPath))
            return false;

        var relative = Path.GetRelativePath(rootFullPath, fullPath);
        return ContainsSkippedSegment(relative);
    }

    private static bool ContainsSkippedSegment(string path)
    {
        foreach (var segment in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (Array.IndexOf(SkipNames, segment) >= 0)
                return true;
        }
        return false;
    }
}
