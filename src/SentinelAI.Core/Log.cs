using System;
using System.IO;
using System.Linq;

namespace SentinelAI.Core
{
    /// <summary>
    /// Global logger: writes to %LOCALAPPDATA%\SentinelAI\Logs with daily rotation.
    /// Also forwards to console for interactive use.
    /// </summary>
    public static class Log
    {
        private static readonly object _lock = new object();
        private static string _currentFile;

        public static string CurrentLogFile
        {
            get
            {
                if (_currentFile == null)
                {
                    string dir = AppConfig.LogDirectory;
                    _currentFile = Path.Combine(dir, $"sentinel-{DateTime.Now:yyyyMMdd}.log");
                }
                return _currentFile;
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", $"{message}\n{ex}");

        private static void Write(string level, string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                lock (_lock)
                {
                    // Daily rotation: create new file when date changes
                    string expected = Path.Combine(AppConfig.LogDirectory, $"sentinel-{DateTime.Now:yyyyMMdd}.log");
                    if (_currentFile != expected) _currentFile = expected;

                    File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
                }
                // Also mirror to console
                Console.WriteLine(line);
            }
            catch { /* logging must never throw */ }
        }

        /// <summary>Deletes log files older than 14 days.</summary>
        public static void CleanOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-14);
                foreach (var f in Directory.GetFiles(AppConfig.LogDirectory, "sentinel-*.log"))
                {
                    if (File.GetLastWriteTime(f) < cutoff) File.Delete(f);
                }
            }
            catch { }
        }
    }
}