# NCL static lowering

`NclStaticLowerer` is the deterministic byte-effect boundary between parsed NCL semantics and future RPCS3 patch emission.

This phase does not write RPCS3 files, access RPCS3 process memory, emit `patch.yml`, or modify the Artemis catalog.

Accepted static semantics are:

- direct replacement writes,
- text writes using the parser-preserved encoded bytes,
- Float32 writes using the parser-preserved big-endian bytes,
- condensed writes expanded in source order when the value increment is zero.

Read-modify-write direct operations, pointers, copy buffers, find/replace, conditionals, runtime copy operations, constant-write blocks, unresolved options, and unsupported opcodes are rejected by the static lowerer.

Condensed writes with a non-zero value increment remain fail-closed until dialect-specific NetCheat and Artemis increment behavior is proven independently.

Expansion is bounded to 65,536 concrete write operations and 16 MiB of expanded payload bytes. UInt64 address arithmetic is checked for overflow.

The lowerer preserves source ordering and exact byte payloads. It intentionally does not select RPCS3 patch instruction types or executable-module addresses. A later evidence and emission stage must prove the target executable/module hash, map NCL addresses to RPCS3 patch offsets, enforce the RPCS3 UInt32 offset contract, and verify emitted byte equivalence.