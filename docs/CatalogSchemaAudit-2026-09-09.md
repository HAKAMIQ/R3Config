# Shipped catalog / RPCS3 schema audit — 2026-09-09

## Scope and evidence identity

This audit inspected the current dirty working tree, without restoring files, regenerating `cat.json`, changing production mappings, or modifying RPCS3. The read-only RPCS3 reference HEAD was verified as `54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6`; its working tree was clean. Source declarations and formatter cases were read from that tree once and reviewed by exact section/key, not by display label.

| Input | SHA-256 |
|---|---|
| Current `App/Data/cat.json` | `A8DE71B58BE488C406B8F368A0D148300C81A63D5F44641538BEE78CBC3F5232` |
| Pinned `rpcs3/Emu/system_config.h` | `CD33A151A8FF5DC3A8B0919610448E93C74144E1D6336983B71EE8E028BA34ED` |
| Pinned `rpcs3/Emu/system_config_types.cpp` | `EB2F25F8969DBD5C798CC8B961425B2C51C382D9F3D507FA6DF98AF5097D47F9` |

> **SUPERSEDED provenance policy.** This audit records the historical state in which June 2026 was the intended canonical baseline. The project owner has since made the September 2026 snapshot `RPCS3+Wiki-20260907205217.xml` (SHA-256 `410AACCBD9A77637671D6DEB6768CD1D7E7506FF83C8197265BEEA34CACB2097`, 2026-09-07) the production canonical Wiki source; `WikiSvc.CanonicalSnapshot*` and `WIKI_CANONICAL_PARITY_GATE` pin it (see docs/Arch/RelTest.md). The June statements below are historical and no longer a production requirement.

The catalog was already September-derived at task start: `DataVersion=2026-09-07`, `SourceHash=410AACCBD9A77637671D6DEB6768CD1D7E7506FF83C8197265BEEA34CACB2097`. This is a pre-existing provenance difference, **NEWER_EVIDENCE / DELTA ONLY**, not authority to replace the June project snapshot. `WikiSvc.CanonicalSnapshotFileName`, date, SHA-256, and canonical parity requirements still pin `RPCS3+Wiki-20260613075133.xml`, `2026-06-13`, `254A0A41870C54048C94F61B2FF5AE9CEEFE7CC55F246C331DF9382B564BE452`. No canonical source or shipped catalog bytes were changed by this audit. Schema validity does not establish canonical Wiki parity or recommendation correctness against June.

## Deterministic accounting

Key counts are distinct exact (section, key) pairs. Value counts are all shipped change occurrences; repeated settings across serials count separately. Serialization checks additionally cover every distinct (section, key, value) tuple. Composite cases count logical serial/ZCULL recommendations, each containing two physical settings. No last-write-wins conflict resolution is used.

| Metric | Result |
|---|---:|
| CATALOG_SERIALS | 8540 |
| CATALOG_CHANGES | 7881 |
| DISTINCT_SETTINGS | 57 |
| KNOWN_KEYS | 57 |
| UNKNOWN_KEYS | 0 |
| VALID_VALUES | 7881 |
| INVALID_VALUES | 0 |
| RANGE_VIOLATIONS | 0 |
| SERIALIZATION_MISMATCHES | Pending fresh R3Test ShippedSchema execution |
| COMPOSITE_MAPPING_CASES | 535 |
| AMBIGUOUS_CASES | 0 |
| DISTINCT_SETTING_VALUES | 113 |
| SERIALS_WITH_CHANGES | 3129 |

The 57 settings comprise 24 booleans, 22 enums, 10 integers and one floating-point setting. All 7,881 values fit the pinned declaration/formatter contract. Catalog categories are 870 MANDATORY_GAME_CONFIG and 7,011 GENERAL_RECOMMENDATION change occurrences. PATCH_SPECIFIC, INFORMATIONAL_NOTE, VERSION_SPECIFIC, UNSUPPORTED and AMBIGUOUS remain distinct non-actionable classifications; zero instances of those classes are shipped as catalog changes. This does not turn their source evidence into default configuration.

ZCULL breakdown: 56 Precise (`false/true`), 110 Approximate (`false/false`), 369 Relaxed (`true/false`), ordered as `Relaxed ZCULL Sync / Accurate ZCULL stats`. Total 535 logical cases / 1,070 target rows. The impossible `true/true` combination is rejected.

## Complete setting mapping

Each row passed source inspection; action for every row is **NO ACTION**. Numeric ranges are declaration bounds, not recommended values. Enums list canonical serialized formatter strings; compile-time conditions are retained explicitly.

| Source Wiki key / alias | RPCS3 section / key | Pinned serialized contract | Shipped distinct values | Change occurrences | Exact pinned source |
|---|---|---|---|---:|---|
| `audio format` | `Audio / Audio Format` | enum: `Stereo`, `Surround 5.1`, `Surround 7.1`, `Automatic`, `Manual` | `Automatic` | 4 | [schema 258](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L258) / [formatter 592](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L592) |
| `audio buffer duration` | `Audio / Desired Audio Buffer Duration` | int: 4..250 | `10`, `20`, `250` | 36 | [schema 264](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L264) |
| `enable buffering` | `Audio / Enable Buffering` | boolean: `true`, `false` | `false` | 76 | [schema 263](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L263) |
| `enable time stretching` | `Audio / Enable Time Stretching` | boolean: `true`, `false` | `true` | 6 | [schema 265](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L265) |
| `microphone type` | `Audio / Microphone Type` | enum: `Null`, `Standard`, `SingStar`, `Real SingStar`, `Rocksmith` | `SingStar` | 27 | [schema 268](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L268) / [formatter 356](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L356) |
| `accurate ppu 128 reservations` | `Core / Accurate PPU 128-byte Reservation Op Max Length` | int: -1..14 | `-1` | 8 | [schema 65](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L65) |
| `accurate rsx reservation access` | `Core / Accurate RSX reservation access` | boolean: `true`, `false` | `true` | 179 | [schema 42](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L42) |
| `accurate spu dma` | `Core / Accurate SPU DMA` | boolean: `true`, `false` | `true` | 19 | [schema 39](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L39) |
| `debug console mode` | `Core / Debug Console Mode` | boolean: `true`, `false` | `true` | 8 | [schema 75](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L75) |
| `disable spu getllar spin optimization` | `Core / Disable SPU GETLLAR Spin Optimization` | boolean: `true`, `false` | `true` | 24 | [schema 31](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L31) |
| `maximum spurs threads` | `Core / Max SPURS Threads` | int: 1..6 | `3`, `4` | 19 | [schema 37](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L37) |
| `ppu decoder` | `Core / PPU Decoder` | enum: `Interpreter (static)`, `Recompiler (LLVM)` | `Interpreter (static)` | 135 | [schema 17](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L17) / [formatter 526](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L526) |
| `ppu thread count` | `Core / PPU Threads` | int: 1..8 | `1` | 8 | [schema 18](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L18) |
| `preferred spu threads` | `Core / Preferred SPU Threads` | int: 0..6 | `1`, `2` | 11 | [schema 34](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L34) |
| `rsx fifo accuracy` | `Core / RSX FIFO Fetch Accuracy` | enum: `Fast`, `Atomic`, `Ordered & Atomic`, `PS3` | `Ordered & Atomic` | 23 | [schema 56](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L56) / [formatter 222](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L222) |
| `sleep timers accuracy` | `Core / Sleep Timers Accuracy` | enum: `As Host`, `Usleep Only`, `All Timers` | `As Host`, `Usleep Only` | 299 | [schema 88,90](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L88) / [formatter 239](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L239) |
| `spu block size` | `Core / SPU Block Size` | enum: `Safe`, `Mega`, `Giga` | `Mega`, `Safe` | 219 | [schema 38](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L38) / [formatter 318](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L318) |
| `spu decoder` | `Core / SPU Decoder` | enum: `Interpreter (static)`, `Interpreter (dynamic)`, `Recompiler (ASMJIT)`, `Recompiler (LLVM)` | `Interpreter (dynamic)`, `Interpreter (static)`, `Recompiler (ASMJIT)` | 81 | [schema 27](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L27) / [formatter 301](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L301) |
| `enable spu loop detection` | `Core / SPU loop detection` | boolean: `true`, `false` | `true` | 11 | [schema 36](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L36) |
| `spu xfloat accuracy`, `xfloat accuracy` | `Core / SPU XFloat Accuracy` | enum: `Accurate`, `Approximate`, `Relaxed`, `Inaccurate` | `Accurate`, `Approximate`, `Relaxed` | 182 | [schema 64](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L64) / [formatter 714](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L714) |
| `camera settings` | `Input/Output / Camera` | enum: `Null`, `Fake`, `Qt`, `SDL` (HAVE_SDL3 only) | `Qt` | 20 | [schema 279](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L279) / [formatter 374](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L374) |
| `camera input` | `Input/Output / Camera type` | enum: `Unknown`, `EyeToy`, `PS Eye`, `UVC 1.1` | `PS Eye` | 20 | [schema 280](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L280) / [formatter 408](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L408) |
| `keyboard handler` | `Input/Output / Keyboard` | enum: `Null`, `Basic` | `Basic` | 27 | [schema 277](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L277) / [formatter 113](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L113) |
| `mouse handler` | `Input/Output / Mouse` | enum: `Null`, `Basic`, `Raw` | `Null` | 3 | [schema 278](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L278) / [formatter 11](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L11) |
| `move handler` | `Input/Output / Move` | enum: `Null`, `Real`, `Fake`, `Mouse`, `Raw Mouse`, `Gun` (HAVE_LIBEVDEV only) | `Fake`, `Mouse` | 32 | [schema 284](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L284) / [formatter 442](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L442) |
| `network status` | `Net / Internet enabled` | enum: `Disconnected`, `Connected` | `Connected` | 36 | [schema 331](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L331) / [formatter 270](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L270) |
| `psn status` | `Net / PSN status` | enum: `Disconnected`, `Simulated`, `RPCN` | `RPCN`, `Simulated` | 43 | [schema 339](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L339) / [formatter 285](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L285) |
| `zcull accuracy` | `Video / Accurate ZCULL stats` | boolean: `true`, `false` | `false`, `true` | 535 | [schema 163](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L163) |
| `allow host gpu labels` | `Video / Allow Host GPU Labels` | boolean: `true`, `false` | `true` | 89 | [schema 176](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L176) |
| `anisotropic filter` | `Video / Anisotropic Filter Override` | int: 0..16 | `0`, `16` | 275 | [schema 167](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L167) |
| `disable asynchronous memory manager` | `Video / Disable Asynchronous Memory Manager` | boolean: `true`, `false` | `true` | 6 | [schema 178](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L178) |
| `disable vertex cache` | `Video / Disable Vertex Cache` | boolean: `true`, `false` | `true` | 126 | [schema 147](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L147) |
| `disable zcull occlusion queries` | `Video / Disable ZCull Occlusion Queries` | boolean: `true`, `false` | `true` | 88 | [schema 145](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L145) |
| `driver wake-up delay` | `Video / Driver Wake-Up Delay` | int: 0..16667 | `100`, `120`, `200`, `3000`, `400`, `50`, `800` | 80 | [schema 172](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L172) |
| `emulate special depth comparison` | `Video / Emulate Special Depth Comparison` | boolean: `true`, `false` | `true` | 63 | [schema 149](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L149) |
| `force cpu blit emulation` | `Video / Force CPU Blit` | boolean: `true`, `false` | `true` | 75 | [schema 151](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L151) |
| `framelimit` | `Video / Frame limit` | enum: `Off`, `30`, `50`, `60`, `120`, `Display`, `Auto`, `PS3 Native`, `Infinite` | `30`, `60`, `Auto`, `Infinite`, `Off`, `PS3 Native` | 281 | [schema 124](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L124) / [formatter 334](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L334) |
| `handle rsx memory tiling` | `Video / Handle RSX Memory Tiling` | boolean: `true`, `false` | `true` | 140 | [schema 135](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L135) |
| `resolution scale threshold` | `Video / Minimum Scalable Dimension` | int: 1..1024 | `1`, `1024`, `128`, `135`, `16`, `160`, `240`, `256`, `288`, `302`, `320`, `322`, `35`, `400`, `440`, `512`, `576`, `592`, `640`, `720`, `80` | 717 | [schema 169](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L169) |
| `anti-aliasing` | `Video / MSAA` | enum: `Disabled`, `Auto` | `Auto`, `Disabled` | 147 | [schema 126](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L126) / [formatter 82](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L82) |
| `multithreaded rsx` | `Video / Multithreaded RSX` | boolean: `true`, `false` | `false`, `true` | 237 | [schema 156](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L156) |
| `read color buffers` | `Video / Read Color Buffers` | boolean: `true`, `false` | `false`, `true` | 365 | [schema 133](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L133) |
| `read depth buffers` | `Video / Read Depth Buffer` | boolean: `true`, `false` | `true` | 78 | [schema 134](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L134) |
| `zcull accuracy` | `Video / Relaxed ZCULL Sync` | boolean: `true`, `false` | `false`, `true` | 535 | [schema 157](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L157) |
| `renderer` | `Video / Renderer` | enum: `Null`, `OpenGL`, `Vulkan` | `OpenGL` | 9 | [schema 117,119](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L117) / [formatter 27](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L27) |
| `default resolution` | `Video / Resolution` | enum: `1920x1080`, `1920x1080i`, `1280x720`, `720x480`, `720x480i`, `720x576`, `720x576i`, `1600x1080`, `1440x1080`, `1280x1080`, `960x1080` | `1920x1080` | 26 | [schema 122](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L122) / [formatter 43](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L43) |
| `resolution scale` | `Video / Resolution Scale` | int: 25..800 | `100` | 111 | [schema 166](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L166) |
| `shader mode` | `Video / Shader Mode` | enum: `Legacy Recompiler (single-threaded)`, `Async Recompiler (multi-threaded)`, `Async Recompiler with Shader Interpreter`, `Shader Interpreter only` | `Async Recompiler with Shader Interpreter`, `Legacy Recompiler (single-threaded)` | 42 | [schema 127](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L127) / [formatter 541](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L541) |
| `shader quality` | `Video / Shader Precision` | enum: `Auto`, `Ultra`, `High`, `Low` | `High`, `Low`, `Ultra` | 79 | [schema 128](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L128) / [formatter 641](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L641) |
| `strict rendering mode` | `Video / Strict Rendering Mode` | boolean: `true`, `false` | `true` | 172 | [schema 143](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L143) |
| `lod bias offset` | `Video / Texture LOD Bias Addend` | float: -32..32 | `-2` | 2 | [schema 168](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L168) |
| `vblank ntsc fixup` | `Video / Vblank NTSC Fixup` | boolean: `true`, `false` | `true` | 6 | [schema 174](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L174) |
| `vblank rate` | `Video / Vblank Rate` | int: 1..6000 | `120`, `3000`, `60`, `6000` | 148 | [schema 173](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L173) |
| `vsync` | `Video / VSync Mode` | enum: `Disabled`, `Adaptive`, `Full` | `Full` | 5 | [schema 129](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L129) / [formatter 762](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config_types.cpp#L762) |
| `asynchronous texture streaming` | `Video / Vulkan.Asynchronous Texture Streaming` | boolean: `true`, `false` | `false`, `true` | 272 | [schema 192](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L192) |
| `write color buffers` | `Video / Write Color Buffers` | boolean: `true`, `false` | `true` | 1483 | [schema 131](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L131) |
| `write depth buffers` | `Video / Write Depth Buffer` | boolean: `true`, `false` | `true` | 133 | [schema 132](https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/rpcs3/Emu/system_config.h#L132) |

## Serialization, aliases and source evolution

RPCS3 [`cfg::_bool`](<https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/Utilities/Config.h#L204>) serializes lowercase `true`/`false`; [`cfg::_enum`](<https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/Utilities/Config.h#L293>) serializes through `fmt_class_string<T>`. RPCS3 YAML decoding passes scalar content to [`from_string`](<https://github.com/RPCS3/rpcs3/blob/54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6/Utilities/Config.cpp#L650>); therefore a quoted YAML string scalar is valid when its content is the canonical enum/numeric/boolean value. YAML spelling such as quoting numeric-looking enum `30` must not be confused with a schema type mismatch.

`R3Yaml.Build` normalizes only percentage suffixes; none of the 113 shipped setting/value pairs contains one. It preserves all reviewed canonical scalar content. Its existing dotted-key handling emits `Video -> Vulkan -> Asynchronous Texture Streaming`, never a literal flat `Vulkan.Asynchronous Texture Streaming` key. New regression coverage independently traverses the prepared YAML tree and checks exact scalar contents and nesting. It never writes to RPCS3.

Existing Wiki aliases are preserved: `xfloat accuracy` and `spu xfloat accuracy` both target `Core/SPU XFloat Accuracy`; `Always Enabled` for accurate PPU 128 reservations maps to integer `-1`; On/Off maps to lowercase booleans; anisotropic Auto maps to `0`; square resolution thresholds map to one bounded dimension. ZCULL stays a two-setting mapping. The regression reimports every shipped distinct source-alias/target/value combination through the existing Wiki parser.

Historical findings were checked against the current pinned source, not copied blindly:

- `PPU Accurate Non-Java Mode` exists at system_config.h:70. The existing `ppu llvm java mode handling` alias already targets it. It is not present in the current shipped changes; focused mapping coverage remains.
- `Allow Host GPU Labels` and `Emulate Special Depth Comparison` exist and their shipped booleans pass.
- Renderer/audio/microphone Disabled aliases already map to serialized `Null`; their focused regression checks remain. Windows XAudio2 and optional FAudio are build-dependent. No audio Renderer change is shipped in this catalog.
- RSX FIFO `PS3` is canonical; PPU `Interpreter (dynamic)` is absent from the pinned PPU formatter, while SPU `Interpreter (dynamic)` remains valid. The 81 shipped SPU decoder changes are valid.
- Bare `Anaglyph` is absent; named anaglyph modes exist. The parser rejects the bare form.
- VBlank 1..6000, Driver Wake-Up Delay 0..16667, Resolution Scale 25..800, SPURS 1..6 and PPU Threads 1..8 match their source declarations.
- Camera `SDL` needs HAVE_SDL3. Move `Gun` needs HAVE_LIBEVDEV and is excluded from the Windows acceptance fixture. Neither conditional value is shipped here.

## Defaults semantics and bounded spot-check

The reported full defaults audit was not repeated. The high-risk entries were rechecked directly: Windows Sleep Timers Accuracy is `Usleep Only` (the Linux/macOS branch is `As Host`); VSync is `Disabled`; Net/Internet enabled is enum `Disconnected`, not boolean; Camera type is `Unknown`; asynchronous texture streaming is the nested Vulkan boolean. All match current `PlanService`. The current table contains 76 entries, not the reported 84; this count difference is reporting evidence, not proof of a missing setting.

Renderer default `Vulkan` presumes HAVE_VULKAN; source fallback is OpenGL. This audit makes no claim about builds without Vulkan or arbitrary platform configurations. No shipped recommendation is reclassified by comparing it to a template's presence.

The existing effective-read path remains game custom config -> global config -> verified built-in default. Only selected actionable deviations are written to `config/custom_configs/config_<SERIAL>.yml`; the audit does not copy global configuration or create real custom configs. Existing `TemplateDefaultsDoNotBecomeRecommendations`, `SemanticRecommendationInvariants`, and `CfgBaselineT` cover template defaults, non-actionable categories, inherited values and no-change behavior.

## Differences and action log

| Serial / version | Source/category | R3Config value | Expected / reason | Action |
|---|---|---|---|---|
| All serials / unspecified | Catalog provenance metadata | September DataVersion/hash already on disk | Historical note: June was the previous intended baseline; SUPERSEDED - the September 2026 snapshot is now the production canonical source (owner decision), see docs/Arch/RelTest.md. Schema validity itself never proved parity | NO ACTION: preserved dirty catalog; reported separate evidence status |
| All serials / unspecified | Prior progress report | 84 built-in defaults reported | Current PlanService contains 76 entries; bounded source spot-check agrees for reviewed entries | NO ACTION: corrected report count, no speculative table additions |
| All audited settings / unspecified | Per-row aliases and classes above | 7,881 shipped canonical values | 57 known pinned schema keys, no enum/range defect found | NO ACTION |

No proven production mapping defect was found. Intentional audit edits are confined to `Tst/WikiCorpusT.cs` and this report.

## Verification

`WikiCorpusT.ShippedSchema` is invoked by the existing `WikiCorpusT.Fixture`, so both `--wiki-parser-only` and the full harness execute it. It validates every loaded row against the independently reviewed pinned source contract, validates deterministic composite pairs, tests every distinct scalar/nested YAML result, and reimports every distinct alias/value mapping. `MappingSchemaRegression` adds PS3 FIFO acceptance, Precise/Approximate ZCULL pairs, removed PPU dynamic and bare Anaglyph rejection, and ambiguous ZCULL/SPU range rejection.

Fresh Release build and focused test execution: pending coordinator integration run. This document must be updated from actual output, not inferred from source inspection. Canonical Wiki parity and official external corpus status are independently reported by the coordinator; neither is implied PASS here. No emulator execution, hardware, live configuration mutation, or upstream RPCS3 compilation was performed by this subtask.
