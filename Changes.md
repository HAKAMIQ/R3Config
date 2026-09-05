# Changelog

## Unreleased

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

## 2026-08-09

* Updated UI theme architecture guidance and release/RPCS3 integration contracts.

## 2026-08-08

* Corrected trusted `PARAM.SFO` test fixtures.

## 2026-08-07

* Hardened runtime, staging, mutation, input, security, patch-state, ToolTip, and selection regressions.

## 2026-07-30

* Fixed Canary runtime-key activation and added safe one-time PPU fingerprint capture.
