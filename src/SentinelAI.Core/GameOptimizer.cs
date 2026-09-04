using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SentinelAI.Core
{
    /// <summary>
    /// Per-game optimization profiles: known game tweaks (process priority,
    /// HPET, GameDVR off, network tuning). Safe, reversible, user-approved.
    /// </summary>
    public static class GameOptimizer
    {
        public class Profile
        {
            public string GameName;
            public string ExeName;          // process name to boost
            public bool DisableGameDvr;     // Windows GameDVR overhead
            public bool HighPriority;        // above-normal priority
            public string[] Notes;
        }

        public static readonly Dictionary<string, Profile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["destiny2"] = new Profile
            {
                GameName = "Destiny 2", ExeName = "destiny2", HighPriority = true, DisableGameDvr = true,
                Notes = new[] { "Bungie recommends full-screen exclusive; GameDVR off reduces stutter." }
            },
            ["haloinfinite"] = new Profile { GameName = "Halo Infinite", ExeName = "HaloInfinite", HighPriority = true, DisableGameDvr = true, Notes = new[] { "GameDVR off recommended." } },
            ["eldenring"] = new Profile { GameName = "Elden Ring", ExeName = "eldenring", HighPriority = false, DisableGameDvr = false, Notes = new[] { "Single-player — no aggressive tweaks needed." } },
            ["fortnite"] = new Profile { GameName = "Fortnite", ExeName = "FortniteClient-Win64-Shipping", HighPriority = true, DisableGameDvr = true, Notes = new[] { "GameDVR off; DirectX 11 mode if DX12 stutters." } },
            ["cyberpunk"] = new Profile { GameName = "Cyberpunk 2077", ExeName = "Cyberpunk2077", HighPriority = true, DisableGameDvr = true, Notes = new[] { "RTX 4090 runs it maxed; GameDVR off anyway." } },
        };

        public static Profile Lookup(string processName)
        {
            return Profiles.TryGetValue(processName, out var p) ? p : null;
        }

        /// <summary>Applies safe, reversible tweaks when a known game launches.</summary>
        public static void Apply(Profile p)
        {
            try
            {
                if (p.DisableGameDvr)
                {
                    // HKCU — user scope, reversible, no admin needed
                    Microsoft.Win32.Registry.SetValue(
                        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
                        "AppCaptureEnabled", 0);
                }
                if (p.HighPriority)
                {
                    foreach (var proc in Process.GetProcessesByName(p.ExeName))
                    {
                        try { proc.PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }
                    }
                }
                foreach (var n in p.Notes) System.Diagnostics.Trace.WriteLine($"[GameOpt] {p.GameName}: {n}");
            }
            catch { }
        }

        public static void Revert(Profile p)
        {
            try
            {
                if (p.DisableGameDvr)
                {
                    Microsoft.Win32.Registry.SetValue(
                        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
                        "AppCaptureEnabled", 1);
                }
            }
            catch { }
        }
    }
}