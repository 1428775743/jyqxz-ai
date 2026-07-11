namespace AutoWuxia.Core;

public static class GameLogger
{
    private static readonly string LogDir;
    private static readonly string LogFile;
    private static readonly object Lock = new();

    static GameLogger()
    {
        LogDir = AppPaths.LogsDir;
        try { Directory.CreateDirectory(LogDir); } catch { }
        LogFile = Path.Combine(LogDir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message);
        if (ex != null)
            Write("ERROR", $"  Exception: {ex.Message}\n  Stack: {ex.StackTrace}");
    }

    public static void Action(string message) => Write("ACTION", message);
    public static void Combat(string message) => Write("COMBAT", message);
    public static void UI(string message) => Write("UI", message);
    public static void AI(string message) => Write("AI", message);
    public static void Dialogue(string message) => Write("DIALOG", message);
    public static void Economy(string message) => Write("ECON", message);

    private static void Write(string level, string message)
    {
        lock (Lock)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-6}] {message}";
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch { }
        }
    }

    public static string GetLogPath() => LogFile;
}
