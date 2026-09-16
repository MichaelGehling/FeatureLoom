# LazyValue concurrency and service-container reuse

## Scope and contract
- Centralize reference visibility and atomic exchange in `LazyValue<T>`; retain its mutable, allocation-free struct wrapper and existing public members.
- Reads are acquire snapshots and assignments publish with release semantics. Snapshot reads do not reserve an object or coordinate a later operation.
- Competing constructors may execute, but without intervening mutation they return the same published instance. Constructor exceptions are not cached.
- `Obj` returns a non-null constructed or observed instance even when removal overlaps publication. Removal/replacement do not cancel constructors already running; callers can hold a detached value.
- Preserve `RemoveObj()`'s void signature. Add `ExchangeObj(T)` to atomically replace/remove and return the detached object without a separate read.
- Restore `LazyValue<AsyncLocal<LocalInstance>>` in the service container. Keep pending local-instance state, one-read access paths, retry, cycle handling and detached-work protection.
- This is correctness/encapsulation work, not a benchmark or a performance improvement claim. Existing benchmark and instruction-file edits are left untouched.

## Steps
1. **step-1 — Contract tests** (completed): Added 9 bounded contract tests. Unchanged helper: 8 passed, 1 failed; concurrent removal produced 18,877 null results from Obj.
2. **step-2 — Shared implementation** (completed): LazyValue now uses acquire snapshot reads, release assignment, atomic ExchangeObj, and direct CAS-result return. Added 2 exchange tests and restored the wrapper in ServiceInstanceContainer without changing pending local-state logic.
3. **step-3 — Validate** (completed): Full suite previously passed with 2,426 passed, 5 skipped, 0 failed (2,431 total). Reconfirmed all 36 focused tests and the workspace build after the discussion. Terminal diff review was skipped at the user's request after repeated command cancellation.

## Tracking
The progress service previously reported no active plan and provides no creation operation; this file is the primary progress record.

## Results
- Baseline build succeeded. `ObjNeverReturnsNullWhileAnotherThreadRemovesValues` failed: expected 0 null results, observed 18,877. The post-CompareExchange field reread permits removal to change the return value.
- Other baseline tests passed: snapshot-only reads, initialized reuse, replacement/null assignment, exception retry, competing constructors, removal/replacement during construction, independent struct copies.
- After implementation, all 36 focused tests passed: 11 new LazyValue concurrency/API tests, 8 existing lazy-variant tests, and all 17 service-registry regression scenarios.
- Final inspection confirmed the service container already uses `LazyValue<AsyncLocal<LocalInstance>>`, with `ObjIfExists` for holder snapshots and `ExchangeObj(null)` for atomic detachment. No additional service-code edits were needed.
- No unsafe accessor properties were added. Use the existing `LazyUnsafeValue<T>` for thread-confined use; the shared service holder retains the synchronized helper.
- `RemoveObj()` continues to use `ExchangeObj(null)`. A release write would also satisfy its non-canceling reset contract, but changing that implementation is not required for correctness or service integration.
- No performance improvement was measured or claimed. Optional documentation follow-up: a lazy-initialization skill note covering snapshot, reset, and struct-copy semantics.
