# SentinelAI — Design Study: How the Big AVs Work

Researched before building, so every SentinelAI component maps to a
battle-tested concept from the industry leaders.

## 1. Detection layers across the industry

| Vendor | Signature | Heuristics | Behavioral | Cloud/ML | Sandbox |
|---|---|---|---|---|---|
| Windows Defender | hash + fuzzy | emulated launch | AMSI, controlled folders | massive MS cloud graph | built-in |
| Bitdefender | + ML local | Bitdefender Shield | Ransomware vaccine | 500M+ users telemetry | traffic light |
| Kaspersky | + YARA | system watcher | rollback of malicious actions | KSN cloud | full emulation |
| ESET | + LiveGrid | HIPS | hostname reputation | LiveGrid | x86 emulator |
| Norton | + SONAR | — | behavioral | 4B+ users | SONAR |
| Malwarebytes | signature-light | + heuristics strong | real-time web/shield | community | — |

**Takeaway:** every serious AV today = signatures (fast, cheap) + heuristics
(static scoring) + behavioral (runtime) + cloud/ML (long tail). No single
layer is sufficient. SentinelAI implements all four.

## 2. What SentinelAI implements (mapping)

### Layer 1 — Signatures → `StaticAnalyzer.TrustedHashes` + SHA256
Whitelist-first (defeats false positives on legit tools); every scanned file
is SHA256-hashed. Extension: YARA-rule support later for known-family match.

### Layer 2 — Static heuristics → `StaticAnalyzer.Analyze`
- Double-extension check (Troy-style `.jpg.exe`)
- Executables in %TEMP% / %APPDATA% (malware staging ground)
- Hidden attribute + executable (stealth)
- **Shannon entropy > 7.2 = packed/encrypted** (UPX, custom packers)
- Unsigned binaries in user-writable paths (Authenticode via wintrust)

### Layer 3 — Behavioral → `BehaviorMonitor`
- **Ransomware canaries** (decoy files in Documents/Pictures — if touched,
  alarm; same as Bitdefender's Ransomware Vaccine concept)
- **Impostor process detection** (lsass.exe running from %TEMP% = malware;
  same as Kaspersky System Watcher)
- **Hacking-tool blacklist** (mimikatz, procdump, Rubeus — LSASS dumpers)
- **Persistence sweep** (startup folders + Run keys every 5 min)
- Process from Temp alert (very common real-world TTP)

### Layer 4 — AI/ML → `AiClassifier` (Ollama, local)
- Ambiguous files (score 30–59) escalated to a local LLM with a
  security-analyst prompt; JSON verdict + confidence.
- Runs on the RTX 4090 via Ollama (`qwen2.5:latest`) — **fully local, no
  sample upload, no privacy leak** (contrast: Defender uploads to Microsoft
  cloud; SentinelAI keeps everything on-device).
- Long tail: AI catches what signatures miss; static layer catches what AI
  hallucinates. Two independent opinions on every ambiguous file.

### Layer 5 — Response → `ScanEngine.Quarantine`
- DPAPI (LocalMachine scope) encrypts the file into the vault, then deletes
  the original. Reversible only by the local admin (same machine), standard
  industry pattern.

### Layer 6 — Hardening (applied to the OS)
- Defender already hardened (CloudBlockLevel=High, PUA on, CFA on,
  NetworkProtection on, 14 ASR rules) — SentinelAI *complements* it, it does
  not replace it. Big-AV lesson: Defender's cloud is unbeatable at scale;
  our win is local AI + behavioral transparency + gaming-aware CPU cap.

## 3. Gaming-specific (the differentiator)

- `ScanAvgCPULoadFactor = 30` — Defender setting, caps background scan CPU
  so FPS doesn't tank during gameplay.
- Ransomware canaries protect save-game folders cheaply (no full FS filter
  driver needed).
- Process sweep ignores game processes (no false positives from mods).
- Future: game-mode API that pauses periodic scans while a fullscreen game
  is in the foreground.

## 4. Honest limitations (documented, not hidden)

- No proprietary cloud telemetry (Bitdefender/Defender see billions of
  samples/day; we see one machine). That's why Defender stays ON underneath.
- No real filesystem minifilter driver (would need a signed kernel driver —
  not feasible for a personal project). Canaries + scheduled scans are the
  user-mode equivalent.
- AI verdict is only as good as the local model. qwen2.5:7B is decent at
  reasoning over features but not a trained malware classifier. It's a
  second opinion, not ground truth.

## 5. Sources of inspiration (patterns borrowed)

- Bitdefender: ransomware canary idea, gaming-friendly profile
- Kaspersky: impostor-process detection, persistence sweep
- ESET: entropy heuristic, emulator-like static analysis ordering
- Defender: ASR rules, cloud-block philosophy, CPU cap during scans
- Malwarebytes: "keep it simple, focus on what built-in AV misses"
- OCRID / industry: DPAPI quarantine vault
