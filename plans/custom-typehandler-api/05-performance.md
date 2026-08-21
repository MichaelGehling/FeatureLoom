# Custom Type Handler API — Performance

Part of the [Custom Type Handler API redesign](../custom-typehandler-api-redesign.md).

The "close to built-in" goal must be measured, not claimed.

Benchmark: `FeatureLoom.PerformanceTests/JsonSerializer/CustomTypeHandlerTest.cs`. For each
shape the same payload is written once by the built-in handler and once by a custom handler.
Absolute times do not matter; the **built-in/custom ratio within a run** is the number to
defend. (No benchmark covered custom handlers before — verified against all 315 benchmarks in
`FeatureLoom.PerformanceTests`.)

## Baseline (old API, .NET 10, Ryzen 5 PRO 5650U, BDN 0.15.8)

| Case | Built-in | Custom | Custom vs built-in |
|---|---:|---:|---|
| Value, single | 49.78 µs | 36.43 µs | **0.73×** (faster) |
| Value, array | 512.5 µs | 383.0 µs | **0.75×** (faster) |
| Object, single | 155.8 µs | 170.8 µs | **1.10×** (slower) |
| Object, array | 1512 µs | 1754 µs | **1.16×** (slower) |

All serialize cases allocate 0 B on both sides.

Reading of the baseline:

- The **value** case is faster than built-in only because it does less work: it collapses the
  type into one string instead of writing an object. That is the payload difference, not a
  handler-path advantage. It does prove the primitive wrapper adds no measurable overhead.
- The **object** case is the real target. A hand-written custom object handler was 10–16%
  slower than the generated one, writing identical JSON. Suspected cause: the hand-written body
  re-encodes field-name strings on every call, while the generated handler uses field-name bytes
  prepared once plus merged commas. **Target: object-builder ratio ≤ 1.0×.**

## Why the object builder was expected to close the gap

`CreateFieldValueWriter<T, V>` in `ComplexHandler.cs` is why the generated handler wins. Per
field it does three things the hand-written form does not:

1. `writer.PrepareFieldNameBytes(fieldName)` once in phase 1, then emits those bytes with a
   single `WritePreparedBytes` call.
2. Merges the separating comma **into** those prepared bytes (`withLeadingComma`), so a field
   costs one buffer copy instead of a `WriteComma` plus a name write.
3. Compiles name-write + value-write into **one** delegate via `Expression.Lambda`, and when
   `writer.TryGetPrimitiveWriteMethod<V>` succeeds the value write is a direct call — no getter
   delegate, no handler lookup, no boxing.

The object builder gets points 1 and 2 for free, because `AddField` is declared in phase 1, and
most of 3 — one delegate call per field remains versus the built-in's zero.

## Re-measurement (new API, same machine/BDN version)

| Case | Baseline (old API) | New API | Target |
|---|---:|---:|---|
| Value, single | 0.73× | 0.61× | — |
| Value, array | 0.75× | 0.57× | — |
| Object, single | 1.10× | 1.04× | ≤ 1.0× |
| Object, array | 1.16× | 1.13× | ≤ 1.0× |

Still 0 B allocated in every serialize case.

**The object-builder target was not met.** Honest reading of this run:

- The run was noisy (StdDev up to 190 µs on a 1470 µs mean), so the 1.10→1.04 and 1.16→1.13
  movements are within noise and must not be claimed as an improvement. Absolute numbers also
  shifted broadly against the baseline run, so only within-run ratios are comparable.
- Preparing the field-name bytes once was therefore **not** sufficient to close the gap. The
  remaining cost is most likely the per-field delegate indirection (one `Func<T,TValue>` plus one
  `Action<T>` per field) against the generated handler's inlined body.
- The value cases improved their ratio, but that remains a payload difference (one string instead
  of an object), not evidence about the handler path.

## If the gap is to be chased

- Collapse the field-writer array into a single generated delegate.
- Specialise the common field-count cases beyond the existing 0/1 split.
- Add an `Expression<Func<T, TField>>` overload as an opt-in fast path (see resolved decision 5)
  so plain member access can be compiled in rather than invoked.

Any attempt needs a low-noise run first — the current numbers cannot resolve a few percent.

## Caveat on the deserialize figures

The deserialize pair in the benchmark is **not** an apples-to-apples parse comparison — each
deserializer reads the JSON its own serializer produced (object vs. bare string), so the input
sizes differ. It is kept as an end-to-end representation-cost figure only, and must not be
quoted as custom-vs-built-in reader overhead. A real reader comparison is a task for
[phase 2](04-reader-implementation.md).
