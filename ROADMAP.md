# SentinelAI + JARVIS — Development Roadmap

**Last updated:** 2026-09-04
**Status:** v0.1.0-beta (SentinelAI) + v0.2.0-beta (JARVIS) released

## What's already shipped

| Repo | Version | What it does |
|---|---|---|
| [SentinelAI](https://github.com/otter9678-arch/SentinelAI) | v0.1.0-beta | AI security utility: real-time protection, behavioral monitoring, ransomware canaries, local AI classifier, quarantine vault, gaming mode, hardware auto-detect |
| [JarvisAI](https://github.com/otter9678-arch/JarvisAI) | v0.2.0-beta | Personal AI assistant: voice I/O, screen vision (any game), gaming mode, smart home (Hisense AC + Matter), weather, self-learning, productivity, web tools, 13 AI providers, automation |
| [CapBot](https://github.com/otter9678-arch/CapBot) | v1.2.2 | PULSAR game mod (separate project) |
| [pulsar-mod-loader](https://github.com/otter9678-arch/pulsar-mod-loader) | fork | Private fork with docs + GUI hardening |

## Phase 1 — Stabilize (current phase)

### SentinelAI
- [ ] Replace hardcoded paths with AppConfig (partially done)
- [ ] Add Inno Setup installer (done: `SentinelAI-Setup-0.1.0-beta.exe`)
- [ ] Global exception handler with logs written to `%LOCALAPPDATA%\SentinelAI\Logs` (done: Log.cs)
- [ ] Auto-update via VersionLink JSON (done: ModUpdater pattern in CapBot — port to SentinelAI)
- [ ] Add unit tests for StaticAnalyzer, BehaviorMonitor, ScanEngine (xUnit)
- [ ] Test on a clean Windows 11 VM
- [ ] Test on Windows ARM64
- [ ] Test on a machine with no GPU (AI features gracefully disabled)

### JARVIS
- [ ] Test voice input with whisper.cpp (need to install whisper.cpp)
- [ ] Test screen vision with a real game running
- [ ] Test smart home with a real Hisense AC unit
- [ ] Add Matter hub configuration UI
- [ ] Add weather location persistence (save lat/lon between sessions)
- [ ] Wire SelfLearning.py into the C# ToolExecutor properly

### GameNetDiag
- [ ] Add more game server targets (Steam, EA, Riot)
- [ ] Add historical latency graph
- [ ] Add alert sounds

## Phase 2 — Cross-platform (next)

- [ ] **macOS**: Replace System.Speech with eSpeak/Azure, replace Authenticode with GPG, replace Win32 screen capture with SkiaSharp
- [ ] **Linux**: Replace System.Speech with eSpeak, replace Windows service with systemd
- [ ] **Web companion**: Local web dashboard JARVIS exposes on LAN (phone/tablet access)
- [ ] Test on ARM64 Windows

## Phase 3 — Monetize (after beta stabilizes)

- [ ] Recruit 5-10 beta testers (free)
- [ ] Fix what actually breaks
- [ ] Register sole proprietorship in Canada
- [ ] Write PIPEDA-compliant privacy policy
- [ ] Write EULA
- [ ] Code-sign binaries (DigiCert/Sectigo ~$300/yr)
- [ ] Stripe/Paddle billing integration
- [ ] Free tier: JARVIS chat + basic monitoring + gaming mode
- [ ] Paid tier ($5-10 CAD/month): cloud AI, advanced automation, priority support
- [ ] Lifetime tier ($99 CAD): one-time, first 100 supporters
- [ ] Organic launch (Reddit, Discord, X) — no paid ads until stable

## Phase 4 — Advanced features (backlog)

- [ ] YARA rule scanning (industry-standard signature format)
- [ ] Network IDS (detect port scans, ARP spoofing, DNS tunneling)
- [ ] Browser extension scanner
- [ ] Email attachment scanner
- [ ] USB autorun blocker
- [ ] Rootkit detector (heuristic)
- [ ] Vulnerability scanner (outdated software, open ports)
- [ ] Privacy auditor (what apps have access to what)
- [ ] Firewall rule analyzer
- [ ] GameAI: switch to stable-baselines3 PPO for real training
- [ ] GameAI: add OCR-based reward shaping for games without APIs
- [ ] JARVIS: Whisper large model for better voice transcription
- [ ] JARVIS: WPF/WinUI 3 GUI (currently console-only)

## Cost estimate to first paid user

| Item | Cost |
|---|---|
| Code signing cert (1 yr) | ~$300 |
| Domain + hosting | ~$50/yr |
| Inno Setup / GitHub | Free |
| Stripe fees | 2.9% + $0.30/txn |
| Incorporation (optional) | ~$200-800 |
| **Total** | **~$350-1,150** |

## Known limitations (documented, not hidden)

- No kernel driver / minifilter (requires signed driver — personal project)
- No proprietary cloud telemetry network (single machine)
- AI verdicts are supplementary (local LLM, not trained malware classifier)
- Windows-only for now (macOS/Linux planned)
- Game vision works on any game but has no game-specific API hooks

## Honest positioning

**Not:** "Certified antivirus better than Bitdefender"
**Yes:** "Private AI assistant with built-in security features that runs on your GPU"