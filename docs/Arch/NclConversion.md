# Legacy Artemis NCL conversion boundary

R3Config treats legacy NetCheat/Artemis NCL as an untrusted source format. Syntax similarity is never sufficient to create an RPCS3 patch.

## Proven source semantics

The semantic model follows the NetCheat 4.53 executor and the newer `Code types.txt` shipped in `ncUpdateDir`:

- `0[TYPE]`: arbitrary-length byte write. Modifier `1` reads then ORs, `2` reads then ANDs, `3` reads then XORs; every other modifier follows the executor default and performs replacement.
- `1`: UTF-8 text write with a trailing NUL byte.
- `2`: invariant Float32 converted to four big-endian bytes.
- `4` + `4[VINC]`: repeated write with address increment, count, and value increment.
- `6[MODE]`: reads a 32-bit pointer, adds the supplied offset, then rewrites the next operation's address when MODE is zero or its value when MODE is non-zero.
- `A1` / `A2`: runtime copy buffer and paste buffer.
- `B[COUNT]` + `B`: runtime find/replace over the supplied address range; COUNT zero means no result limit.
- `D[COUNT]`: runtime equality conditional controlling the following COUNT operations.
- `E[COUNT]`: runtime bit-mask conditional controlling the following COUNT operations.
- `F[COUNT]`: runtime memory copy from source address to destination address.

A block whose NCL state is enabled is a constant-write block. It requires runtime execution even if every contained opcode is otherwise a deterministic replacement write.

## Conversion states

`OfflineStaticCandidate` means only that the NCL semantics are deterministic without reading runtime memory. It is not permission to emit `patch.yml`.

Before conversion, later layers must additionally prove:

1. exact game serial;
2. exact application version;
3. authoritative RPCS3 module/PPU identity;
4. address-to-module mapping;
5. a lossless RPCS3 operation equivalent;
6. no constant-write requirement;
7. no unresolved option placeholder or unsupported operation.

Runtime-dependent operations require evidence captured outside the main UI process. Missing or ambiguous evidence fails closed.

## Runtime isolation invariant

The future runtime evidence provider must be out-of-process and read-only by default. R3Config must not request `PROCESS_ALL_ACCESS`, `PROCESS_VM_WRITE`, `PROCESS_VM_OPERATION`, remote-thread creation, process suspension, or `SeDebugPrivilege` for conversion evidence capture.

RPCS3 patch/config mutation remains behind the existing mutation transaction boundary and must not occur while RPCS3 owns the target files.
