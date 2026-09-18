# ReceiverBuffer lost wake-up fix

## Contract and scope
- Use the external encoder-wakeup-repro/Program.cs as evidence and a regression basis; do not modify the external project.
- ReceiverBuffer has a single consumer. Producers may post concurrently through the buffer or underlying receiver.
- Never rely on transition notifications copied into a second event for waiting. With no local batch, delegate waits to the underlying receiver's level-triggered handle.
- A cached buffer wait handle must follow current buffer state; local buffered items must make all wait APIs immediately ready.
- Notifier remains an advisory availability source, not an ordered synchronization protocol. Preserve notifications for local-buffer availability.
- Avoid moving callbacks under queue locks, production test instrumentation, polling workarounds, and unrelated event/counter changes.

## Steps
1. **step-1 — Regression coverage (completed):** Adapted controlled delayed-Set replay for all three posting overloads; covered wait APIs, local batches, cancellation, and cached handles. Baseline: 8 passed, 5 failed.
2. **step-2 — Availability fix (completed):** Added a stable buffer-aware wait adapter, retained advisory notifications, and documented unsupported native conversion. All 18 focused tests pass, including out-of-order notifications and concurrent progress.
3. **step-3 — Validation (completed):** Focused tests: 18 passed, 0 failed. Full FeatureLoom.Tests suite: 2,460 total, 2,455 passed, 5 skipped, 0 failed (.NET 10). Workspace build successful.

## Findings
- QueueReceiver posts Set outside queueLock. A trailing Set after draining can leave its event set while empty.
- ReceiverBuffer resets its own event independently. Later queue Set calls can be no-ops, permanently losing buffer wake-ups.
- AsyncManualResetEvent sends callbacks outside its lock; boolean notifications can arrive out of order and must not control waits.
- Baseline also exposed blocking with a local batch: the queue resets its event during ReceiveMany before the batch is assigned in ReceiverBuffer.
- User approved returning false/null from native WaitHandle conversion. Preserve all interface wait methods.
- Progress service has no active plan and exposes no creation operation; this file records progress.
- Validation exposed a partial-batch copy defect: the IList CopyFrom overload ignores destination.Offset. ReceiverBuffer's two affected receive/peek calls now use the explicit array-offset overload; permanent tests cover nonzero destination offsets. Shared helper code remains unchanged.

## Limits
- This fix preserves the existing single-consumer contract; it does not make ReceiverBuffer multi-consumer safe.
- Underlying handles may produce spurious wakes, so callers still recheck availability. No polling fallback was added.
- Native conversion returns false/null as approved. Cached IAsyncWaitHandle adapters remain valid across batch changes.
- QueueReceiver and AsyncManualResetEvent were not changed; no claim is made that all independent races in those types are resolved.
- Recommend a focused message-flow skill file documenting level-based waiting, advisory notifications, and single-consumer invariants.
- External reproducer and plugin sources were not modified. No plugin deployment or long-duration production validation was performed.
