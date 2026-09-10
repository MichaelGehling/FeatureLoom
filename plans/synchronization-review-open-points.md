# Synchronization Review — Open Points

Context: investigation triggered by a report of **stuck threads** in a project using FeatureLoom.
It is not confirmed that FeatureLoom is the cause. This document collects findings that were
identified but **not yet resolved**, so they can be picked up later.

Reviewed and completed so far:
`TaskExtensions.cs` · `SingleThreadSynchronizationContext.cs` · `AsyncWaitHandle.cs` ·
`AsyncManualResetEvent.cs` · `WaitHandleExtensions.cs`
(see "Resolved" at the bottom).

Note on scope: blocking on a task whose continuation needs the blocked thread's own
synchronization context is deadlocking **by nature** and cannot be fixed inside the blocking
helper. That is documented at the affected APIs (`TaskExtensions.WaitFor`,
`AsyncWaitHandle.Wait`) and is not tracked here as an open point. Only actionable items remain below.

---

## OP-1 — Remaining unmitigated `WaitFor` call sites (LOW)

**Status:** mostly resolved · **Area:** `FeatureLoom.Deprecated`

All call sites in `FeatureLoom.Core` and `FeatureLoom.Web` were audited and mitigated (see "Resolved").
In every case the async method was invoked locally, so wrapping the **invocation** works:

```csharp
using (SynchronizationContext.Current.Suspend()) DoSomethingAsync().WaitFor();
```

(Verified: suspending around the invocation completes; suspending only around `WaitFor` still
deadlocks, because the context is captured when the `await` inside the async method is reached.)

### Still open
`FeatureLoom.Deprecated/Workflows/DefaultStepExecutionController.cs` — lines ~181, ~204, ~228, ~249, ~333.
Deliberately left untouched because the project is deprecated. Apply the same pattern if it is revived.

### Not fixable inside the library
`AsyncWaitHandle.Wait()` blocks on a task supplied by the *caller* (via `IAsyncWaitHandle`), so the
library never sees the invocation. Documented at the API and covered by a test that demonstrates the
deadlock; it is up to the user to avoid blocking waits on context-bound handles.

---

## OP-2 — Global `AwaitConfig.ContinueOnCapturedContextByDefault` arms every blocking path (BY DESIGN)

**Status:** closed (by design) · **Area:** `FeatureLoom.Core/Synchronization/AwaitConfig.cs`

A mutable global `static bool` (default `false`) controls whether **every** `ConfiguredAwait()` in the
library marshals continuations back to the captured context.

**Resolution:** This is intentional. `ConfiguredAwait()` replaces the `ConfigureAwait(false)` calls that
were previously spread through the library (the usual recommendation for library code). Since that
behaviour is not desirable in every scenario, the global switch provides a zero-cost opt-out.

Consequence to keep in mind, not a defect: setting it to `true` re-enables context capture, so
sync-over-async call sites must keep suspending the context around the *invocation* (as done in OP-1)
rather than relying on `ConfigureAwait(false)` semantics.

---

## OP-3 — Pre-.NET 8 code paths are never executed by the test suite (MEDIUM)

**Status:** open · **Area:** `FeatureLoom.Tests`

`FeatureLoom.Tests` targets `net10.0` only, while `FeatureLoom.Core` targets
`net48`, `netstandard2.0`, `netstandard2.1`, `net8.0`, `net10.0`. All `#else` branches (i.e. everything the
.NET Framework / netstandard consumers actually run) are compiled but **never executed** by any test.

The deadlock defects fixed in `TaskExtensions` existed *only* in those branches — the bug most likely
responsible for stuck threads was in code with zero test coverage.

### Next action
Multi-target the test project (at least add `net48`), or extract the target-specific logic so it can be
tested on modern TFMs.

### Related
If the reporting project targets .NET Framework or netstandard, it was running the vulnerable pre-fix code.
If it targets .NET 8+, it was not — worth confirming with them.

---

## OP-4 — Inconsistent exception-unwrapping defaults between blocking APIs (RESOLVED)

**Status:** resolved · **Area:** `TaskExtensions.WaitFor` vs `AwaitConfig.WaitConfigured`

`WaitFor(..., bool unwrapException = true)` hardcoded its default, while `AwaitConfig.WaitConfigured` honors
the global `AwaitConfig.UnwrapExceptionsByDefault`. Two blocking APIs in the same namespace with different
exception semantics is a trap.

### Fix
All three `WaitFor` overloads now take `bool? unwrapException = null` (source-compatible) and fall back to
`AwaitConfig.UnwrapExceptionsByDefault`, matching `WaitConfigured`. Explicitly passing `true`/`false` still
overrides the global default.
Covered by `TaskExtensionsTests.WaitForUsesGlobalUnwrapExceptionsDefault`.

---

## OP-5 — No bounded blocking variant of `WaitFor` (LOW)

**Status:** open · **Area:** `TaskExtensions`

`WaitFor` can only block indefinitely. Callers wanting a bounded wait have to write
`task.TryWaitAsync(timeout).WaitFor()`, which is awkward and still blocks indefinitely on the outer call.
A `WaitFor(TimeSpan)` returning `bool` would give a first-class bounded option — and a bounded wait turns a
hard deadlock into a recoverable timeout.

---

## OP-6 — Missing argument null checks (COSMETIC)

**Status:** open · **Area:** `TaskExtensions`, `AsyncWaitHandle` static wait helpers

A `null` task or handle array yields a `NullReferenceException` from an extension method instead of an
`ArgumentNullException`.

---

## Resolved during this review

### `TaskExtensions.cs`
- Pre-.NET 8 `TryWaitAsync(Task, CancellationToken)` ran the awaiting continuation **inline inside
  `CancellationTokenSource.Cancel()`** (`TaskCompletionSource` without `RunContinuationsAsynchronously`) —
  a genuine deadlock source. Fixed; same fix applied to `CancellationToken.AsTask()`.
- `CancellationTokenRegistration.Dispose()` was called from inside a synchronous continuation. Removed.
- The cancellation registration leaked indefinitely if the awaited task never completed. Fixed.
- `Timeout.InfiniteTimeSpan` was rejected by one overload and honored by another. Now consistent, and
  timeout validation matches `Task.Delay` / `Task.WaitAsync` (synchronous `ArgumentOutOfRangeException`).
- `TryWaitAsync(Task)` threw on faulted/cancelled tasks while the other overloads returned `false`. Unified.
- Over-broad `catch { }` narrowed to timeout / cancellation / the awaited task's own failure.
- Late task faults could surface as `UnobservedTaskException`. Fixed via `ObserveException()`.
- `Task.Delay` timers were kept alive for the full timeout even after completion. Fixed.
- `Suspend()` restored the context passed as argument instead of the one it actually removed.
- `default(SynchronizationContextRestorer)` cleared the thread's context on dispose instead of doing nothing.

### `SingleThreadSynchronizationContext.cs`
- **Hard deadlock:** callbacks were invoked while holding `workItemsLock`, so any callback that posted
  again blocked on the non-reentrant lock. Callbacks now execute outside the lock.
- `Dispose` now drains the queue and deterministically aborts queued `Send` callers instead of leaving
  them blocked.
- `Send` preserves the original exception stack trace via `ExceptionDispatchInfo`.

### `AsyncWaitHandle.cs`
- `TryConvertToWaitHandle` leaked an `EventWaitHandle` when losing an allocation race. Fixed.
- Faulted/cancelled tasks were reported as "signalled" by the `WaitAny`/`WaitAll` result scans.
  Replaced by `FindSignalledIndex`, which also observes faults.
- Timeout waits abandoned their `Task.Delay` timer, keeping it armed for the full duration after
  another handle had already won. Now cancelled via `GetWaitingTasksWithTimeout`.
- Blocking/timeout/cancellation semantics of the instance `Wait` overloads unified; overflow in
  `(int)timeout.TotalMilliseconds` removed.

### `AsyncManualResetEvent.cs`
- **Lost wakeup:** cancellation could pulse before the waiter reached `Monitor.Wait`, leaving the untimed
  `Wait(CancellationToken)` blocked forever. Cancellation is now checked inside the monitor lock.
- `Wait()` / `Wait(TimeSpan)` returned `true` when woken by *another* waiter's cancellation
  (`Monitor.PulseAll` wakes everyone). All sync waits now loop on the set counter.
- `Reset()` ran without `myLock`, so a concurrent `Set`/`Reset` could leave the `EventWaitHandle`
  inconsistent with `isSet`. Now locked (`PulseAll` inlines its reset branch, as the lock is not reentrant).
- `TryConvertToWaitHandle` leaked a handle on races and could miss a concurrent `Set`. Now created under lock.
- `DetachAndDisposeWaitHandle` could dispose a handle while `Set`/`Reset` was using it. Now detaches under lock.

### `WaitHandleExtensions.cs`
- **Removed from `FeatureLoom.Core`.** All four `WaitAllAsync`/`WaitAnyAsync` overloads had no callers,
  always returned `true` regardless of timeout/cancellation, and abandoned blocked thread-pool threads.
  `AsyncWaitHandle.WaitAll/WaitAny` covers the need correctly.
- The only used method, `WaitOneAsync(WaitHandle, CancellationToken)`, moved to
  `FeatureLoom.PerformanceTests` (its only consumer) and reimplemented on
  `ThreadPool.RegisterWaitForSingleObject`, so it no longer blocks a pooled thread per wait.
- This is a **public API removal** from `FeatureLoom.Core` — relevant for versioning if external
  consumers exist.

Test coverage added: `TaskExtensionsTests.cs`, `SingleThreadSynchronizationContextTests.cs`,
`AsyncWaitHandleTests.cs`, `AsyncManualResetEventTests.cs`, `ProcessingEndpointTests.cs`.
Full suite: 2392 passed / 0 failed.

### Sync-over-async call sites (OP-1)
`SynchronizationContext.Current.Suspend()` applied around the async **invocation** at:
`ProcessingEndpoint.Post` (both overloads, Async + AsyncChecked), `UndoRedoAction.Execute`,
`Aggregator`'s sync bridge delegate, `WebSocketEndpoint.Post` (both overloads),
`DefaultWebServer` constructor, `HttpEndpointConfig.HostAddress` setter.
Verified by `PostingAnAsyncActionFromASingleThreadContextDoesNotDeadlock`, which was confirmed to
fail (10 s deadlock) when the mitigation is removed.

---

## Not yet reviewed

- `FeatureLock.cs`
- `MicroLock.cs`
- `LockOrderDeadlockResolver.cs`
- `AwaitConfig.cs` (beyond OP-2 / OP-4)
