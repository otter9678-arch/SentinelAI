using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SentinelAI.Core
{
    /// <summary>
    /// Central configuration: resolves paths from a config file, environment,
    /// or sensible defaults. Replaces every hardcoded path in the codebase.
    /// </summary>
    public static class AppConfig
    {
        private static Dictionary<string, string> _values;
        private static string _configPath;
        private static readonly object _lock = new object();

        public static string ConfigDirectory
        {
            get
            {
                // %LOCALAPPDATA%\SentinelAI — the standard Windows pattern for user-editable config
                string base_ = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SentinelAI");
                Directory.CreateDirectory(base_);
                return base_;
            }
        }

        public static string LogDirectory
        {
            get
            {
                string p = Path.Combine(ConfigDirectory, "Logs");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static string QuarantineDirectory
        {
            get
            {
                string p = Path.Combine(ConfigDirectory, "Quarantine");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static string KnowledgeFilePath
        {
            get
            {
                string p = Path.Combine(ConfigDirectory, "knowledge.json");
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                return p;
            }
        }

        public static string GameAiModelPath
        {
            get
            {
                string p = Path.Combine(ConfigDirectory, "gameai_model.pt");
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                return p;
            }
        }

        public static string ConfigFilePath
        {
            get
            {
                if (_configPath == null)
                    _configPath = Path.Combine(ConfigDirectory, "config.json");
                return _configPath;
            }
        }

        /// <summary>Load config from disk (or create defaults).</summary>
        public static void Load()
        {
            lock (_lock)
            {
                if (_values != null) return;
                _values = new Dictionary<string, string>();
                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        string json = File.ReadAllText(ConfigFilePath);
                        _values = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    }
                }
                catch { /* corrupt config — start with defaults */ }

                // Seed defaults if missing
                SetDefault("OllamaUrl", "http://localhost:11434");
                SetDefault("OllamaChatModel", "gemma4:26b");
                SetDefault("OllamaVisionModel", "gemma4:26b");
                SetDefault("GameNetDiagTarget", "1.1.1.1");
                SetDefault("MatterHubUrl", "http://homeassistant.local:8123");
                SetDefault("MatterToken", "");
                SetDefault("WeatherLatitude", "43.65");
                SetDefault("WeatherLongitude", "-79.38");
                SetDefault("SteamPath", FindSteamPath());
                SetDefault("GamingCpuCap", "30");
                Save();
            }
        }

        static void SetDefault(string key, string value)
        {
            if (!_values.ContainsKey(key)) _values[key] = value;
        }

        static string FindSteamPath()
        {
            // Try common Steam locations
            string[] candidates =
            {
                "C:\\Program Files (x86)\\Steam",
                "D:\\SteamLibrary",
                "E:\\SteamLibrary",
                "F:\\SteamLibrary"
            };
            foreach (var c in candidates)
                if (Directory.Exists(c)) return c;
            return "C:\\Program Files (x86)\\Steam";
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(ConfigFilePath, json);
                }
                catch { }
            }
        }

        public static string Get(string key, string fallback = "")
        {
            Load();
            return _values.TryGetValue(key, out var v) ? v : fallback;
        }

        public static void Set(string key, string value)
        {
            Load();
            _values[key] = value;
            Save();
        }

        public static int GetInt(string key, int fallback = 0)
        {
            return int.TryParse(Get(key), out var v) ? v : fallback;
        }

        public static double GetDouble(string key, double fallback = 0)
        {
            return double.TryParse(Get(key), out var v) ? v : fallback;
        }
    }
}