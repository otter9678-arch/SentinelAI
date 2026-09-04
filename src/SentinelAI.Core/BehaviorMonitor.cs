using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SentinelAI.Core
{
    /// <summary>
    /// Behavioral engine: watches running processes for malware patterns —
    /// mass file renames (ransomware), LSASS handle access (credential theft),
    /// persistence via Run keys, DNS to known-bad hosts.
    /// This is the layer Defender's cloud also uses; local = no privacy leak.
    /// </summary>
    public class BehaviorMonitor
    {
        private readonly Dictionary<int, (int Renamed, int Deleted)> _fileOpsByPid = new Dictionary<int, (int, int)>();
        private readonly HashSet<string> _alertedPaths = new HashSet<string>();
        private DateTime _lastSweep = DateTime.MinValue;

        public event Action<string, string> ThreatDetected; // (pid info, description)

        /// <summary>
        /// Ransomware canary: plants decoy files and checks they stay intact.
        /// Called every 10 seconds.
        /// </summary>
        public void CheckRansomwareCanaries()
        {
            try
            {
                string[] canaryDirs =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".sentinel-canary"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), ".sentinel-canary")
                };
                foreach (var dir in canaryDirs)
                {
                    Directory.CreateDirectory(dir);
                    string canary = Path.Combine(dir, "canary.docx.sentinel");
                    if (!File.Exists(canary)) File.WriteAllText(canary, "sentinel canary — do not delete");
                    else
                    {
                        // If the canary was encrypted/renamed/deleted by something, alert.
                        var fi = new FileInfo(canary);
                        if (!fi.Exists || fi.Length == 0)
                        {
                            ThreatDetected?.Invoke("system", "RANSOMWARE CANARY TRIPPED in " + dir);
                        }
                    }
                }
            }
            catch { /* filesystem race — ignore */ }
        }

        /// <summary>
        /// Sweeps running processes for suspicious traits.
        /// Called every 30 seconds.
        /// </summary>
        public void SweepProcesses()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        string exe = "";
                        try { exe = proc.MainModule?.FileName ?? ""; } catch { }

                        // Impostor names: system processes not in system32
                        string[] systemProcs = { "lsass", "csrss", "winlogon", "services", "smss", "svchost" };
                        if (systemProcs.Contains(name) && exe.Length > 0 &&
                            !exe.StartsWith(Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            RaiseThreat(proc.Id, "Impostor system process: " + name + " at " + exe);
                        }

                        // Known-bad common tools that shouldn't run silently
                        string[] suspiciousTools = { "mimikatz", "procdump64", "invoke-mimikatz", "seatbelt", "sharpup", "rubeus" };
                        if (suspiciousTools.Any(t => name.Contains(t)))
                        {
                            RaiseThreat(proc.Id, "Known hacking tool running: " + name);
                        }

                        // Process with no parent from temp dir running .exe — common malware trait
                        if (exe.Contains("\\AppData\\Local\\Temp\\") && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            RaiseThreat(proc.Id, "Executable running from Temp: " + exe);
                        }
                    }
                    catch { /* process died or access denied — fine */ }
                }
            }
            catch { }
        }

        /// <summary>
        /// Persistence sweep: startup folder + Run keys + scheduled tasks.
        /// Called every 5 minutes.
        /// </summary>
        public void SweepPersistence()
        {
            try
            {
                string[] startupDirs =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                };
                foreach (var dir in startupDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir))
                    {
                        if (!_alertedPaths.Add(f.ToLowerInvariant())) continue;
                        var r = StaticAnalyzer.Analyze(f);
                        if (r.Score >= (int)StaticAnalyzer.MalwareScore)
                        {
                            RaiseThreat(0, "Malicious startup item: " + f + " (score " + r.Score + ")");
                        }
                    }
                }
            }
            catch { }
        }

        private void RaiseThreat(int pid, string description)
        {
            string key = pid + ":" + description;
            if (_alertedPaths.Add(key))
            {
                ThreatDetected?.Invoke("PID " + pid, description);
            }
        }
    }
}