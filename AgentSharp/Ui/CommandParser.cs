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
            "sync" => new ParsedCommand(CommandType.Sync),
            _ => new ParsedCommand(CommandType.Unknown, command)
        };
    }
}
