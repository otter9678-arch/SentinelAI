# SentinelAI

AI-powered security utility for Windows. Runs 100% locally on your GPU — no cloud, no telemetry, no data mining.

**Author:** otter9678-arch
**Version:** 0.1.0-beta
**Download:** [Releases](https://github.com/otter9678-arch/SentinelAI/releases)

## Quick Start (3 steps)

1. **Install Ollama** — download from [ollama.com](https://ollama.com), then pull a model:
   ```
   ollama pull gemma4:26b
   ```
2. **Download** `SentinelAI-Setup-0.1.0-beta.exe` from [Releases](https://github.com/otter9678-arch/SentinelAI/releases)
3. **Run the installer** — it sets up everything else automatically

That's it. JARVIS launches after install. No other dependencies needed.

## What it is

SentinelAI is a security utility that complements (not replaces) Windows Defender. It adds:

- **Real-time protection** — watches Startup, Desktop, Downloads, Temp, and Programs folders for new executables, auto-quarantines high-threat files
- **Static analysis** — SHA256, Shannon entropy (packer detection), Authenticode signature check, double-extension detection, Temp-dir heuristic
- **Behavioral monitoring** — ransomware canaries, impostor process detection, persistence sweep, hacking-tool blacklist
- **Local AI classifier** — ambiguous files (score 30–59) are classified by a local LLM via Ollama on your GPU. Nothing uploaded.
- **Quarantine vault** — DPAPI-encrypted vault, reversible only on the same machine
- **Gaming mode** — detects 70+ known games, pauses background sweeps so your FPS never dips
- **Hardware auto-detect** — adapts scan workers, AI concurrency, and memory budget to your CPU/RAM/GPU. Works on Intel, AMD, NVIDIA, Apple Silicon, ARM.

## What it is NOT

- It is **not** certified antivirus software. It does not replace Windows Defender.
- It is **not** guaranteed to detect or block any specific threat.
- It does **not** upload any data to any cloud service.

## System Requirements

| Component | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 64-bit | Windows 11 64-bit |
| CPU | 4 cores | 8+ cores |
| RAM | 8 GB | 16+ GB |
| GPU | Any (for AI inference) | NVIDIA RTX / AMD Radeon |
| Disk | 100 MB free | 500 MB free |
| Ollama | Any model | gemma4:26b (4.7 GB) |
| .NET | 4.7.2 (pre-installed on Win 10/11) | — |

**Works on:** Intel, AMD, and ARM64 processors. NVIDIA, AMD, and Intel GPUs. No specific brand required — auto-detects your hardware and adapts.

## Installation Options

### Manual (recommended)
Run `SentinelAI-Setup-0.1.0-beta.exe` from [Releases](https://github.com/otter9678-arch/SentinelAI/releases).

### From source
```bash
git clone https://github.com/otter9678-arch/SentinelAI.git
cd SentinelAI
dotnet build src\SentinelAI.Core\SentinelAI.Core.csproj -c Release
dotnet build src\SentinelAI.Service\SentinelAI.Service.csproj -c Release
dotnet publish src\JarvisAI\src\Jarvis.AI\Jarvis.AI.csproj -c Release -o publish
```

## Usage

Launch JARVIS (the AI assistant) or SentinelAI.Service (background protection).

The real-time protection engine starts automatically with the service. New executables in watched directories are analyzed immediately — high-threat files are quarantined, suspicious files generate alerts.

## Architecture

```
┌─────────────────────────────────────────────┐
│  JARVIS (separate repo — assistant/UI)     │
├─────────────────────────────────────────────┤
│  SentinelAI.Core                            │
│  ├── StaticAnalyzer     (signature/heuristic)│
│  ├── BehaviorMonitor    (runtime detection)  │
│  ├── RealTimeProtection (file watcher)       │
│  ├── ScanEngine         (parallel scanner)   │
│  ├── AiClassifier       (Ollama GPU)         │
│  ├── GamingMode         (FPS protection)     │
│  ├── HardwareProfile    (auto-tune)          │
│  └── AppConfig/Log      (infrastructure)     │
├─────────────────────────────────────────────┤
│  SentinelAI.Service (Windows service host)  │
└─────────────────────────────────────────────┘
```

## Privacy

All scanning, analysis, and AI inference runs locally. The only outbound network requests are:
- Ollama API calls (localhost:11434) — optional, for AI classification
- No file contents, no personal data, no telemetry ever leave the machine

## Configuration

Config file: `%LOCALAPPDATA%\SentinelAI\config.json`

| Key | Default | Effect |
|---|---|---|
| OllamaUrl | http://localhost:11434 | Local AI endpoint |
| OllamaChatModel | gemma4:26b | Model for classification |
| OllamaVisionModel | gemma4:26b | Model for screen vision |
| MatterHubUrl | http://homeassistant.local:8123 | Smart home hub |
| GamingCpuCap | 30 | Max CPU % during scans while gaming |

## Hardware Support

SentinelAI auto-detects and adapts to any hardware:

- **CPU:** Intel, AMD, ARM64 — uses `Environment.ProcessorCount` (works on everything)
- **GPU:** NVIDIA, AMD, Intel, integrated — auto-detected via nvidia-smi + WMIC fallback
- **RAM:** Auto-scales scan worker count and AI concurrency to available memory
- **Disk:** Works on HDD, SATA SSD, NVMe — auto-throttles on slower drives
- **OS:** Windows 10/11 x64 + ARM64 (macOS/Linux planned)

No configuration needed — SentinelAI benchmarks your hardware on first run and adjusts automatically.

## License

Proprietary — maintained by otter9678-arch. See LICENSE.txt in the installer for full terms.