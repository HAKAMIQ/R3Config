# Rpcs3History

Read-only evidence scripts over the frozen RPCS3 source (`git rev-list`, `git log`, `git grep` only; no fetch,
pull, checkout or network). Node.js, not part of the .NET toolchain.

- `verify-build-mapping.js <out.csv> <frozen-rpcs3-repo>`: checks `git rev-list --count <commitFull>` against every `rpcs3-builds.json`
  row. RPCS3 computes its build number exactly that way (`rpcs3/git-version.cmake`: `git rev-list HEAD --count`).
  The rule is authoritative for official GitHub rows; the historical archive rows use an older numbering and are
  reported separately.
- `setting-lifecycle.js <out.csv> <frozen-rpcs3-repo>`: lifecycle evidence (last change of a setting's name in `system_config.h` and the
  build number of that commit) for settings that no longer exist. It records lifecycle only; it never says a game needed
  the setting.
