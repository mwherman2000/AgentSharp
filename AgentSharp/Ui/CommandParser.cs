namespace AgentSharp.Ui;

/// <summary>
/// Parses slash commands in user input.
/// Commands start with '/' and provide REPL control.
/// </summary>
public enum CommandType
{
    None,       // Regular message, not a command
    Help,
    Exit,
    Clear,
    Save,
    Load,
    Sessions,
    Model,
    Status,
    Memory,
    History,
    Tools,
    Request,
    Sync,
    Transcript,
    Jaeger,
    Unknown
}

public record ParsedCommand(CommandType Type, string? Argument = null);

public static class CommandParser
{
    public static ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return new ParsedCommand(CommandType.None);

        var parts = input.TrimStart('/').Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        // A bare "/" (or "/" followed only by whitespace) trims/splits down to an
        // empty array -- parts[0] below would throw IndexOutOfRangeException, and
        // with no try/catch around command dispatch that used to take down the
        // entire REPL session over a single stray keystroke.
        if (parts.Length == 0)
            return new ParsedCommand(CommandType.Unknown, "");

        var command = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? parts[1] : null;

        return command switch
        {
            "help" or "h" or "?" => new ParsedCommand(CommandType.Help),
            "exit" or "quit" or "q" => new ParsedCommand(CommandType.Exit),
            "clear" or "cls" => new ParsedCommand(CommandType.Clear),
            "save" => new ParsedCommand(CommandType.Save, argument),
            "load" or "resume" => new ParsedCommand(CommandType.Load, argument),
            "sessions" or "ls" => new ParsedCommand(CommandType.Sessions),
            "model" => new ParsedCommand(CommandType.Model, argument),
            "status" => new ParsedCommand(CommandType.Status),
            "memory" or "mem" => new ParsedCommand(CommandType.Memory, argument),
            "history" => new ParsedCommand(CommandType.History, argument),
            "tools" => new ParsedCommand(CommandType.Tools, argument),
            "request" => new ParsedCommand(CommandType.Request),
            "sync" => new ParsedCommand(CommandType.Sync),
            "transcribe" => new ParsedCommand(CommandType.Transcript, argument),
            "jaeger" => new ParsedCommand(CommandType.Jaeger, argument),
            _ => new ParsedCommand(CommandType.Unknown, command)
        };
    }
}
