# NCL address binding

`NclStaticLowerer` produces ordered concrete guest-memory byte effects. Those byte effects are not RPCS3 patch instructions yet: a source address must first be bound to one verified RPCS3 run key and one verified address domain.

## Boundary

`NclAddressBinder` is a pure Core-layer transformation. It does not inspect RPCS3 processes, read or write RPCS3 files, emit YAML, alter `art.yml`, or infer module placement from a serial/version/hash alone.

The binder consumes:

- a successful `NclStaticLowerResult`;
- one or more explicitly verified `NclAddressDomain` values;
- each domain's RPCS3 `RunKey`, guest start, byte length, and RPCS3 patch-address start.

For a write wholly contained by exactly one domain, the mapping is:

`PatchAddress = PatchAddressStart + (NclAddress - GuestStart)`

The write ordering and exact byte payload are preserved. No write is split, merged, repaired, or guessed.

## Evidence rule

`Rpcs3GameDescriptor` and `HashRes` provide executable identity/hash evidence. They do not provide guest address ranges or module/segment placement. Hash presence alone therefore cannot authorize address binding. Address-domain evidence must come from a later verified binary/runtime mapping stage.

## Fail-closed cases

Binding rejects:

- a failed static-lowering result;
- no address domains;
- invalid RPCS3 run keys;
- zero-length or overflowing domains;
- empty or overflowing concrete writes;
- writes outside every verified domain;
- writes crossing a domain boundary;
- writes matching more than one domain;
- mappings that cannot fit RPCS3's UInt32 patch-address field.

This phase deliberately stops before RPCS3 patch YAML emission.
