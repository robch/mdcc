using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class RunCommand : Command
{
    public enum ScriptType
    {
        Default, // Uses cmd on Windows, bash on Linux/Mac
        Cmd,
        Bash,
        PowerShell
    }

    public RunCommand() : base()
    {
        ScriptToRun = string.Empty;
        Type = ScriptType.Default;
        TimeoutMilliseconds = -1; // No timeout by default
    }

    public override bool IsEmpty()
    {
        return string.IsNullOrWhiteSpace(ScriptToRun);
    }

    public override string GetCommandName()
    {
        return "run";
    }

    public string ScriptToRun { get; set; }
    public ScriptType Type { get; set; }
    public int TimeoutMilliseconds { get; set; }

    public async Task<int> ExecuteWithTimeoutAsync(Process process)
    {
        if (TimeoutMilliseconds <= 0)
        {
            // No timeout specified, just wait for completion
            process.WaitForExit();
            return process.ExitCode;
        }

        try
        {
            // Wait for the process to exit with timeout
            var exited = await Task.Run(() => process.WaitForExit(TimeoutMilliseconds));
            
            if (!exited)
            {
                // Try graceful shutdown first
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1000)) // Give it 1 second to close gracefully
                    {
                        // Send Ctrl+C
                        if (!process.HasExited)
                        {
                            process.StandardInput.Close(); // This can trigger Ctrl+C behavior
                            if (!process.WaitForExit(1000)) // Another second to respond to Ctrl+C
                            {
                                // Force kill as last resort
                                if (!process.HasExited)
                                {
                                    process.Kill(true); // Kill process tree
                                }
                            }
                        }
                    }
                }
                return -1; // Indicate timeout
            }

            return process.ExitCode;
        }
        catch (Exception)
        {
            if (!process.HasExited)
            {
                try { process.Kill(true); } catch { } // Best effort kill
            }
            throw;
        }
    }
}
