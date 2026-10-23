# NCL PPU address-domain evidence

The static NCL pipeline may emit an RPCS3 Patch 1.2 entry only after the numeric NCL address has been bound to an explicit RPCS3 run-key address domain.

For the main PPU executable, RPCS3 applies patch addresses against each file-backed ELF PT_LOAD program segment using the segment `p_vaddr` as the minimum patch address and `p_filesz` as the available patch range. The NCL PPU evidence collector therefore derives identity mappings only for non-empty `p_type=0x1` ranges that occur immediately before the requested `PPU executable hash` inside the newest matching `SYS: Serial` session:

`GuestStart = p_vaddr`
`Length = p_filesz`
`PatchAddressStart = p_vaddr`

`p_memsz` is deliberately not used to extend the patchable range.

The collector fails closed when the serial session is absent, the requested exact PPU hash is absent from the newest matching session, the run key is not a 40-hex PPU key, a serial-scoped run key disagrees with the selected serial, relevant segment evidence is malformed, a segment cannot fit the RPCS3 UInt32 patch-address domain, or PT_LOAD ranges overlap.

This component reads evidence only. It performs no RPCS3 process access, no memory writes, no patch-file writes, and no Artemis catalog mutation.

## Foreign module boundaries

RPCS3 logs PRX, overlay and relocatable modules with the same `ppu_loader: ** Segment:` format string as the main executable (`PPUModule.cpp:1614`, `:2193`, `:2909`), so pending segment evidence is discarded whenever a foreign module boundary is observed before the requested hash:

- `**** Loaded to 0x` ends a `ppu_load_prx` segment run (`PPUModule.cpp:1672`).
- `SPU executable hash:` marks an embedded SPU image (`PPUModule.cpp:1483`, `:1487`).
- `lv2_overlay::load():` and `Loaded overlay:` mark the `sys_overlay` path (`sys_overlay.cpp:88`, `:72`).
- `Trying to load: `, `Trying to load as executable: ` and `Trying to load binary: ` open a virtual-load attempt on the precompilation path (`PPUThread.cpp:4388`, `:4605`, `System.cpp:1966`). These are emitted on the `PPU` channel (`PPUThread.h:12`) rather than on `ppu_loader`, and RPCS3 supports per-channel log levels (`util/logs.hpp:178`, `:184`), so they are an additional boundary and never a sufficient one.
- A `** Segment:` line that follows a `** Section:` line always opens a different module load, because every loader entry point emits its complete segment run before its section run (`PPUModule.cpp:1614`/`:1697`, `:2193`/`:2292`, `:2909`/`:2985`). This is the only boundary a direct virtual-load overlay (`PPUThread.cpp:4516`) cannot bypass, since `ppu_load_overlay` logs no completion marker of its own and never reaches `sys_overlay.cpp`.

## Residual limitation

One source-reachable shape remains indistinguishable from a single module in the log text: a virtual-load overlay whose ELF carries no section headers (`e_shnum = 0` is accepted at `Loader/ELF.h:316`, `:336`), logged while the `PPU` channel is raised above notice, immediately followed by the main executable's own segment run and hash. Both loaders use the identical format string and emit nothing between function entry and their first segment line, and a main executable may legitimately carry any number of PT_LOAD headers, so two concatenated runs and one longer run are textually identical. Separating them would require p_vaddr ordering or segment-count guessing, which this component does not use, or decrypted ELF or live-memory input, which the NCL contract excludes. The case is therefore documented rather than guessed at; every other observable foreign-module shape is discarded.