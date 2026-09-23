using System;
using System.IO;

namespace CommonLogging;

public static class AppLogger
{
    private static string _logPrefix = "app";
    private static string _logDirectory = "";
    private static readonly object _lock = new();

    public static void Initialize(string appName)
    {
        _logPrefix = appName;
        
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // If we are running from a provider directory (e.g., ...\Providers\JetBrainsProvider\)
        // we want to put logs in the Hub's Logs folder (...\[Hub Root]\Logs)
        if (baseDir.Contains($"{Path.DirectorySeparatorChar}Providers{Path.DirectorySeparatorChar}"))
        {
            // Traverse up out of Providers\{ProviderName}\ to the Hub root
            _logDirectory = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Logs"));
        }
        else
        {
            // We are the Hub, so Logs is just here
            _logDirectory = Path.Combine(baseDir, "Logs");
        }

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null) => Log("ERROR", $"{message} {ex}");

    private static void Log(string level, string message)
    {
        if (string.IsNullOrEmpty(_logDirectory))
        {
            Initialize("app"); // Fallback if not initialized
        }

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var fileName = $"{_logPrefix}Logs_{date}.log";
        var logPath = Path.Combine(_logDirectory, fileName);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var logLine = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(logPath, logLine);
            }
            catch
            {
                // Ignore logging failures to prevent crashing the app
            }
        }
    }
}
