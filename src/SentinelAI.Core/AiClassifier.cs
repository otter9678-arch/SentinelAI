using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SentinelAI.Core
{
    /// <summary>
    /// AI layer: uses the local Ollama instance (RTX 4090) to classify ambiguous
    /// files the static analyzer couldn't confidently score. Runs a small model
    /// with a security-tuned prompt against the file's static feature vector.
    /// </summary>
    public class AiClassifier
    {
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        public string Model { get; set; } = "qwen2.5:latest"; // matches user's local Ollama install

        public bool Available { get; private set; }

        public async Task<bool> PingAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/api/tags");
                Available = resp.IsSuccessStatusCode;
                return Available;
            }
            catch { Available = false; return false; }
        }

        /// <summary>
        /// Asks the local AI to classify a file given its static features.
        /// Returns (verdict, confidence 0-1, reasoning).
        /// </summary>
        public async Task<(string verdict, float confidence, string reasoning)> ClassifyAsync(ScanResult staticResult)
        {
            if (!Available) return ("ai-unavailable", 0f, "Ollama not running");

            try
            {
                string prompt =
                    "You are a malware analyst. Given these features of a file, output ONLY a JSON " +
                    "{\"verdict\":\"malware|suspicious|clean\",\"confidence\":0.0,\"reason\":\"...\"} — no other text.\n" +
                    $"File: {Path.GetFileName(staticResult.FilePath)}\n" +
                    $"Extension: {Path.GetExtension(staticResult.FilePath)}\n" +
                    $"Size: {staticResult.SizeBytes} bytes\n" +
                    $"Static score: {staticResult.Score}/100\n" +
                    $"Entropy (0-8, >7.2 = packed): {staticResult.Entropy:F2}\n" +
                    $"Is executable: {staticResult.IsExecutable}\n" +
                    $"In temp dir: {staticResult.FilePath.Contains("\\\\Temp\\\\")}\n" +
                    $"SHA256: {staticResult.Sha256}\n";

                var payload = JsonSerializer.Serialize(new
                {
                    model = Model,
                    prompt = prompt,
                    stream = false,
                    format = "json",
                    options = new { temperature = 0.1, num_predict = 200 }
                });

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync("/api/generate", content);
                if (!resp.IsSuccessStatusCode) return ("ai-http-error", 0f, resp.StatusCode.ToString());

                string body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                string text = doc.RootElement.GetProperty("response").GetString() ?? "{}";
                using var verdictDoc = JsonDocument.Parse(text);
                string verdict = verdictDoc.RootElement.TryGetProperty("verdict", out var v) ? v.GetString() ?? "unknown" : "unknown";
                float conf = verdictDoc.RootElement.TryGetProperty("confidence", out var c) ? c.GetSingle() : 0f;
                string reason = verdictDoc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                return (verdict, conf, reason);
            }
            catch (Exception ex)
            {
                return ("ai-error", 0f, ex.Message);
            }
        }
    }
}