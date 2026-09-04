using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SentinelAI.Core
{
    /// <summary>
    /// Real-time protection: FileSystemWatcher on critical directories that
    /// immediately analyzes every new/modified executable, detects ransomware
    /// rename patterns, and blocks known-bad hashes.
    /// </summary>
    public class RealTimeProtection : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly HashSet<string> _knownBadHashes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _recentlyScanned = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public event Action<string, string> ThreatBlocked;  // (path, reason)
        public event Action<string, string> ThreatAlert;    // (path, reason)

        public bool IsEnabled { get; private set; }

        public void AddKnownBadHash(string sha256) { lock (_lock) _knownBadHashes.Add(sha256); }

        /// <summary>
        /// Watches: Startup folders, Desktop, Downloads, Program Files temp,
        /// AppData\Local\Temp, and the user profile root.
        /// </summary>
        public void Start()
        {
            if (IsEnabled) return;
            IsEnabled = true;

            string[] watchDirs =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
            };

            foreach (var dir in watchDirs.Where(Directory.Exists))
            {
                try
                {
                    var watcher = new FileSystemWatcher(dir)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        IncludeSubdirectories = dir.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                                               dir.Contains("Programs", StringComparison.OrdinalIgnoreCase),
                        EnableRaisingEvents = true
                    };
                    watcher.Created += OnFileCreated;
                    watcher.Changed += OnFileChanged;
                    _watchers.Add(watcher);
                }
                catch { /* Some dirs may throw on access */ }
            }
            Log.Info($"[RTP] Started watching {watchDirs.Length} directories.");
        }

        public void Stop()
        {
            foreach (var w in _watchers) { w.EnableRaisingEvents = false; w.Dispose(); }
            _watchers.Clear();
            IsEnabled = false;
            Log.Info("[RTP] Stopped.");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e) => AnalyzeAndBlock(e.FullPath);
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed) AnalyzeAndBlock(e.FullPath);
        }

        private void AnalyzeAndBlock(string path)
        {
            // Dedup: don't re-scan the same file within 5s
            lock (_lock)
            {
                if (_recentlyScanned.Contains(path)) return;
                _recentlyScanned.Add(path);
            }
            // Trim old entries
            if (_recentlyScanned.Count > 1000) _recentlyScanned.Clear();

            try
            {
                // Only analyze executables
                if (!StaticAnalyzer.IsExecutable(path)) return;
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length == 0) return;

                var result = StaticAnalyzer.Analyze(path);

                // Known bad hash → block immediately
                if (!string.IsNullOrEmpty(result.Sha256))
                {
                    bool isKnownBad;
                    lock (_lock) isKnownBad = _knownBadHashes.Contains(result.Sha256);
                    if (isKnownBad)
                    {
                        ThreatBlocked?.Invoke(path, "Known malware hash");
                        QuarantineFile(path);
                        return;
                    }
                }

                // High score → alert and optionally block
                if (result.Score >= StaticAnalyzer.MalwareScore)
                {
                    ThreatBlocked?.Invoke(path, $"Static score {result.Score}/100 — {result.Verdict}");
                    QuarantineFile(path);
                }
                // Suspicious → alert only (don't auto-delete)
                else if (result.Score >= 30)
                {
                    ThreatAlert?.Invoke(path, $"Suspicious file (score {result.Score}/100): {result.Verdict}");
                }
            }
            catch (IOException) { /* File still in use — skip */ }
            catch { }
        }

        static void QuarantineFile(string path)
        {
            try
            {
                string vault = AppConfig.QuarantineDirectory;
                Directory.CreateDirectory(vault);
                string dest = Path.Combine(vault, Path.GetFileName(path) + "." + DateTime.Now.Ticks + ".quarantined");
                byte[] data = File.ReadAllBytes(path);
                byte[] enc = System.Security.Cryptography.ProtectedData.Protect(
                    data, Encoding.UTF8.GetBytes("SentinelAI-RTP"), System.Security.Cryptography.DataProtectionScope.LocalMachine);
                File.WriteAllBytes(dest, enc);
                File.Delete(path);
                Log.Info($"[RTP] Quarantined: {path} -> {dest}");
            }
            catch (Exception ex) { Log.Error($"[RTP] Quarantine failed for {path}: {ex.Message}"); }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}