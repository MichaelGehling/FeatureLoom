# Service registry deadlock correction

Scope: preserve global activation/clear semantics and the public API; no independent execution-context scope API. Registry locks protect metadata, never service factories. Keep existing manual local-instance behavior.

## Steps
1. **step-1 — Reproduce** (completed): Added 13 permanent child-process regression scenarios. Baseline: 1 passed, 12 failed; includes a timeout in the actual Storage/TestHelper cold-clock path.
2. **step-2 — Fix** (completed): Registry factories moved outside metadata locks. All activation slots are prepared first; new registrations honor local mode; compatible-container initialization is deferred; stale local work is detached by clearing. Expanded regressions: 17/17 passed.
3. **step-3 — Validate** (completed): All 17 regressions passed. Full suite: 2,415 passed, 5 skipped, 0 failed (2,420 total). Workspace build succeeded. Final diff check passed; only intended source, test, and plan files changed.
4. **step-4 — Follow-up review** (completed): Reviewed registry factory boundaries and lifecycle handling. Gave each child-process test a private temporary working directory. Reproduced archive contention against the unchanged baseline binary with local mode disabled; logging-library changes remain out of scope.
5. **step-5 — Confirm validation** (completed): Reran the current full suite after filesystem isolation: 2,415 passed, 5 skipped, 0 failed (2,420 total), including all 17 regressions. Workspace build and diff check passed; zero temporary scenario directories remained.

## Decisions
- User selected existing global activation/clear behavior, not independent scopes.
- Use a test-only executable entry point in the existing test assembly for child-process scenarios; no production test hooks or external dependencies.
- Propose a DI skill file describing factory/registry lock boundaries; defer that optional documentation addition to user approval.

## Results
- Regression baseline: 13 run, 1 passed, 12 failed. Tests built successfully before production edits.
- Expanded regressions after fix: 17 run, 17 passed, 0 failed. Additional manual-null-fallback test caught a compatibility regression; fixed and rerun successfully.
- Progress tool has no active plan and no plan creation tool is exposed; this file is the progress record.
- Initial full test run emitted file-logger contention errors for `log.txt` and `logArchive.zip`, despite passing all non-skipped tests. The follow-up baseline probe below established that archive contention predates the fix. Logging changes are outside this fix; passing tests do not imply a diagnostic-free run.

## Preserved behavior and limits
- Activation/clearing remain global operations; local values still flow with `ExecutionContext`. No independent scope API was introduced.
- Factories are outside the registry lock, including compatible-container resolution. Instance creation still has per-instance synchronization; arbitrary cross-thread dependency cycles are not solved.
- Activation prepares all current slots before eager construction. Exceptions leave completed instances intact and pending slots retryable through normal service access.
- Clearing detaches pending initialization. A factory already running may finish, but its result cannot restore the detached local state. Promotion uses only a completed current-context local value.
- `Set` of a supplied instance no longer invokes an unused factory first. Named local factories now receive the registered name. Manual overrides retain global fallback outside global local-mode, including null resets.
- Optional follow-up: add `.github/skills/dependency-inversion.md` documenting these locking and lifecycle invariants.

## Follow-up evidence
- Baseline revision: `f328c904de49eb3aeddaecd6454ccd2c2e3a2f54`. An isolated unchanged checkout under `.vs/service-registry-validation/baseline/source` passed 2,398 tests with 5 skipped (2,403 total) in a fresh directory.
- A second unchanged-baseline run with pre-existing log files was aborted by the 45-second inactivity watchdog after 172 passes. That run did not capture logger errors; its cause was not established.
- A separate bounded direct-logger probe used a SHA-256-matched copy of that baseline core DLL. With 32 loggers sharing an existing log file and global local-mode disabled, it observed 11 logger errors, including the same `logArchive.zip` sharing violation at `FileLogger.cs:183`. This establishes that the archive contention predates the fix, not that its frequency is unchanged.
- Probe and baseline logs remain in ignored `.vs/service-registry-validation/`; no baseline sources replaced working-tree files.
- Review also noted that compatible alias containers survive source-type reset. The old code already copied and registered these containers independently; alias-rebinding semantics were not changed in this fix.
- Final working-tree validation again reported archive contention while passing every non-skipped test. No logging behavior was changed or claimed fixed. The current build succeeded and all 17 isolated regression scenarios passed in the full suite.
