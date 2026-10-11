using System.IO.Abstractions;

namespace WindowsSearch.Common.Logging;

public static class AppLogger
{
    public static IFileSystem FileSystem { get; set; } = new FileSystem();
    private static string _logPrefix = "app";
    private static string _logDirectory = "";
    public static LogLevel LogLevel { get; private set; } = LogLevel.Info;
    private static readonly object _lock = new();

    public static void Initialize(string appName, LogLevel initialLevel = LogLevel.Info, string? customDirectory = null)
    {
        _logPrefix = appName;
        LogLevel = initialLevel;

        if (!string.IsNullOrWhiteSpace(customDirectory))
        {
            _logDirectory = FileSystem.Path.GetFullPath(customDirectory);
        }
        else
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (baseDir.Contains($"{FileSystem.Path.DirectorySeparatorChar}Providers{FileSystem.Path.DirectorySeparatorChar}"))
            {
                _logDirectory = FileSystem.Path.GetFullPath(FileSystem.Path.Combine(baseDir, "..", "..", "Logs"));
            }
            else
            {
                _logDirectory = FileSystem.Path.Combine(baseDir, "Logs");
            }
        }

        if (!FileSystem.Directory.Exists(_logDirectory))
        {
            FileSystem.Directory.CreateDirectory(_logDirectory);
        }
    }

    public static void SetLogLevel(LogLevel level)
    {
        LogLevel = level;
    }

    public static void Debug(string message) => Log(LogLevel.Debug, "DEBUG", message);
    public static void Info(string message) => Log(LogLevel.Info, "INFO", message);
    public static void Warn(string message) => Log(LogLevel.Warn, "WARN", message);
    public static void Error(string message, Exception? ex = null) => Log(LogLevel.Error, "ERROR", $"{message} {ex}");

    private static void Log(LogLevel level, string levelStr, string message)
    {
        if (level < LogLevel)
            return;

        if (string.IsNullOrEmpty(_logDirectory))
        {
            Initialize("app"); // Fallback if not initialized
        }

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var fileName = $"{_logPrefix}Logs_{date}.log";
        var logPath = FileSystem.Path.Combine(_logDirectory, fileName);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var logLine = $"[{timestamp}] [{levelStr}] {message}{Environment.NewLine}";

        lock (_lock)
        {
            try
            {
                FileSystem.File.AppendAllText(logPath, logLine);
            }
            catch
            {
                // Ignore logging failures to prevent crashing the app
            }
        }
    }
}
