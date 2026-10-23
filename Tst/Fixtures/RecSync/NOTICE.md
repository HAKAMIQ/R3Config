# Recommendation sync fixtures

Test inputs for `RecSyncT`. They are never shipped and never used as real updates.

- `config-api-v1.json`: 120 game entries copied unchanged from a live response of
  `https://api.rpcs3.net/config/?api=v1` captured on 2026-10-20 (`timestamp`
  1792479016, 2287 games, 313898 bytes, SHA-256
  `A35F37C4518D82009F694A6D2CFFADAE57EDB8A7132B2AEC1D3B4101183D3A5C`). The
  selection is the first 110 serials in ordinal order plus serials the tests use.
  The data is derived from the RPCS3 Wiki (CC BY-SA 4.0).
- `wiki-*.json`: MediaWiki API `formatversion=2` responses (recentchanges with
  continuation, revisions with `slots.main`, embeddedin). The Wiki API returned
  HTTP 403 with a Cloudflare challenge on 2026-10-20, so these responses were not
  captured live: they follow the documented MediaWiki response shape and carry the
  page text of `Tst/Fixtures/Wiki/GtaIV-20260907.xml` and
  `Tst/Fixtures/Wiki/DemonsSouls-20241214.xml`, with one semantic edit (Grand Theft
  Auto IV), one prose-only edit (Demon's Souls) and one synthetic new page
  (`Fixture New Game`, serial `BLUS99901`).
