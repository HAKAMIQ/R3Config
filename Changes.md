# Changelog

## Unreleased

### AppFlow compatibility facade

* Kept the public compatibility API unchanged while removing redundant namespace qualification and limiting `CA1822` suppression to the two legacy instance methods that delegate to static recommendation-catalog operations.
* Added an architecture regression gate that rejects filesystem, process, or JSON implementation logic in `App/Flow.cs`, keeping the facade delegation-only.

### Settings persistence

* Moved settings to `LocalApplicationData` with safe migration from the legacy roaming location.
* Added canonical RPCS3 path handling, bounded UTF-8/JSON persistence, cross-process locking, and corruption/concurrency regressions.

### H-Canary

* Hardened `HCanDiscoveryStore` with path-scoped cross-process locking, canonical session/hash validation, bounded persistence, and malformed-state handling.
* Preserved one-time Canary fingerprint capture and saved-binding behavior.

### R3Test

* Refactored the former 10,809-line `Program.cs` into responsibility-based test suites.
* Extracted `TestRunner`, `TestContext`, `TestAssert`, `TestFixtures`, and `TestJson`.
* Preserved test order, CLI modes, fixtures, and behavior while adding architecture gates against future monolith growth.

### Artemis

* Hardened Artemis import provenance, archive validation, canonical-path protection, and transactional output handling.
* Added interruption recovery, rollback, external-change protection, and expanded regression coverage.
* Added the reviewed 23-rule dependency overlay without modifying the executable `art.yml` catalog.
* Kept the ChiDreams Artemis Patch Collection as the only bundled Artemis executable source.

### UI and localization

* Completed Arabic/English bidirectional layout handling while keeping technical values LTR.
* Improved patch-page actions, selection behavior, ToolTips, Canary density, and H-Canary authoring workflow.
* Polished Arabic RPCS3/Artemis notes while preserving canonical technical terminology.

## 2026-08-09

* Consolidated WPF resource-order guidance into `docs/Arch/UiTheme.md`.
* Cleaned release verification and RPCS3 integration contracts.

## 2026-08-08

* Corrected trusted `PARAM.SFO` fixtures for game-directory limit tests.

## 2026-08-07

* Hardened runtime-path, mutation, staging, bounded-input, and release-security contracts.
* Fixed H-Canary/imported-patch fixture paths and game scanner boundary fixtures.
* Improved patch action regressions, Canary state handling, ToolTips, and selection controls.

## 2026-07-30

* Corrected Canary runtime-key activation behavior.
* Added immutable one-time selected-game PPU fingerprint capture and safe RPCS3 process ownership handling.
