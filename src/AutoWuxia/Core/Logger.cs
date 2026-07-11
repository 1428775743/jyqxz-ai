namespace AutoWuxia.Core;

public static class Logger
{
    private static readonly string LogDir = AppPaths.LogsDir;
    private static readonly string LogFile = Path.Combine(LogDir, $"game_{DateTime.Now:yyyyMMdd}.log");

    static Logger()
    {
        try { Directory.CreateDirectory(LogDir); }
        catch { }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        try
        {
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch { }

        if (level == "ERROR")
            System.Diagnostics.Debug.WriteLine(line);
    }

    public static string GetLogFilePath() => LogFile;
}
