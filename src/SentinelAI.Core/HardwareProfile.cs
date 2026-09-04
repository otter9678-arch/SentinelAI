using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SentinelAI.Core
{
    /// <summary>
    /// Detects hardware capabilities so SentinelAI adapts: CPU cores â†’ parallel
    /// scan workers, RAM â†’ memory budget, GPU â†’ AI inference, disk type â†’ scan
    /// throttling. Works across Intel, AMD, NVIDIA, ARM, Apple Silicon.
    /// Uses Process/fallbacks instead of WMI so it works on net472 without
    /// extra packages (System.Management is not always available).
    /// </summary>
    public static class HardwareProfile
    {
        public class Info
        {
            public int CpuCores;
            public long TotalRamBytes;
            public long AvailableRamBytes;
            public bool HasNvidiaGpu;
            public bool HasAmdGpu;
            public bool HasIntelGpu;
            public string PrimaryGpuName = "";
            public long TotalDiskBytes;
            public long FreeDiskBytes;
            public bool IsWindows;
            public bool IsMac;
            public bool IsLinux;
            public bool IsArm64;
            public string OsDescription = "";
            public int RecommendedScanWorkers;
            public int RecommendedAiConcurrency;
        }

        private static Info _cached;
        private static readonly object _lock = new object();
        private static DateTime _cacheTime;

        public static Info Detect(bool forceRefresh = false)
        {
            if (_cached != null && !forceRefresh && (DateTime.Now - _cacheTime).TotalMinutes < 5)
                return _cached;

            var info = new Info();
            try
            {
                // CPU / architecture
                info.CpuCores = Environment.ProcessorCount;
                string procArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
                info.IsArm64 = procArch.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0;

                // OS
                var platform = Environment.OSVersion.Platform;
                info.IsWindows = platform == PlatformID.Win32NT || platform == PlatformID.Win32Windows;
                info.IsLinux = platform == PlatformID.Unix && File.Exists("/proc/meminfo");
                info.IsMac = platform == PlatformID.Unix && !info.IsLinux;
                info.OsDescription = Environment.OSVersion.ToString();

                // RAM (try PerformanceCounter first, fallback to /proc/meminfo)
                try
                {
                    var pc = new PerformanceCounter("Memory", "Available MBytes");
                    info.AvailableRamBytes = (long)pc.NextValue() * 1024L * 1024L;
                    // Total: use GC + system assumption (better than nothing)
                    info.TotalRamBytes = info.AvailableRamBytes + GC.GetTotalMemory(false) * 8;
                }
                catch { }

                if (File.Exists("/proc/meminfo"))
                {
                    foreach (var line in File.ReadAllLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out var kb))
                                info.TotalRamBytes = kb * 1024L;
                        }
                        if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out var kb))
                                info.AvailableRamBytes = kb * 1024L;
                        }
                    }
                }

                // GPU: nvidia-smi is cross-platform and tells us everything
                try
                {
                    var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=name --format=csv,noheader")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using var p = Process.Start(psi);
                    string name = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    if (!string.IsNullOrEmpty(name) && name.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        info.HasNvidiaGpu = true;
                        info.PrimaryGpuName = name;
                    }
                }
                catch { }

                // Fallback: detect AMD/Intel GPU from the system drive / registry-free heuristic
                if (!info.HasNvidiaGpu)
                {
                    try
                    {
                        // Use WMIC (built into Windows) as a second pass
                        var psi2 = new ProcessStartInfo("wmic", "path Win32_VideoController get Name /value")
                        { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                        using var p2 = Process.Start(psi2);
                        string outp = p2.StandardOutput.ReadToEnd();
                        p2.WaitForExit(3000);
                        foreach (var line in outp.Split('\n'))
                        {
                            if (!line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)) continue;
                            string val = line.Substring(5).Trim();
                            if (val.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) info.HasNvidiaGpu = true;
                            if (val.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0) info.HasAmdGpu = true;
                            if (val.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) info.HasIntelGpu = true;
                            if (string.IsNullOrEmpty(info.PrimaryGpuName) && !string.IsNullOrEmpty(val) && val.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0)
                                info.PrimaryGpuName = val;
                        }
                    }
                    catch { }
                }

                // Disk free space on the drive SentinelAI is installed on
                try
                {
                    var drive = new DriveInfo(AppContext.BaseDirectory);
                    info.TotalDiskBytes = drive.TotalSize;
                    info.FreeDiskBytes = drive.AvailableFreeSpace;
                }
                catch { }

                // Recommended tuning
                info.RecommendedScanWorkers = Math.Max(2, Math.Min(info.CpuCores, 16));
                info.RecommendedAiConcurrency = info.HasNvidiaGpu ? 4 : info.HasAmdGpu ? 2 : 1;

                _cached = info;
                _cacheTime = DateTime.Now;
            }
            catch { }

            return _cached ?? new Info();
        }

        static long ParseMeminfoKb(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 && long.TryParse(parts[1], out var v) ? v : 0;
        }

        /// <summary>Human-readable summary. JARVIS can speak this.</summary>
        public static string Summary()
        {
            var i = Detect();
            string gpu = i.HasNvidiaGpu ? "NVIDIA" : i.HasAmdGpu ? "AMD" : i.HasIntelGpu ? "Intel" : "None";
            string os = i.IsWindows ? "Windows" : i.IsMac ? "macOS" : "Linux";
            string arch = i.IsArm64 ? "ARM64" : "x64";
            double ramGb = i.TotalRamBytes / (1024.0 * 1024 * 1024);
            double diskFreeGb = i.FreeDiskBytes / (1024.0 * 1024 * 1024);
            return $"CPU: {i.CpuCores} cores | RAM: {ramGb:F0} GB | GPU: {gpu} ({i.PrimaryGpuName}) | OS: {os} {arch} | Disk free: {diskFreeGb:F0} GB | Scan workers: {i.RecommendedScanWorkers}";
        }

        /// <summary>Adapts scan concurrency based on detected hardware.</summary>
        public static int GetOptimalScanWorkers()
        {
            var i = Detect();
            return i.RecommendedScanWorkers;
        }
    }
}
