using AgentSharpApp.Ui;
using AgentSharpLib;

namespace AgentSharp.Tests.Ui;

public class CommandParserTests
{
    [Theory]
    [InlineData("/help", CommandType.Help)]
    [InlineData("/h", CommandType.Help)]
    [InlineData("/?", CommandType.Help)]
    [InlineData("/exit", CommandType.Exit)]
    [InlineData("/quit", CommandType.Exit)]
    [InlineData("/q", CommandType.Exit)]
    [InlineData("/clear", CommandType.Clear)]
    [InlineData("/cls", CommandType.Clear)]
    [InlineData("/return", CommandType.Return)]
    [InlineData("/handoff", CommandType.Handoff)]
    [InlineData("/personas", CommandType.Personas)]
    [InlineData("/compact", CommandType.Compact)]
    [InlineData("/save", CommandType.Save)]
    [InlineData("/sessions", CommandType.Sessions)]
    [InlineData("/ls", CommandType.Sessions)]
    [InlineData("/status", CommandType.Status)]
    [InlineData("/model", CommandType.Model)]
    [InlineData("/memory", CommandType.Memory)]
    [InlineData("/mem", CommandType.Memory)]
    [InlineData("/request", CommandType.Request)]
    [InlineData("/sync", CommandType.Sync)]
    [InlineData("/transcribe", CommandType.Transcript)]
    public void Parse_RecognizesCommands(string input, CommandType expected)
    {
        var result = CommandParser.Parse(input);
        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public void Parse_ExtractsArgument()
    {
        var result = CommandParser.Parse("/load abc123");
        Assert.Equal(CommandType.Load, result.Type);
        Assert.Equal("abc123", result.Argument);
    }

    [Fact]
    public void Parse_ReturnsNone_ForRegularText()
    {
        var result = CommandParser.Parse("hello world");
        Assert.Equal(CommandType.None, result.Type);
    }

    [Fact]
    public void Parse_ReturnsNone_ForEmpty()
    {
        var result = CommandParser.Parse("");
        Assert.Equal(CommandType.None, result.Type);
    }

    [Fact]
    public void Parse_ReturnsUnknown_ForBadCommand()
    {
        var result = CommandParser.Parse("/foobar");
        Assert.Equal(CommandType.Unknown, result.Type);
        Assert.Equal("foobar", result.Argument);
    }

    [Fact]
    public void Parse_MemoryClear_HasArgument()
    {
        var result = CommandParser.Parse("/memory clear");
        Assert.Equal(CommandType.Memory, result.Type);
        Assert.Equal("clear", result.Argument);
    }

    [Fact]
    public void Parse_Transcribe_ExtractsArgument()
    {
        var result = CommandParser.Parse("/transcribe session1");
        Assert.Equal(CommandType.Transcript, result.Type);
        Assert.Equal("session1", result.Argument);
    }

    // Both apps print their --help from CommandLineParser.ReplCommandHelp, so a command added to the
    // main app's parser without a line there would be missing from both help screens.
    [Fact]
    public void EveryCommand_IsListedInTheSharedHelp()
    {
        var listed = CommandLineParser.ReplCommandHelp.Select(c => c.Command.Split(' ')[0]).ToHashSet();

        foreach (var type in Enum.GetValues<CommandType>().Where(t => t is not (CommandType.None or CommandType.Unknown)))
        {
            var name = type == CommandType.Transcript ? "/transcribe" : "/" + type.ToString().ToLowerInvariant();
            Assert.Contains(name, listed);
        }
    }
}
