# Shared local-service scopes across async boundaries

## Contract
- CreateLocalInstancesForAllServices synchronously installs a shared scope object in AsyncLocal before any factories run. Late service holders belong to this scope, not to the child context performing first access.
- Descendants share identity and initialized state; independent activations have distinct scopes. Concurrent first resolution constructs one published instance per scope/key.
- Registry and Service<T> ClearAllLocalServiceInstances gain an optional allContexts=false parameter. Existing one-argument overloads remain for binary compatibility and forward to current-context clearing.
- Default registry clearing closes the caller's shared scope (including its descendants), not independent scopes. With no active scope, lookups fall back to global instances or explicit context-local overrides. There is no implicit parent-scope stack restoration.
- allContexts=true explicitly clears every tracked scope and per-service local holder. Promotion uses only the calling context's completed values, without forcing construction.
- Keep explicit per-service CreateLocalServiceInstance overrides context-local, keep LazyValue for holder publication, and keep factories outside registry/scope metadata locks.
- No terminal diff commands, performance claims, new external dependencies, or unrelated logger changes.

## Steps
1. **step-1 — Regression tests** (completed): Added 11 scenarios to the 17 existing isolated tests. Baseline: 18 passed, 10 failed, reproducing late async identity loss and cross-scope clearing.
2. **step-2 — Scope implementation** (completed): Added shared scope state and explicit all-context clearing, adapted the container and service entry points, rechecked concurrent named-cache insertion, and updated global-clear regression calls to opt in. Global alias creation now resolves the source's global instance rather than capturing its scoped value.
3. **step-3 — Validate and document** (completed): Focused regressions and full suite passed; workspace build succeeded. Public XML documentation describes current-scope and all-context clearing.

## Tracking
The progress service previously reported no active plan and has no exposed plan-creation operation. This file is the primary progress record.

## Results
- Baseline: 28 scenarios run, 18 passed, 10 failed. Concurrent named resolution also exposed ArgumentException for duplicate cache key `parallel` in Service<T>.HandleUninitializedGet. Fix the cache race as part of the concurrent-first-resolution requirement.
- Additional alias-clear regression reproduced scoped-source publication into the global alias slot (34 passed, 1 failed). After separating global source resolution: 35 passed, 0 failed, 0 skipped.
- Full FeatureLoom.Tests run: 2,449 total, 2,444 passed, 5 skipped, 0 failed. Workspace build successful.

## Compatibility and limits
- Existing one-argument calls now clear only the caller's shared scope (including descendants sharing it). Use `allContexts: true` for the previous all-context behavior.
- Service<T> clearing affects that service's named and unnamed entries without closing the registry scope or clearing other service types.
- Explicit per-service overrides remain execution-context-local. Separate registry activations remain independent; clearing a child activation does not restore a parent-scope stack.
- Already-running factories may return to their original caller after clearing, but cannot restore cleared scope entries. Promotion does not force pending or failed factories to run.
- No performance measurements were made. A dependency-inversion skill file is recommended to preserve these scope and locking contracts.
