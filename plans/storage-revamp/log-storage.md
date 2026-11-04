# Phase 1 – Log Storage

## Concept
- **Segments**: append-only files (`<name>.<seq>.<yyyyMMddHHmmss>.log`, see Archiving) with framed records: header (timestamp, flags, custom field source values, payload length, checksum) + payload.
- **Record framing** (per log, in schema; nice-to-have, low priority): pluggable `ISegmentRecordFraming`.
  - `Binary`: compact length-prefixed header.
  - `Text` (human/tool friendly): one line per record: `<ISO timestamp> <len> <crc32 hex> <field=value ...>\t<payload>\n`. With compact JSON payloads (no raw newlines), segments are readable in any editor and usable with `grep`, `cut -f2 | jq`, etc. Readers still use index offsets + length, so payload newlines don't break parsing; tool-friendliness just requires single-line payloads.
  - Design framing as a separate concern from the start, so `Text` can be added later without format changes elsewhere. Compressed segments remain readable via `gzip -dc`.
  - **Header mode** (per log, in schema): `Full` (default; time, flags, field values, length, checksum), `Minimal` (length + checksum only), `None` (payload only; with `Text` framing + single-line JSON the segment is plain NDJSON).
    - What the header provides and the fallback without it: index rebuild (→ only via field extractors `Func<T, value>` on the payload, write time lost); `Hash` match verification and overflow-handle verification (→ extractor, otherwise not allowed); corruption detection (→ lost with `None`); tail recovery after crash (→ `Minimal` keeps it; `None` only with newline-delimited text framing).
    - Schema validation rejects combinations that can't work (e.g. `Hash` field + `None` without extractor).
- **Codec**: `ILogEntryCodec<T>` turns T into a payload and back. Built-in codecs: `JsonObjectCodec<T>`, `TextCodec`, `BytesCodec`.
- **Index**: one sidecar file per segment (`.idx`), one **fixed-size record per entry**, written in batches together with the segment.
  - **Index schema** (`LogIndexSchema`, defined at log creation, stored in each `.idx` header with version + checksum; may only change at rollover, readers handle per-segment schema). Record size and field offsets are computed once from the schema; no padding by default.
    - Time: 4-byte offset to a reference time in the header, configurable resolution, **default 100 µs** (range 2^32 × 100 µs ≈ 4.97 days; 1 ms ≈ 49 days, 1 s ≈ 136 years). `MaxSegmentAge` is validated against the range (default cap ≈ 4.5 days, i.e. range minus safety margin); the range itself remains a hard rotation trigger as a safety net.
    - Position: 4-byte start offset → entry start must be < 4 GiB. Config validation keeps the soft limit safe: `MaxSegmentSize + MaxEntrySize ≤ 4 GiB` (defaults e.g. 3.5 GiB soft max, 256 MiB max entry). Hard check before each append as safety net: rotate first if the start would overflow. Optional 8-byte mode for huge entries/segments. Length is not stored: derived from next record's offset (last record: segment end).
    - Flags: bit field, 1 bit per predefined flag; bit 0 reserved = deleted. Width 1/2/4/8 bytes chosen from flag count.
    - Predefined tags: as bits (filter by mask).
    - **Custom fields**: N fields defined in the schema, each 1–8 bytes, with a kind:
      - `Raw`: caller-supplied value (id, enum, counter). Exact; supports equality, set and range filters.
      - `Hash`: hash of a caller value. Matches verified against the full value in the segment record header (false positives only cost a read).
      - `Handle`: value interned via a built-in **lookup table** (value ↔ handle). Exact, no verification needed.
    - **Lookup tables** (built-in, **strictly per log**, stored in the log's folder): append-only file of `handle → value`; in-memory dictionary for value → handle on write. Queries can filter the table first (e.g. path prefix/pattern) to get a handle set, then scan the index with set membership. Handle size limits table capacity (1 B = 256, 2 B = 65 536, ...). When full, new values get a reserved **overflow handle**; matches on it are verified against the full value in the segment record header. Table entry must be flushed before any index record referencing it (crash consistency). Handles are never reused; no table compaction in v1.
  - Entry N lives at `HeaderSize + N * RecordSize` → O(1) access by sequence number, in-place flag updates.
  - Index timestamp = write time, forced monotonic per log (`max(last, now)`) → binary search works directly on the index file. Caller-supplied timestamps (event time) are stored as `Raw` custom fields (e.g. 8 bytes) and filtered by range scan.
  - Filters = sequential scan of fixed records within the timestamp range (cache friendly). Secondary inverted lists only if benchmarks demand it.
  - Rebuilt from the segment if missing or corrupt (requires the segment record header to carry timestamp, flags, custom field source values).
- **Deletion**: only by setting the deleted flag in the index record (in place; works for compressed segments too since the index stays uncompressed). Readers skip flagged entries.
- **Compaction** (optional, policy-driven): rewrite a closed segment without deleted entries and rebuild its index (offsets change). Triggered after rollover and/or before compression; swap via temp file + atomic rename, deferred while readers hold the segment.
- **Flush policy**: `LogFlushPolicy { MaxEntries, MaxDelay }` plus `FlushAsync()`.
- **Query**: time range + flag/custom field filters → lazy batched reads (D10): batch API with continuation token on all targets; `IAsyncEnumerable` wrapper where available (conditional compilation).
- **Lifecycle** (`LogSegmentPolicy`, all limits optional):
  - Rotation: start new segment when `MaxSegmentAge` or soft `MaxSegmentSize` is exceeded (soft = checked after a record is appended, records never split).
  - Compression: compress closed segment when older than `CompressAfterAge` or when `CompressAfterSegmentCount` newer segments exist. GZip (`System.IO.Compression`, available on all targets). Index files stay uncompressed so lookups still work without decompression; payload reads decompress the segment (streamed).
  - Retention: delete oldest segments (+ index) when `DeleteAfterAge`, `MaxTotalSize` or `MaxSegmentCount` is exceeded. Active segment is never deleted.
  - Lifecycle runs in background after rotation and on a timer; never blocks appends; readers holding a segment defer its deletion/compression.
- **Multi-log handling**:
  - Each log is fully self-contained in its own folder (segments, indexes, lookup tables, schema). Deleting a folder removes exactly one log; no cross-log files or dependencies.
  - Cross-log queries via a runtime `MultiLogReader`: runs the query per log (each resolving its own lookup tables/handles), then merges results by index timestamp (k-way merge) or concatenates, depending on the query. Continuation token holds a position per log.
  - Logs in one merged query may have different schemas; filters referring to a field missing in a log exclude (or optionally ignore) that log.

## Archiving & backup
Goal: backup/archive = plain file copy/move of segment + index files (+ lookup table + schema).
- **Self-describing files**: each `.idx` header carries schema, reference time, segment sequence and log id; segment file names include sequence and start time (`<name>.<seq>.<yyyyMMddHHmmss>.log`) → no name collisions when archives from different times are combined.
- **Closed segments are immutable** except for: delete flags (in-place in `.idx`), compaction and compression (both replace files via temp file + atomic rename). A copy of a closed segment pair is therefore always consistent; at worst it misses later delete flags.
- **Lookup tables** are append-only and flushed before referencing index records → copying the table file at any time after the segments yields a consistent superset. Archive copies the table along with the segments.
- **Active segment**: copying while writing may produce a torn tail; the regular truncated-tail recovery handles it on open. For exact snapshots use the API below.
- **API**:
  - `CreateSnapshotAsync(targetDir)`: flush, briefly pause lifecycle jobs (compaction/compression/retention), copy closed segment pairs + table + schema (hard links where supported), resume.
  - Retention action configurable: `Delete` (default) or `MoveTo(archiveDir)` → archiving instead of deletion via plain file move.
  - `LogReader.OpenReadOnly(dir)`: open an archive folder as a read-only log; combinable with live logs through `MultiLogReader`.
  - Re-attach: moving archived files back into a log folder is picked up on next open/rescan (schema per segment, so older layouts stay readable).

## Performance & allocations
Primary design goal: high throughput, minimal allocations on append and query hot paths (see `optimization` skill).
- Steady-state append: zero allocations per entry beyond what the codec inherently needs (reused write buffers, no per-entry `Task`/closure/LINQ).
- Index records are encoded/decoded via precomputed field offsets on spans (`BinaryPrimitives`); `#if NETSTANDARD2_0` fallbacks only where needed, without degrading newer targets.
- Batched IO: entries and index records are written in batches per flush; index reads in large blocks, scanned in place.
- Query filters compiled once per query into a fixed-record matcher (no per-record virtual dispatch/boxing where avoidable); handle sets as compact structures (bit set / sorted array).
- Lookup table: value → handle lookup without allocation for existing values (`ByteSegment` keys with cached hash; strings decoded via `Utf8StringCache`).
- Codecs write into a reused buffer: `JsonSerializer.Serialize(Stream, T)` into a writer-owned reusable `MemoryStream`; deserialize from `ByteSegment`.
- Async APIs avoid allocation where possible (`ValueTask` on targets that support it; sync fast path when data is buffered).
- Acceptance: BenchmarkDotNet benchmarks with `MemoryDiagnoser` for append, query and multi-log merge; allocations per op tracked and kept stable.

### Buffer / pooling tool choice (FeatureLoom first)
If a needed helper is missing (e.g. pooled `MemoryStream`, bit set, span-based binary field reader/writer, large-buffer pool), build it as a **general reusable helper** in the fitting FeatureLoom namespace (`FeatureLoom.Helpers`, `FeatureLoom.Collections`, `FeatureLoom.Extensions`), with its own XML docs and tests; not as storage-internal code.
| Need | Tool | Why |
|---|---|---|
| Writer batch buffer (entries + index records until flush) | Writer-owned reusable `byte[]` (grow on demand) | Single long-lived owner; no pool needed. |
| Small transient slices (record header encoding, filter scratch) | `SlicedBuffer<byte>` (`Shared` per thread) | LIFO bump allocation, no per-slice alloc; slices must not be held long (pin whole buffer). |
| Reusable objects (query contexts, `LogBatch<T>`, matchers, `MemoryStream`s) | `Pool<T>` per log/reader; `SharedPool<T>` for cross-thread reuse | Existing, low contention; avoid `ArrayPool`-style ad hoc reuse. |
| Rarely used members (overflow verification sets, optional schema parts, per-handle caches) | `LazyValue<T>`, `LazyList<T>`, `LazyDictionary<K,V>` | No allocation when feature is unused. |
| Payload / value views | `ByteSegment` (`TextSegment` for text codec) | Non-allocating views with optional cached hash. |
| Large transient read buffers (index block reads, decompression) | `ArrayPool<byte>.Shared` | Only gap not covered by FeatureLoom tools (large, non-LIFO, cross-call); re-evaluate during implementation. |
| Binary field encode/decode | `BinaryPrimitives` on spans | Not yet used in Core (`NumberConversionExtensions` uses `BitConverter`); verify availability on netstandard2.0 via existing `System.Memory` reference. |

## Additional features (adopted from TSDB / Kafka comparison)
### v1
- **Global entry id** (64-bit, decided split 32/32): `(segment seq << 32) | entry index in segment`. 32 bits per segment match the 4-byte position limit; entry count per segment is additionally a hard rotation trigger (relevant only for zero-length payloads without header). Stable for the lifetime of the log: compaction keeps the index record count (deleted records keep their slot, pointing to zero-length payload), so ids never shift. Used for continuation tokens, references and consumer positions (consumers persist the id themselves; no built-in named positions).
- **Zone maps**: min/max per custom field and timestamp, plus OR/AND aggregates of flag bits, per segment (in `.idx` header) and per block of N index records (block summary area). Queries skip whole segments/blocks that can't match.
- **Reverse queries / latest N**: backward scan over fixed-size index records.
- **Index-only queries**: count, existence, time-bucket histograms, field value distribution without reading payloads.
- **MessageFlow integration (write side)**: `LogWriter` as message sink. Writer publishes "flushed up to id" notifications.
- **Monotonic field kind** (`Ascending` / `Descending`): like `Raw`, but writer enforces the order (configurable: reject or clamp). Enables binary search on that field (e.g. caller-supplied event time) like on the index timestamp.
- **Secure delete** (optional): besides setting the deleted flag, overwrite payload (and header field values) with zeros in place for uncompressed segments; compressed segments are handled by compaction. A zeroed record has a dedicated flag so checksum mismatch is not treated as corruption. Archives/backups are out of scope.
- **Carry-forward entries** (optional, per `Handle`/`Raw` field; decided: **copy**, not reference, so segments stay self-contained for archiving and retention): at rollover, the latest entry per field value is copied to the start of the new segment, marked with a `carried` flag (skipped by normal queries). Compromise mode: carry only values that did not occur in the previous segment → "last known value before T" needs at most one segment lookback. Bounded by table size; disabled by default.
- **Smaller segments instead of block-wise compression**: keep whole-file GZip; random access in compressed data is bounded by choosing small segment sizes for logs that need it (zone maps keep segment skipping cheap).

### Later
- Bloom filters per segment/block for `Handle`/`Hash` fields (extends zone maps).
- Live tailing reader (catch-up from id, then follow new entries). Only useful where direct MessageFlow isn't enough: late-joining or restarting consumers that must replay without gaps/duplicates, consumers in other processes, or consumers that are slower than the producer and need a durable buffer.
- Further compaction strategies (e.g. keep latest per key).

## Non-goals / limitations
- Single writer process per log; no replication, partitioning or HA.
- Embedded API only; no network protocol, query language or general aggregations beyond index-only queries.
- Non-monotonic field filters are scans (mitigated by zone maps, later bloom filters).
- Batched flush can lose the last unflushed batch on crash; no cross-log transactions, no exactly-once.
- Schema changes only at rollover.
- Secure delete does not reach backups/archives.

## Steps
1. Define primitives (segment file IO, `ISegmentRecordFraming` with `Binary` framing + checksum, flush policy). `Text` framing later as optional extra step.
2. Define `ILogEntryCodec<T>` and the three built-in codecs.
3. Implement `LogWriter<T>` (append, batching, rotation).
4. Implement the fixed-size index writer, custom fields (incl. `Ascending`/`Descending`), lookup tables, global entry ids, zone maps and delete flagging (incl. secure delete).
5. Implement `LogReader<T>` and the query API (forward/reverse, index-only queries, zone-map skipping).
5a. Implement `MultiLogReader` (runtime merge across logs).
6. Implement index rebuild and recovery from a truncated tail.
7. Implement `LogSegmentPolicy` lifecycle: rotation (incl. carry-forward entries), compaction (id-stable), compression, retention (reader-safe).
7a. Implement archiving: snapshot API, retention `MoveTo`, read-only open of archive folders.
7b. Implement MessageFlow integration (writer as sink, flush notifications).
8. Tests: round trip per codec, queries, multi-log merge, lookup table overflow handle, crash/truncation recovery, concurrent appends, rotation/compression/retention limits, snapshot/archive round trip (copy files → open read-only → same query results).
9. Benchmarks (`MemoryDiagnoser`): append throughput + allocations, query latency (plain vs. compressed segments), multi-log merge.
10. Add a skill file `.github/skills/storage`.

## Open questions
- None currently.
