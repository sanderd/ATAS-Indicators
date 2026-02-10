namespace Utils.Common.Logging;

/// <summary>
/// Mock logging extensions matching ATAS Utils.Common.Logging API
/// </summary>
public static class LoggingExtensions
{
    public static void LogDebug(this object source, string message)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[DEBUG] {source.GetType().Name}: {message}");
#endif
    }

    public static void LogInfo(this object source, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[INFO] {source.GetType().Name}: {message}");
    }

    public static void LogWarn(this object source, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[WARN] {source.GetType().Name}: {message}");
    }

    public static void LogError(this object source, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[ERROR] {source.GetType().Name}: {message}");
    }
}
