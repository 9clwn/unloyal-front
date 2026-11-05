namespace UnloyalFront.Utility;

public static class Log
{
    static readonly object Lock = new();

    public static string FilePath => Path.Combine(Paths.Logs, "unloyalfont.log");

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Paths.Logs);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { /* logging must never crash the app */ }
    }

    public static void Write(string context, Exception ex) => Write($"{context}: {ex}");
}
