# Changelog

## 0.1.0

### License

* R3Config is free and open source under the GNU GPL v3 or later. There is no Trial, Premium, activation, licensing service, or device identification; every feature works offline for everyone.

### Safety

* The official `patch.yml` stays read-only when the RPCS3 folder is reached through a junction.
* The cross-process writer lock is held by a dedicated thread for the whole session, so it can no longer be released early when a worker thread ends, and a lock failure blocks the write instead of closing the app.
* A configuration or patch write that fails before touching any file no longer leaves an unused backup snapshot behind.
* Catalog export only writes `.json` files and replaces the chosen file instead of writing through a hardlink.
* The portable package check now requires every shipped data file and the GPL-3.0 license text.

### Recommendation updates

* The recommendation database updates quietly from the official RPCS3 config API into a local layer under `LocalApplicationData`; the bundled `cat.json` stays read-only and is the fallback. Unmatched, unsupported or conflicting values are held for review instead of applied.
* Synced recommendations are never pre-selected and are applied only through the existing preview, guard, backup and rollback path.
* Advanced tools → Data updates: automatic updates (on by default; off stops every automatic request), silent notifications by default, Check now, source status and a change log with old/new values and source links.
* The RPCS3 Wiki source is implemented (recent changes, revisions, reconciliation) but reports Blocked while the Wiki answers automated requests with a Cloudflare challenge.

### AppFlow

* Preserved the public compatibility API and added architecture guards to keep `App/Flow.cs` delegation-only.

### Settings

* Moved settings to `LocalApplicationData` with safe legacy migration, canonical paths, bounded persistence, locking, and corruption/concurrency protection.

### H-Canary

* Hardened discovery-state persistence, validation, locking, and malformed-state handling while preserving fingerprint and saved-binding behavior.

### R3Test

* Split the former monolithic `Program.cs` into focused test suites and shared test infrastructure while preserving behavior and CLI modes.

### Artemis

* Hardened provenance, archive/path validation, transactional writes, recovery, rollback, and external-change protection.
* Added the reviewed 23-rule dependency overlay while keeping ChiDreams as the only bundled executable Artemis source.

### UI and localization

* Improved Arabic/English bidirectional layout, patch actions, selection, ToolTips, Canary presentation, and H-Canary workflow.
* Refined Arabic RPCS3/Artemis notes while preserving technical terminology.
* Displayed dates (backup list, backup saved/restore/delete messages, recommendation update times) now follow the Windows Region format and calendar, captured at startup before the interface language is applied, and are always shown in local time. Dates inside Arabic sentences stay left-to-right. The interface language culture, number formatting, logs and every written file are unchanged; a Region change applies on the next launch.

## 2026-08-09

* Updated UI theme architecture guidance and release/RPCS3 integration contracts.

## 2026-08-08

* Corrected trusted `PARAM.SFO` test fixtures.

## 2026-08-07

* Hardened runtime, staging, mutation, input, security, patch-state, ToolTip, and selection regressions.

## 2026-07-30

* Fixed Canary runtime-key activation and added safe one-time PPU fingerprint capture.
