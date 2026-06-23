/// <summary>
/// Aras server-side logger implementation using Serilog via CCO.Logger.
/// Use this in production Aras Innovator R14+ methods.
/// </summary>
public class ArasLogger : ILogger
{
    public void Log(string message)
    {
        CCO.Logger.Log(LogLevel.Debug, $"[labs_sqlToReport] INFO {message}");
    }

    public void LogError(string message, Exception ex = null)
    {
        CCO.Logger.Log(LogLevel.Error, $"[labs_sqlToReport] ERROR {message}");
        if (ex != null)
            CCO.Logger.Log(LogLevel.Error, ex.ToString());
    }
}