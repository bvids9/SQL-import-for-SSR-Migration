using System;

public interface ILogger
{
    void Log(string message);
    void LogError(string message, Exception ex = null);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] INFO {message}");

    public void LogError(string message, Exception ex = null)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR {message}");
        if (ex != null) Console.WriteLine(ex);
    }
}