# Restore accidentally reverted correctness fixes

LazyValue.cs has been restored. Its 11 concurrency tests pass and the workspace builds. The first full run had 2,412 passed, 14 failed, and 5 skipped; inspection confirmed that the service registry/container fixes and internal preparation interface were also reverted. The user approved restoring those changes too.

## Steps
1. **step-1 — Restore services** (completed): Restored the internal preparation contract, factory-free registry critical sections, and pending local-instance state backed by LazyValue. No new behavior or unsafe accessors.
2. **step-2 — Validate** (completed): Full suite passed after restoring services: 2,426 passed, 5 skipped, 0 failed (2,431 total). Workspace build succeeded. No terminal diff commands were run.

## Results
- Restored LazyValue.cs, IServiceInstanceContainer.cs, ServiceRegistry.cs, and Service.ServiceInstanceContainer.cs to the previously validated design.
- All 14 failures from the partially reverted state are resolved. Full suite and workspace build passed after restoration.
