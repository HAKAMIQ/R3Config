# HistWiki

Offline, deterministic Historical Wiki Evidence analyzer (research tooling, not part of `R3Cfg.slnx`,
never reads or writes `App/Data`).

```text
dotnet run --project Script/HistWiki -c Release -- selftest
dotnet run --project Script/HistWiki -c Release -- run --export <46 history XML dir> --schema <Template:Config history XML> --snapshot <current snapshot XML> --out <dir> [--reference <History dir>] [--verify-determinism]
```

Rules enforced by the code: streaming `XmlReader`; no network; no fuzzy matching; a Game+Field timeline that
passes through a duplicate-key (or otherwise unparseable) revision is quarantined whole; identities come only from
`identity-declarations.csv` entries that the tool re-verifies against Template:Config's own history; effective
intervals are page-value intervals intersected with the *SourceFieldKey's* own schema-active intervals; Wiki
timestamps are only ever emitted as `WikiObservedFrom/Until` and never as RPCS3 build/commit boundaries.
