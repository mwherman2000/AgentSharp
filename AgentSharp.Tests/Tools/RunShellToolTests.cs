using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class RunShellToolTests
{
    [Theory]
    [InlineData("dotnet build > nul 2>&1", "dotnet build > /dev/null 2>&1")]
    [InlineData("dotnet build >nul", "dotnet build >/dev/null")]
    [InlineData("dotnet build 2>nul", "dotnet build 2>/dev/null")]
    [InlineData("rm nul", "rm nul")]
    public void NormalizeNullDevice_RewritesNulToDevNull_ForPosixShells(string command, string expected)
    {
        var result = RunShellTool.NormalizeNullDevice(command, usesPosixShell: true);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("dotnet build > /dev/null 2>&1", "dotnet build > nul 2>&1")]
    [InlineData("dotnet build >/dev/null", "dotnet build >nul")]
    [InlineData("rm /dev/null", "rm /dev/null")]
    public void NormalizeNullDevice_RewritesDevNullToNul_ForCmd(string command, string expected)
    {
        var result = RunShellTool.NormalizeNullDevice(command, usesPosixShell: false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetShellCommand_AppliesNormalization_RegardlessOfWhichShellIsSelected()
    {
        var (shell, args) = RunShellTool.GetShellCommand("dotnet build > nul 2>&1");

        var expectedFragment = shell.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? "> nul 2>&1"
            : "> /dev/null 2>&1";

        Assert.Contains(expectedFragment, args);
    }
}
