# JsonSerializer.SerializeAsync – double buffering

## Goal
Intermediate buffer flushes in `SerializeAsync` should not block on stream I/O. When the buffer is full, start `WriteAsync` for it, swap to a spare buffer and continue serializing synchronously.

## Design
- `JsonUTF8StreamWriter` gets `asyncMode`, `spareBuffer` (lazy, reused) and `pendingWrite` (Task).
- `WriteBufferToStream()` (flush on full buffer): if `asyncMode`, wait for a previous pending write (only blocks if I/O is slower than serialization), start `WriteAsync(mainBuffer)`, swap buffers.
- At most one write in flight -> stream never used concurrently, chunk order preserved.
- `WriteBufferToStreamAsync()` awaits the pending write and writes the final chunk.
- On failure the pending write is awaited before the buffers are reused (`CompleteAsyncModeAsync`).
- Sync `Serialize` path unchanged; the mode check happens only when flushing.
- Safety check: all `var buffer = mainBuffer` caches in the writer are in "WithoutCheck" methods (no flush in between) -> swap is safe.

## Steps
- [x] Analyze writer flush points
- [x] Implement in writer + `SerializeAsync`
- [x] Tests: multi-chunk async output, large strings with small buffer, failing stream + reuse (JsonSerializerStreamTests)
- [x] Build + test run (all FeatureLoom.Tests pass; one flaky timing test in AggregatorTests passed on rerun)
- [ ] Optional: benchmark SerializeAsync against a slow/real stream

## Future
- Parallel serialization of large containers / multiple root items (ref tracking, indentation, separators need handling).
