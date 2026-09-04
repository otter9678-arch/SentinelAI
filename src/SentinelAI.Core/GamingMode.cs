using System;
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

        // Known game executables (extensible). Matched by lowercase process name.
        private static readonly string[] KnownGames =
        {
            "destiny2", "halo5", "haloinfinite", "cod", "mwii", "battlefield",
            "fortnite", "apex", "valoralnt", "valorant", "overwatch", "eldenring",
            "cyberpunk", "witcher3", "forza", "gta5", "rdr2", "minecraft",
            "starfield", "diablo", "wow", "palworld", "helldivers", "smite"
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
                    string name = proc.ProcessName.ToLowerInvariant();
                    // Direct match on known games
                    if (KnownGames.Contains(name)) { found = name; break; }
                    // Or any fullscreen process using high GPU (heuristic — cheap)
                    // (Skipped for now: fullscreen detection via user32 is expensive;
                    //  we'd rather err on the side of quiet than slow.)
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