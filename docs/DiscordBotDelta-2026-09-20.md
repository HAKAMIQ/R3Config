# discord-bot delta audit (2026-09-20)

Reference: `Downloads/discord-bot-master.zip` (RPCS3 compatibility bot, 409 files, read-only, extracted outside the repo).
Baseline: the current R3Config working tree (Domain 5: `RuntimeContext`, `Rpcs3LogParser`/`LogRules`/`DiagnosticEngine`, `diagnostics-kb.json`, `rpcs3-builds.json`, `rpcn-evidence.json`).
Corroboration sources used: 37 real RPCS3 logs from 13 local GitHub issues (608 MB decompressed), the full RPCS3 source tree (`R3Config-Evidence/RPCS3-Source-Full`), the canonical Sept-2026 Wiki XML. No network.

## Licence constraint

The bot is **LGPL-2.1**; R3Config is GPL-3.0-or-later and keeps a clean-room boundary to the bot. Nothing was copied: every rule added here was re-derived from real log lines and RPCS3 behaviour. Tables that ship as bot data (`win32_error_codes_v23.0.txt`, `cpu_tier_list.conf`, `confusables.txt`, `metacritic_ps3.json`) must NOT be copied verbatim; if ever needed they must be re-derived from their primary source (Microsoft docs, rpcs3.net, Unicode).

## Answers to the delta questions

1. **Parser design.** Bot: `System.IO.Pipelines` reader, a 50-line sliding window per section, an Aho-Corasick trigger prefilter that runs one regex per hit over the window, multi-value and count-only observation sets. R3Config: a lazy `StreamReader` line stream with a byte budget (256 MB, `Truncated`), gzip support, one single-pass state machine (`Rpcs3LogParser.Machine`) with a pending-header mechanism, bounded continuation lines, per-boot `Segment`s (BootGame or serial change), per-signature counting and a small explicit rule table. Measured on real logs: 11.2 MB / 171k lines in 279 ms, 1 MB / 64k lines in 290 ms. The prefilter and pipeline exist in the bot because it runs 188 regexes over untrusted Discord uploads; R3Config has a handful of anchored regexes over a local file. **REFERENCE_ONLY** (performance design not needed; multi-boot, multi-value and count-only are already covered).
2. **The 188 GeneratedRegex patterns** (`LogParser.RegexPatterns.cs`; 189 counts one attribute in a comment) are classified one by one in `DiscordBotDelta-2026-09-20-regex.csv` with real-log hit counts and RPCS3-source presence. Summary: 86 ALREADY_COVERED (69 of them are "Used configuration" key/value lines that `LogSetting` captures generically), 9 EXTEND_EXISTING (implemented now), 31 NEW_CAPABILITY (queued, not implemented), 38 REFERENCE_ONLY, 11 SUPERSEDED (LDR: variants and per-flag xfloat lines), 12 OBSOLETE, 1 NOT_APPLICABLE (Installation ID). Superseded and obsolete count as NOT_APPLICABLE in the five-class scheme.
3. **RuntimeContext fields missing** vs the bot: PS3 firmware version, renderer GPU driver version, package VERSION, configuration mode / custom config file, boot media (ISO), ISO key/encryption, patches applied by RPCS3. All added to `LogRunContext` (log-stated, never inferred). Still absent: installed-firmware-missing observation, compat database path (install location as RPCS3 printed it), OS structured fields (host `HwProbe` already owns the OS), CPU model/tier classification.
4. **Normalization rules missing:** none that matter. `LogRules.NormalizeMessage` already strips addresses, hashes, paths, IPs and numbers. The bot's Unicode work (`confusables`, homoglyphs, zalgo) is for Discord message moderation: NOT_APPLICABLE.
5. **GeneralNotes** (109 + 3 emissions): about 30 are factual observations (path length over 260, RPCS3 under Program Files / a sync folder / WinRAR, booted from ISO / disc / ELF, PSP/PS2/minis category, firmware missing, corrupted ISO, custom firmware, patch.yml syntax error); the rest are advice or opinions (CPU/GPU "too weak", RAM, driver-version minimums, "update your build", Windows-version nagging). Observations are candidates for typed log observations (queued). Advice is REFERENCE_ONLY: it is uncorroborated (see 8) and R3Config's policy forbids invented recommendations.
6. **WeirdSettings** (186 + 30 emissions): almost entirely "set X to Y" advice and game-specific serial lists (Do not use DX12, Vulkan recommended, Resolution 1280x720, Write Color Buffers, SPU decoder, clocks, 60 fps patch pairing, ...). REFERENCE_ONLY.
7. **Overlap / conflict.** Overlap: fatal-error classification, sys_fs failures, hashes, settings. Conflicts found from source: (a) the bot reports "Disc decryption key is missing" whenever the key file was not found, but real logs print `enc type: NONE` next to that line for unencrypted ISOs, so the bot overclaims; R3Config now records the key state and the ISO encryption type as separate observations and raises no problem. (b) `Selected config` appears twice per boot when a custom config exists and the bot's last-value-wins state hides it, whereas R3Config used to MERGE both `Used configuration` blocks (duplicate paths, first-wins lookups): a real defect, fixed.
8. **Corroboration.** Of the bot's 188 triggers, 128 occur in the local real logs and 97 have a literal in the RPCS3 source (config-block lines are generated dynamically, so their source check is meaningless; the logs confirm them). The canonical Wiki has 0 mentions of the bot's global advice topics (DX12, "Vulkan recommended", Windows/RAM/firmware minimums, Program Files, OneDrive), so those rules are uncorroborated by it. The RPCN evidence and `diagnostics-kb.json` are not touched by any bot rule (the bot has no RPCN concept). GitHub Issues: only 13 issues carry log attachments locally; the bot's regression knowledge is not comparable to `diagnostics-kb.json` (bot has no per-build boundary data).
9. **IRD / PSN / Win32 / syscall / hardware / performance / archive / OCR / Unicode.**
   - IRD (dump validation by known file list): needs a network download and third-party data. REFERENCE_ONLY.
   - PSN metadata and title updates (`PsnClient`, `TitleUpdateInfoProvider`): network lookups. R3Config's runtime must stay offline. REFERENCE_ONLY.
   - Win32 error text for `Verification failed (e=0x..)`: offline table derived from Microsoft docs (bot data is LGPL, re-derive if wanted). NEW_CAPABILITY, queued.
   - Syscall statistics (`SyscallInfoProvider`): bot-side statistics. REFERENCE_ONLY.
   - Hardware: the bot ranks CPUs with the rpcs3.net tier list; R3Config's `HwTier` is a threshold heuristic. Replacing it with the official list needs a fresh capture of rpcs3.net/cputierlist (not in the local evidence). Queued as EXTEND_EXISTING.
   - Performance sensor / CPU-RAM usage stats: REFERENCE_ONLY.
   - Archive handlers (zip/rar/7z/tar/gzip) and source handlers (Discord attachments, Mega, Pastebin, ...): R3Config reads local files and `.log.gz` only. NOT_APPLICABLE.
   - OCR / vision (`Commands/Vision.cs`), fortunes, warnings, invites, starbucks, name lists: NOT_APPLICABLE (Discord features).

## Implemented in this tranche (all fact-only, no advice)

- `LogRunContext` on `LogSession` (`Core/Mdl/LogDiag.cs`) and its extraction in `Rpcs3LogParser`; shown as rows in the Log diagnostics session block (`LogDiagVm.BuildSession`) only when the log stated them; EN and AR keys.
- Effective configuration: a later `Used configuration` block replaces an earlier one (fixes duplicated settings, the 500-setting cap being reached, and a stale renderer). Verified on real logs: settings 500 -> 252 / 255.
- Driver version by exact adapter name (never "the first adapter").
- `rpcn-evidence.json` manifest (schema, generator identity, source snapshot dates, SHA-256 of every input, counts by role, claim, identity and distinct subjects) and structured rejection diagnostics (`DiagnosticEvidenceRejectedException`: rule, file, record index, reason; the UI log reports it and the catalog stays empty, never partial).

## What "457 records" means

`rpcn-evidence.json` holds 457 evidence records. A record is one dated claim about one source entry, NOT one game: the artifact describes 68 browser entries and 98 tracker rows (166 subjects, `SubjectsByRole` in the manifest); one entry carries up to four claims (listing, online-now, 24 h peak, all-time peak) and a game may have several entries.

## Deliberately NOT ported

- Pipelines + Aho-Corasick + sliding window: measured throughput is far above need; adding it would only add code.
- Per-setting regexes (69 of the 188): `LogSetting(path, value)` already captures every printed setting exactly.
- Advice engines (GeneralNotes/WeirdSettings recommendations, CPU/GPU/RAM/driver nags): uncorroborated by the canonical Wiki and contrary to the no-invented-fix policy.
- LDR-prefixed and per-flag legacy patterns: absent from current logs.
- Bot data tables (Win32 codes, CPU tiers, confusables, metacritic): LGPL-licensed data.
- IRD/PSN/update lookups, archive/source handlers, moderation, OCR: network or Discord features.

## Queued next (needs a real-log fixture per rule before it ships)

Named observations for explicit RPCS3 lines: firmware not installed, RAP/license missing, pad handler missing, audio backend init failed, shader compile/link failed, GPU/driver feature unsupported, descriptor-pool fragmentation, EDAT/SELF decrypt failures, unimplemented syscall, patch.yml syntax error, corrupted ISO. Then the official CPU tier list as exact-model evidence.
