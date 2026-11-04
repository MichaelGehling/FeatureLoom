# Storage Revamp – Overview

## Goals
1. New **Log Storage**: append-only data plus an index built alongside it for fast lookup by timestamp, flags and custom fields.
2. New **Object Storage**: replaces `IStorageReader`/`IStorageWriter` (`FeatureLoom.Core/Storages`).
3. Old storage stays until the new one is stable, then moves to `FeatureLoom.Deprecated`.

## Positioning
An embedded, zero-install log store for applications where a server-based solution (Kafka, time-series DB, log server) would be overkill: fast, file-based, self-maintaining, copy-to-backup. Covers the common cases without the heavy environment; it does not aim to replace these products (see non-goals in [log-storage.md](log-storage.md)).

## Robustness requirements
- **File format versioning from day one**: every segment/index/lookup table file carries a format version; readers support all older versions, writers write the current one. Format changes are documented.
- **Crash/recovery testing**: simulated crashes at arbitrary write points (torn writes, missing index tail, partial lookup table), plus randomized (fuzz-style) operation sequences verifying that every flushed entry is readable and nothing corrupt is returned.

## Target priority
- **Main focus**: modern .NET (net8, net10, netstandard2.1). Performance work, design trade-offs and benchmarks are optimized for these.
- **net48 / netstandard2.0**: must stay functionally compatible (fallback code paths via `#if NETSTANDARD2_0`, e.g. software CRC32), but no design compromises or performance degradation for modern targets on their behalf.

## Decisions
| # | Decision | Rationale |
|---|---|---|
| D1 | Order: Log Storage first, then Object Storage | Purely additive and low risk; nothing existing has to migrate yet. |
| D2 | Object Storage does not depend on Log Storage | Keeps the two designs separate and simpler. |
| D3 | Both live in FeatureLoom.Core, in new namespaces (`FeatureLoom.Storage.Log`, `FeatureLoom.Storage.Objects`) | No external dependencies needed. Package policy: Core (planned rename to `FeatureLoom`) stays dependency-free and hosts everything that needs none; optional `FeatureLoom.XXX` packages exist mainly for parts with additional dependencies. |
| D4 | Shared low-level primitives go in `FeatureLoom.Storage.Primitives` (file segment IO, atomic write, flush policy, serializer adapter) | Avoids designing these twice; both storages use them. |
| D5 | Log entry encoding uses a replaceable codec (`ILogEntryCodec<T>`): object (JsonSerializer), text, raw bytes | The user wants to log objects, text or bytes through a swappable writer/reader. |
| D6 | First-version index keys: write timestamp, flag bits, custom fields (`Raw`, `Hash`, `Handle`, `Ascending`/`Descending`) defined per log schema | Replaces dynamic tags and user keys; see [log-storage.md](log-storage.md). |
| D7 | Durability: batched flush with a configurable policy (max entries / max delay / explicit FlushAsync) | Balances throughput and safety. |
| D8 | Use `.ConfiguredAwait()`; no new external dependencies; `#if NETSTANDARD2_0` only where needed | Follows the repo rules. |
| D9 | Segment lifecycle policy: rotation (max age, soft max size), optional compression (after age or N newer segments), retention/deletion (max age, max total size, max segment count) | User requirement; all limits configurable and optional. |
| D10 | Query API offers both: a batch API (`Task<LogBatch<T>> ReadBatchAsync(query, continuation)`) on all targets, plus `IAsyncEnumerable<LogEntry<T>>` where available (netstandard2.1, net8+), implemented on top of the batch API and conditionally compiled | One core implementation; no extra package for net48/netstandard2.0. |
| D11 | Performance and allocations are primary design goals: zero-allocation steady-state hot paths, pooled buffers, batched IO, benchmarks with `MemoryDiagnoser` as acceptance criterion | Storage is infrastructure on hot paths; waste multiplies. |

## Phases
1. [Log Storage](log-storage.md)
2. Object Storage (sub-plan to be written after Phase 1 is stable)
3. Migrate users (`Session`, `Identity`, `IdentityGroup`, `Configuration`, `StorageWebAccess`, `CertificateStorageReader`, `EnvironmentVariableStorage`)
4. Move the old `Storages` code to `FeatureLoom.Deprecated`

## Open questions
- Should an index survive a crash, or be rebuilt from segments on startup? (Proposal: rebuild if missing or corrupt.)
