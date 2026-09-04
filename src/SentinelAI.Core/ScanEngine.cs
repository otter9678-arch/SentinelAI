using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SentinelAI.Core
{
    /// <summary>
    /// Full system scanner: enumerates high-risk paths + running processes, runs
    /// static analysis in parallel (uses all 16 cores), escalates ambiguous
    /// findings to the AI classifier, quarantines confirmed malware.
    /// </summary>
    public class ScanEngine
    {
        public List<ScanResult> Results { get; } = new List<ScanResult>();
        public int FilesScanned { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime CompletedAt { get; private set; }
        public bool Running { get; private set; }

        public event Action<string> Progress;
        public event Action<ScanResult> ThreatFound;

        private readonly AiClassifier _ai;

        public ScanEngine(AiClassifier ai) { _ai = ai; }

        /// <summary>High-value scan targets: user profile, program files, temp, startup.</summary>
        public static string[] DefaultScanPaths { get; } = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            "C:\\Windows\\Temp"
        };

        public async Task ScanAsync(bool includeFullAi = true)
        {
            Running = true;
            StartedAt = DateTime.Now;
            Results.Clear();
            FilesScanned = 0;

            var targets = DefaultScanPaths.Where(Directory.Exists).ToList();
            Progress?.Invoke($"Scanning {targets.Count} root paths...");

            var allFiles = new List<string>();
            foreach (var root in targets)
            {
                try
                {
                    allFiles.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                        .Where(f => StaticAnalyzer.IsExecutable(f) || Path.GetExtension(f).ToLowerInvariant() == ".scr"));
                }
                catch { /* access denied on some dirs is expected */ }
            }
            Progress?.Invoke($"Found {allFiles.Count} executables to scan.");

            // Parallel static analysis: 16-core Ryzen means 16 concurrent workers.
            var results = new List<ScanResult>();
            var lockObj = new object();
            int scanned = 0;
            await Task.Run(() =>
            {
                Parallel.ForEach(allFiles, new ParallelOptions { MaxDegreeOfParallelism = 16 }, file =>
                {
                    var r = StaticAnalyzer.Analyze(file);
                    lock (lockObj) results.Add(r);
                    scanned = System.Threading.Interlocked.Increment(ref scanned);
                    if (scanned % 250 == 0)
                        Progress?.Invoke($"Scanned {scanned}/{allFiles.Count}...");
                });
            });
            FilesScanned = scanned;

            lock (lockObj) { foreach (var r in results) Results.Add(r); }

            // AI escalation on the suspicious band
            var ambiguous = results.Where(r => r.Score >= 30 && r.Score < 60).ToList();
            Progress?.Invoke($"AI classifying {ambiguous.Count} ambiguous files...");
            if (includeFullAi && await _ai.PingAsync())
            {
                foreach (var r in ambiguous)
                {
                    var (verdict, conf, reason) = await _ai.ClassifyAsync(r);
                    r.AiVerdict = verdict;
                    r.AiConfidence = conf;
                    if (verdict == "malware" && conf > 0.7f)
                    {
                        r.Score = Math.Max(r.Score, 85);
                        r.Verdict = "malware-ai";
                    }
                    else if (verdict == "clean" && conf > 0.8f)
                    {
                        r.Score = Math.Min(r.Score, 10);
                        r.Verdict = "clean-ai";
                    }
                }
            }

            foreach (var threat in results.Where(r => r.Score >= 60))
                ThreatFound?.Invoke(threat);

            CompletedAt = DateTime.Now;
            Running = false;
            Progress?.Invoke($"Scan complete: {FilesScanned} files in {(CompletedAt - StartedAt).TotalSeconds:F1}s. " +
                             $"Threats: {Results.Count(r => r.Score >= 60)}");
        }

        /// <summary>Quarantine: encrypt into vault, delete original.</summary>
        public static string Quarantine(ScanResult threat, string vaultDir = null)
        {
            vaultDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelAI", "Quarantine");
            Directory.CreateDirectory(vaultDir);
            string dest = Path.Combine(vaultDir, threat.Sha256 + ".quarantine");

            // Use DPAPI via ProtectedData — needs the System.Security.Cryptography.ProtectedData package
            byte[] data = File.ReadAllBytes(threat.FilePath);
            byte[] entropy = Encoding.UTF8.GetBytes("SentinelAI-Quarantine-v1");
            byte[] enc = ProtectedData.Protect(data, entropy, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(dest, enc);

            try { File.Delete(threat.FilePath); } catch { /* in-use; retry later */ }
            return dest;
        }
    }
}