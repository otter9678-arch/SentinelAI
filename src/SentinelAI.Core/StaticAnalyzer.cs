using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SentinelAI.Core
{
    /// <summary>
    /// Static-analysis engine: hashes, PE header inspection, embedded-string
    /// heuristics, packer detection. Fast pass that filters which files go to
    /// the AI model for deep classification.
    /// </summary>
    public static class StaticAnalyzer
    {
        public const int MalwareScore = 60;

        /// <summary>Known-good whitelisted hashes (sha256 of common Windows files).</summary>
        public static HashSet<string> TrustedHashes { get; } = new HashSet<string>();

        /// <summary>
        /// Analyzes a file and produces a 0-100 threat score.
        /// 0-29 clean, 30-59 suspicious, 60+ likely malware.
        /// </summary>
        public static ScanResult Analyze(string path)
        {
            var result = new ScanResult { FilePath = path };
            try
            {
                var fi = new FileInfo(path);
                result.SizeBytes = fi.Length;
                if (fi.Length == 0 || !fi.Exists)
                {
                    result.Score = 0;
                    result.Verdict = "empty-or-missing";
                    return result;
                }

                result.Sha256 = ComputeSha256(path);
                if (TrustedHashes.Contains(result.Sha256))
                {
                    result.Score = 0;
                    result.Verdict = "trusted";
                    return result;
                }

                // Hidden executables in temp dirs are a classic malware pattern.
                if (path.Contains("\\AppData\\Local\\Temp\\") && IsExecutable(path)) result.Score += 25;
                if (path.Contains("\\AppData\\Roaming\\") && IsExecutable(path) && fi.Attributes.HasFlag(FileAttributes.Hidden))
                    result.Score += 30;

                // Double extensions: "photo.jpg.exe"
                var name = fi.Name.ToLowerInvariant();
                if (name.Count(c => c == '.') >= 2 && IsExecutable(path)) result.Score += 20;

                // No digital signature + executable in user-writable dir
                if (IsExecutable(path) && !Authenticode.IsSigned(path)) result.Score += 15;

                // PE structure heuristics
                byte[] header = ReadHeader(path);
                if (header != null && header.Length > 0x40)
                {
                    // MZ magic
                    if (header[0] == 0x4D && header[1] == 0x5A)
                    {
                        result.IsExecutable = true;
                        // Packer/entropy detection: high entropy in first 2KB
                        // suggests UPX/custom packing.
                        double entropy = ShannonEntropy(header, Math.Min(header.Length, 2048));
                        result.Entropy = entropy;
                        if (entropy > 7.2) result.Score += 20;
                    }
                }

                result.Score = Math.Min(100, result.Score);
                result.Verdict =
                    result.Score >= 60 ? "malware-likely" :
                    result.Score >= 30 ? "suspicious" : "clean";
            }
            catch (Exception ex)
            {
                result.Verdict = "error: " + ex.Message;
            }
            return result;
        }

        public static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var s = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }

        public static bool IsExecutable(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".exe" || ext == ".dll" || ext == ".sys" || ext == ".scr"
                || ext == ".com" || ext == ".pif" || ext == ".bat" || ext == ".cmd" || ext == ".ps1";
        }

        public static byte[] ReadHeader(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    byte[] buf = new byte[4096];
                    int read = fs.Read(buf, 0, buf.Length);
                    Array.Resize(ref buf, read);
                    return buf;
                }
            }
            catch { return null; }
        }

        /// <summary>Shannon entropy of a byte array (0 = uniform, 8 = random).</summary>
        public static double ShannonEntropy(byte[] data, int length)
        {
            if (length <= 0) return 0;
            int[] freq = new int[256];
            foreach (byte b in data.Take(length)) freq[b]++;
            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] == 0) continue;
                double p = (double)freq[i] / length;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy;
        }
    }

    public class ScanResult
    {
        public string FilePath { get; set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public int Score { get; set; }
        public string Verdict { get; set; }
        public double Entropy { get; set; }
        public bool IsExecutable { get; set; }
        public string AiVerdict { get; set; }
        public float AiConfidence { get; set; }
    }
}