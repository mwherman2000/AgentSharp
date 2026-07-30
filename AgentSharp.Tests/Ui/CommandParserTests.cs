using AgentSharp.Ui;

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
    [InlineData("/save", CommandType.Save)]
    [InlineData("/sessions", CommandType.Sessions)]
    [InlineData("/ls", CommandType.Sessions)]
    [InlineData("/status", CommandType.Status)]
    [InlineData("/model", CommandType.Model)]
    [InlineData("/memory", CommandType.Memory)]
    [InlineData("/mem", CommandType.Memory)]
    [InlineData("/sync", CommandType.Sync)]
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
}
