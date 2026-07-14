using System.Diagnostics;

namespace Qomicex.Core.AOT.Debugger;

public sealed class ColoredConsoleTraceListener : TraceListener
{
    private static readonly object _lock = new();

    public override void Write(string? message)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(message);
            Console.ResetColor();
        }
    }

    public override void WriteLine(string? message)
    {
        WriteFormatted("TRACE", ConsoleColor.DarkGray, message);
    }

    public override void Fail(string? message, string? detailMessage)
    {
        WriteFormatted("FAIL", ConsoleColor.Red, $"{message} | {detailMessage}");
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        var (color, label) = eventType switch
        {
            TraceEventType.Critical => (ConsoleColor.Magenta, "CRIT"),
            TraceEventType.Error => (ConsoleColor.Red, "ERROR"),
            TraceEventType.Warning => (ConsoleColor.Yellow, "WARN"),
            TraceEventType.Information => (ConsoleColor.Cyan, "INFO"),
            TraceEventType.Verbose => (ConsoleColor.DarkGray, "VERB"),
            _ => (ConsoleColor.Gray, "TRACE"),
        };
        WriteFormatted(label, color, $"[{source}] {message}");
    }

    private void WriteFormatted(string label, ConsoleColor color, string? message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");

            Console.ForegroundColor = color;
            Console.Write($"{label} ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);

            Console.ResetColor();
        }
    }
}
