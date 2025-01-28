using System;
using System.Threading.Tasks;
using Xunit;

public class RunCommandTimeoutTests
{
    [Fact]
    public async Task RunCommand_WithTimeout_ShouldTerminateAfterTimeout()
    {
        // Arrange
        var command = new RunCommand
        {
            ScriptToRun = "sleep 10", // Sleep for 10 seconds
            Type = RunCommand.ScriptType.Bash,
            TimeoutMilliseconds = 500 // Timeout after 500ms
        };

        // Act
        var startTime = DateTime.Now;
        var (output, exitCode) = await ProcessHelpers.RunShellCommandAsync(command.ScriptToRun, "bash", command.TimeoutMilliseconds);
        var duration = DateTime.Now - startTime;

        // Assert
        Assert.True(duration.TotalMilliseconds < 2000); // Should terminate well before 2 seconds
        Assert.Contains("Timedout!", output); // Should include timeout message
        Assert.NotEqual(0, exitCode); // Should have non-zero exit code due to timeout
    }

    [Fact]
    public async Task RunCommand_WithinTimeout_ShouldCompleteSuccessfully()
    {
        // Arrange
        var command = new RunCommand
        {
            ScriptToRun = "sleep 0.1", // Sleep for 0.1 seconds
            Type = RunCommand.ScriptType.Bash,
            TimeoutMilliseconds = 2000 // Timeout after 2 seconds
        };

        // Act
        var (output, exitCode) = await ProcessHelpers.RunShellCommandAsync(command.ScriptToRun, "bash", command.TimeoutMilliseconds);

        // Assert
        Assert.Equal(0, exitCode); // Should complete successfully
        Assert.DoesNotContain("Timedout!", output); // Should not include timeout message
    }

    [Fact]
    public void RunCommand_InvalidTimeout_ShouldThrowException()
    {
        // Arrange
        var args = new[] { "run", "--timeout", "-500", "echo test" };

        // Act & Assert
        var ex = Assert.Throws<CommandLineException>(() =>
        {
            CommandLineOptions.Parse(args, out var options, out var parseEx);
            if (parseEx != null) throw parseEx;
        });

        Assert.Contains("--timeout requires a positive integer value", ex.Message);
    }
}