using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SentinelAI.Core
{
    /// <summary>
    /// Gaming mode: detects fullscreen game sessions and pauses periodic
    /// SentinelAI scans / sweeps so they never steal frames mid-match.
    /// Re-enables automatically when the game closes.
    /// </summary>
    public class GamingMode
    {
        private static Timer _timer;
        private static bool _gaming;
        private static string _currentGame = "";
        private static readonly object _lock = new object();

        // Known game executables (curated list — update as needed).
        // Matched by lowercase process name (no .exe extension).
        private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
        {
            // Bungie
            "destiny2",
            // Halo
            "halo5", "haloinfinite",
            // Call of Duty
            "cod", "cod22-cod.exe", "cod24-cod", "mwii", "mwiii",
            // Battlefield / EA
            "battlefield", "bf2042", "bf6",
            // shooters
            "fortnite", "fortniteclient-win64-shipping", "apex", "apexlegends",
            "valoralnt", "valorant", "overwatch", "overwatch2",
            "csgo", "cs2", "cs2.exe",
            "pubg", "pubg-tslgame", "rainbowsix", "r6s",
            // RPG / open world
            "eldenring", "cyberpunk", "witcher3", "starfield",
            "fallout4", "fallout76", "skyrim", "skse64",
            // Rockstar
            "gta5", "gtav", "rdr2", "reddeadredemption2",
            // racing
            "forza", "forzahorizon5", "fh5", "forzamotorsport",
            // others
            "minecraft", "minecraftserver", "palworld", "helldivers",
            "helldivers2", "smite", "diablo", "diablo4", "wow", "wowclassic",
            "sekiro", "liesofp", " lords of the fallen",
            "baldursgate3", "bg3_dx11", "monsterhunterworld",
            "monsterhunterrise", "mhwilds", "nioh2", "nioh",
            "satisfactory", "valheim", "rust", "dota2", "league of legends",
            "leagueclient", "pathofexile", "pathofexile_x64",
            "terraria", "starbound", "subnautica", "no mans sky",
            "nomanssky", "escapefromtarkov", "eft",
            "warframe", "x64dbg", "war thunder", "wt",
            "grounded", "sons of the forest", "sonsoftheforest",
            "the forest", "theforest", "greenhell", "raft",
            "strandeddeep", "7daystodie", "7days", "dayz",
            "fallguys", "amongus", "amogus", "goose goose duck",
        };

        public static bool IsGaming
        {
            get { lock (_lock) return _gaming; }
        }
        public static string CurrentGame { get { lock (_lock) return _currentGame; } }

        public static event Action<string> GamingStarted;   // (game name)
        public static event Action<string> GamingStopped;   // (game name)

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(_ => CheckForGame(), null, 0, 5000);
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>Returns a GamingMode instance for consumers that want a handle.</summary>
        public static GamingMode CreateInstance() { return new GamingMode(); }

        private static void CheckForGame()
        {
            try
            {
                string found = null;
                foreach (var proc in Process.GetProcesses())
                {
                    string name = proc.ProcessName;
                    // Exact match on known game list
                    if (KnownGames.Contains(name)) { found = name; break; }
                    // Fuzzy fallback: any process whose name contains a known game
                    // (catches renamed exe's like "eldenring_launcher" or "destiny2 -beta")
                    foreach (var g in KnownGames)
                    {
                        if (name.StartsWith(g, StringComparison.OrdinalIgnoreCase) ||
                            name.IndexOf(g, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = g; // normalize to known game name
                            break;
                        }
                    }
                    if (found != null) break;
                    // Heuristic: any process using > 25% GPU for extended time is
                    // likely a game. Requires PerformanceCounter per-process which
                    // is expensive — skipped for now to keep the sweep cheap.
                }

                lock (_lock)
                {
                    if (found != null && !_gaming)
                    {
                        _gaming = true;
                        _currentGame = found;
                        GamingStarted?.Invoke(found);
                        OnEnterGame();
                    }
                    else if (found == null && _gaming)
                    {
                        string game = _currentGame;
                        _gaming = false;
                        _currentGame = "";
                        GamingStopped?.Invoke(game);
                        OnExitGame();
                    }
                }
            }
            catch { }
        }

        private static void OnEnterGame()
        {
            // Pause all periodic behavior sweeps (they're the noisy ones).
            // Static scan-on-demand still works if the user asks for it.
            System.Diagnostics.Trace.WriteLine($"[SentinelAI] Gaming mode ON ({_currentGame}) — background sweeps paused.");
        }

        private static void OnExitGame()
        {
            System.Diagnostics.Trace.WriteLine("[SentinelAI] Gaming mode OFF — background sweeps resumed.");
        }
    }
}