namespace AgentSharpLib;

/// <summary>Formats local timestamps with a trailing 3-letter zone indicator such as "MST".</summary>
public static class LocalTime
{
    /// <summary>The zone abbreviation in effect at <paramref name="local"/> ("MST", "MDT", ...).</summary>
    public static string ZoneAbbreviation(DateTime local)
    {
        var tz = TimeZoneInfo.Local;
        var name = tz.IsDaylightSavingTime(local) ? tz.DaylightName : tz.StandardName;
        name = name.Replace("Summer", "Daylight"); // some runtimes name the DST zone "Mountain Summer Time"
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && words[0] == "US") words = words[1..]; // Windows "US Mountain Standard Time"
        var initials = words.Length > 1
            ? new string(words.Where(w => char.IsLetter(w[0])).Select(w => char.ToUpperInvariant(w[0])).ToArray())
            : name;
        return initials.Length is >= 2 and <= 5 ? initials : $"UTC{tz.GetUtcOffset(local):hh}";
    }

    public static string Format(DateTime time, string format)
    {
        var local = time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time;
        return $"{local.ToString(format)} {ZoneAbbreviation(local)}";
    }

    public static string Format(DateTimeOffset t, string format) => Format(t.ToLocalTime().DateTime, format);

    public const string Seconds = "yyyy-MM-dd HH:mm:ss";
    public const string Minutes = "yyyy-MM-dd HH:mm";
}
