using System;

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
}
