# SentinelAI

AI-powered security utility for Windows. Runs 100% locally on your GPU — no cloud, no telemetry, no data mining.

**Author:** otter9678-arch
**Version:** 0.1.0-beta
**Download:** [Releases](https://github.com/otter9678-arch/SentinelAI/releases)

## What it is

SentinelAI is a security utility that complements (not replaces) Windows Defender. It adds:

- **Real-time protection** — watches Startup, Desktop, Downloads, Temp, and Programs folders for new executables, auto-quarantines high-threat files
- **Static analysis** — SHA256, Shannon entropy (packer detection), Authenticode signature check, double-extension detection, Temp-dir heuristic
- **Behavioral monitoring** — ransomware canaries, impostor process detection, persistence sweep, hacking-tool blacklist
- **Local AI classifier** — ambiguous files (score 30–59) are classified by a local LLM via Ollama on your GPU. Nothing uploaded.
- **Quarantine vault** — DPAPI-encrypted vault, reversible only on the same machine
- **Gaming mode** — detects 70+ known games, pauses background sweeps so your FPS never dips
- **Hardware auto-detect** — adapts scan workers, AI concurrency, and memory budget to your CPU/RAM/GPU

## What it is NOT

- It is **not** certified antivirus software. It does not replace Windows Defender.
- It is **not** guaranteed to detect or block any specific threat.
- It does **not** upload any data to any cloud service.

## Requirements

- Windows 10/11 (64-bit)
- .NET Framework 4.7.2+
- [Ollama](https://ollama.com) with at least one model pulled (for AI classification)
- Recommended: NVIDIA/AMD GPU for fast AI inference

## Installation

**Manual:** run `SentinelAI-Setup-0.1.0-beta.exe`

The installer:
- Copies binaries to `Program Files\SentinelAI`
- Creates Start Menu shortcut
- Optionally registers a startup entry
- Creates config at `%LOCALAPPDATA%\SentinelAI`

## Usage

Launch JARVIS (the AI assistant) or SentinelAI.Service (background protection).

The real-time protection engine starts automatically with the service. New executables in watched directories are analyzed immediately — high-threat files are quarantined, suspicious files generate alerts.

## Commands / API

SentinelAI runs as a library + service. JARVIS (separate repo) calls into it via tool calls.

Key APIs:
- `ScanEngine.ScanAsync()` — full system scan
- `RealTimeProtection.Start()` — enable real-time watching
- `GamingMode.Start()` — pause sweeps while gaming
- `HardwareProfile.Summary()` — hardware report
- `ScanEngine.Quarantine()` — DPAPI-encrypt and vault a file

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

## Building from source

```
dotnet build src\SentinelAI.Core\SentinelAI.Core.csproj -c Release
dotnet build src\SentinelAI.Service\SentinelAI.Service.csproj -c Release
```

## License

Proprietary — maintained by otter9678-arch. See LICENSE.txt in the installer for full terms.