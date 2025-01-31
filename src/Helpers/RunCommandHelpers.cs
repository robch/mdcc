using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

static class RunCommandHelpers
{
    public static List<Task<string>> HandleRunCommand(CommandLineOptions commandLineOptions, RunCommand command, SemaphoreSlim throttler, bool delayOutputToApplyInstructions)
    {
        var tasks = new List<Task<string>>();
        var shell = command.Type switch
        {
            RunCommand.ScriptType.Cmd => "cmd",
            RunCommand.ScriptType.Bash => "bash",
            RunCommand.ScriptType.PowerShell => "powershell",
            _ => string.Empty
        };

        var getCheckSaveTask = ProcessHelpers.RunShellCommandAsync(command.ScriptToRun, shell, command.EnvironmentVariables);
        var taskToAdd = delayOutputToApplyInstructions
            ? getCheckSaveTask.ContinueWith(t => t.Result.Item1)
            : getCheckSaveTask.ContinueWith(t =>
            {
                ConsoleHelpers.PrintLineIfNotEmpty(t.Result.Item1);
                return t.Result.Item1;
            });

        tasks.Add(taskToAdd);
        return tasks;
    }
}