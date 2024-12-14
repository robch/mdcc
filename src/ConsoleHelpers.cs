using System;
using System.Text;
using System.Threading;

class ConsoleHelpers
{
    public static void Configure(bool debug, bool verbose)
    {
        Console.OutputEncoding = Encoding.UTF8;

        _debug = debug;
        _verbose = verbose;
    }

    public static void PrintStatus(string status)
    {
        if (!_debug && !_verbose) return;
        if (Console.IsOutputRedirected) return;

        lock (_printLock)
        {
            PrintStatusErase();
            Console.Write("\r" + status);
            _cchLastStatus = status.Length;
            if (_debug) Thread.Sleep(1);
        }
    }

    public static void PrintStatusErase()
    {
        if (!_debug && !_verbose) return;
        if (_cchLastStatus <= 0) return;

        lock (_printLock)
        {
            var eraseLastStatus = "\r" + new string(' ', _cchLastStatus) + "\r";
            Console.Write(eraseLastStatus);
            _cchLastStatus = 0;
        }
    }

    public static void PrintLine(string message)
    {
        lock (_printLock)
        {
            PrintStatusErase();
            Console.WriteLine(message);
        }
    }

    public static void PrintDebugLine(string message)
    {
        if (!_debug) return;
        PrintLine(message);
    }

    private static bool _debug = false;
    private static bool _verbose = false;
    private static object _printLock = new();
    private static int _cchLastStatus = 0;
}