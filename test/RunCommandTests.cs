using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class RunCommandTests
{
    [Fact]
    public async Task RunCommand_WithEnvironmentVariables_ShouldPassToProcess()
    {
        // Arrange
        var command = new RunCommand
        {
            ScriptToRun = "echo $TEST_VAR",
            Type = RunCommand.ScriptType.Bash,
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "TEST_VAR", "Hello World" }
            }
        };

        var throttler = new SemaphoreSlim(1);
        var options = new CommandLineOptions();

        // Act
        var tasks = RunCommandHelpers.HandleRunCommand(options, command, throttler, true);
        await Task.WhenAll(tasks);
        var result = tasks[0].Result;

        // Assert
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public async Task RunCommand_WithMultipleEnvironmentVariables_ShouldPassAllToProcess()
    {
        // Arrange
        var command = new RunCommand
        {
            ScriptToRun = "echo $VAR1 $VAR2",
            Type = RunCommand.ScriptType.Bash,
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "VAR1", "First" },
                { "VAR2", "Second" }
            }
        };

        var throttler = new SemaphoreSlim(1);
        var options = new CommandLineOptions();

        // Act
        var tasks = RunCommandHelpers.HandleRunCommand(options, command, throttler, true);
        await Task.WhenAll(tasks);
        var result = tasks[0].Result;

        // Assert
        Assert.Contains("First Second", result);
    }
}