# 08 · Performance

How FeatureLoom's serializer and deserializer spend time, and which settings matter.

## Why

STJ and Newtonsoft make you choose: fast but rigid (STJ source generation) or flexible but slow
(Newtonsoft reflection, `JObject` detours). FeatureLoom keeps its flexibility (layered settings,
per-member mapping, custom handlers, polymorphism) and moves the cost of that flexibility to
**preparation time**, once per type.

## Cost model

| Phase | When | Cost |
|---|---|---|
| Settings compilation | constructing a `JsonSerializer` / `JsonDeserializer` | once per instance |
| Type handler creation | first time a type is written/read by an instance | once per type and instance; reflection, name encoding, handler lookup, type-owned configuration discovery |
| Hot path | every value | UTF-8 in, UTF-8 out; no reflection, no per-value settings lookup |

Consequences:

- **Reuse serializer/deserializer instances.** A new instance per call pays the setup cost every
  time, which is the most common performance mistake.
- The first call per type is noticeably slower than the following ones. Warm up at startup if
  first-request latency matters.
- The number of settings and configured types has virtually no effect on the hot path.

⚠ **STJ**: a new `JsonSerializerOptions` per call discards the metadata cache. This is the same
pitfall, but it is easy to hit because options are often created inline.

## Thread safety

An instance can be used from several threads. Calls on the same instance are serialized by an
internal lock, because the instance owns its buffers. For high parallel throughput, use one
instance per thread or a small pool of instances with identical settings instead of one shared
instance.

`JsonHelper.DefaultSerializer` / `DefaultDeserializer` are shared instances. They are convenient,
but under heavy concurrent load they become a contention point.

## Relevant settings

### Serializer

| Setting | Default | Performance note |
|---|---|---|
| `referenceCheck` | `NoRefCheck` | fastest; see [page 07](07-references.md) for the trade-offs of the other modes |
| `referenceFormat` | `JsonPath` | `IdBased` is faster to write, `JsonPath` produces less output |
| `typeInfoHandling` | `AddDeviatingTypeInfo` | `AddAllTypeInfo` adds output size and work |
| `indent` | `false` | indentation costs size and time; keep it for diagnostics |
| `writeBufferChunkSize` | 64 KB | chunk size when writing to streams |
| `tempBufferSize` | 8 KB | scratch buffer for number/string formatting |

### Deserializer

| Setting | Default | Performance note |
|---|---|---|
| `referenceResolutionMode` | `DisabledByDefault` | `ForceDisabled` removes all tracking; enable only for types that need it |
| `proposedTypeMode` | `CheckWhereReasonable` | `Ignore` skips `$type` evaluation entirely |
| `useStringCache` | `true` | deduplicates recurring strings, see below |
| `stringCacheBitSize` | 12 (4096 entries) | fixed memory, no growth |
| `stringCacheMaxLength` | 128 | longer strings bypass the cache |
| `initialBufferSize` | 128 KB | read buffer; larger payloads grow it |
| `populateExistingMembers` | `true` | reuses existing member instances instead of allocating new ones |
| `typeSelfConfigurationMode` | `IgnoreButWarn` | `EnabledKeepRefTrackingOff` keeps the reference-tracking shortcut, see page 03 |

## String cache

Recurring string values (status codes, currencies, names, enum-like strings) are materialized once
and shared. This reduces allocations and GC pressure, not just parsing time.

- Fixed size (2^`stringCacheBitSize` entries). Memory does not grow with the payload.
- Bounded probing with age-based replacement. Unique strings still pay hash + probe + insert and
  evict useful entries, so they cost time (see *Mixed string dataset* below).
- Toggle it per type, per member or recursively. Disable it for fields that are always unique
  (ids, hashes, free text), and keep it for low-cardinality fields.

See [page 03](03-type-configuration.md) for the configuration API.

⚠ Neither STJ nor Newtonsoft deduplicates string values. Every occurrence allocates a new string.

## Avoiding detours

The fastest intermediate representation is none:

- **Per-member mapping** instead of deserializing to a DTO and copying (page 03).
- **`JsonFragment`** to pass through subtrees without materializing them.
- **Two-phase custom writers/readers** instead of converters that look up names or handlers per value
  (page 04).
- **Type mappings** instead of reading into a DOM to find the discriminator (page 05).

⚠ **STJ**: polymorphic payloads whose discriminator is not the first property require
`AllowOutOfOrderMetadataProperties` (buffering) or a `JsonDocument` pre-pass.
⚠ **Newtonsoft**: the common pattern for conditional logic is `JObject.Load` + `ToObject`, which
parses the data twice and allocates a full DOM.

## Benchmark results

> **Preliminary.** Values are taken from existing BenchmarkDotNet artifacts
> (`FeatureLoom.PerformanceTests`, .NET 10, AMD Ryzen 5 PRO 5650U, notebook, not a clean system).
> Runs were done at different times. Absolute numbers will change after the planned clean rerun;
> ratios are more meaningful. **Ratio** (time) and **Alloc ratio** (allocated bytes) are relative
> to STJ (reflection) in the same run; lower is better.
> SpanJson is included as a speed reference: a UTF-8-only serializer with no comparable
> configurability. Newtonsoft was only part of the mixed string run.

### Complex object (main reference)

`ComplexObject` mixes strings (plain and escaped), all common numeric types, `int?`, `bool`, enum,
`Guid`, `DateTime`, `TimeSpan`, `IList<int>`, `byte[]`, `string[]`, `Dictionary<string,string>`,
an embedded struct, an embedded object and a list of objects. *Single* = one object, *Array* =
1000 objects.

**Serialize**

| | Single | Ratio | Alloc | Alloc ratio | Array (1000) | Ratio | Alloc | Alloc ratio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **FeatureLoom** | **1.37 µs** | **0.58** | **56 B** | **0.15** | **1.35 ms** | **0.57** | **56 KB** | **0.77** |
| STJ | 2.35 µs | 1.00 | 384 B | 1.00 | 2.36 ms | 1.00 | 73 KB | 1.00 |
| STJ source gen | 2.21 µs | 0.94 | 384 B | 1.00 | 2.29 ms | 0.97 | 73 KB | 1.00 |
| SpanJson | 1.40 µs | 0.59 | 56 B | 0.15 | 1.42 ms | 0.60 | 56 KB | 0.77 |

**Deserialize**

| | Single | Ratio | Alloc | Alloc ratio | Array (1000) | Ratio | Alloc | Alloc ratio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **FeatureLoom** | **2.59 µs** | **0.60** | **1.25 KB** | **0.42** | **2.64 ms** | **0.55** | **1.26 MB** | **0.50** |
| FeatureLoom, no string cache | 3.16 µs | 0.73 | 1.98 KB | 0.67 | 3.22 ms | 0.67 | 1.99 MB | 0.79 |
| STJ | 4.35 µs | 1.00 | 2.95 KB | 1.00 | 4.79 ms | 1.00 | 2.51 MB | 1.00 |
| STJ source gen | 4.12 µs | 0.95 | 2.95 KB | 1.00 | 4.71 ms | 0.98 | 2.51 MB | 1.00 |
| SpanJson | 2.49 µs | 0.57 | 2.37 KB | 0.80 | 2.72 ms | 0.57 | 2.38 MB | 0.95 |

FeatureLoom is ~1.7× faster than STJ in both directions, on par with SpanJson, and allocates the
least when deserializing: half of STJ, thanks to the string cache.

⚠ STJ source generation barely helps here (2-5 %). It mainly improves startup and trimming,
not throughput.

### Simple object

`SimpleObject` = `int id`, `string name`, `double value`.

| Deserialize | Single | Ratio | Alloc | Alloc ratio | Array (1000) | Ratio | Alloc | Alloc ratio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **FeatureLoom** | **152 ns** | **0.50** | **40 B** | **0.42** | **124 µs** | **0.48** | **48 KB** | **0.40** |
| STJ | 303 ns | 1.00 | 96 B | 1.00 | 259 µs | 1.00 | 121 KB | 1.00 |
| SpanJson | 103 ns | 0.34 | 96 B | 1.00 | 114 µs | 0.44 | 104 KB | 0.86 |

### String cache: mixed string dataset

Records with 6 low-cardinality strings (country, region, status, category, 2 tags) and 4 unique
ones (user name, session id, description, transaction tag). Each record is read exactly once.

| Deserialize | Time | Ratio | Alloc | Alloc ratio |
|---|---:|---:|---:|---:|
| FeatureLoom, cache for all fields | 7.52 ms | 0.90 | 4.87 MB | 0.47 |
| FeatureLoom, no cache | 4.95 ms | 0.59 | 5.85 MB | 0.56 |
| **FeatureLoom, cache only for low-cardinality fields** | **5.72 ms** | **0.68** | **3.91 MB** | **0.37** |
| STJ | 8.37 ms | 1.00 | 10.43 MB | 1.00 |
| Newtonsoft | 14.64 ms | 1.75 | 61.09 MB | 5.86 |
| SpanJson | 5.04 ms | 0.60 | 5.93 MB | 0.57 |

(The report itself uses "cache for all fields" as baseline; ratios here are rebased to STJ.)

This is the honest trade-off: caching unique values costs time without saving memory. **Selective**
(−63 % vs. STJ
cost compared to no cache. The default (cache everywhere) is tuned for typical payloads with mostly
recurring short strings, such as the complex object above; configure per member when a payload
contains many unique strings.

### String values

Arrays of 1000 identical strings of a given kind, deserialize. Ratio vs. STJ.

| Kind (length) | FeatureLoom ratio | Alloc | Alloc ratio | no cache ratio | Alloc | Alloc ratio |
|---|---:|---:|---:|---:|---:|---:|
| Short (2) | 0.58 | 8 KB | 0.14 | 0.66 | 40 KB | 0.71 |
| ASCII (44) | 0.40 | 8 KB | 0.06 | 0.58 | 120 KB | 0.88 |
| Escaped (26) | 0.56 | 8 KB | 0.08 | 1.02 | 88 KB | 0.84 |
| Emoji (400) | 0.62 | 832 KB | 0.98 | 0.63 | 832 KB | 0.98 |
| Long ASCII (1000) | 1.03 | 2.0 MB | 0.99 | 1.03 | 2.0 MB | 0.99 |
| Latin-1 (200) | 1.45 | 432 KB | 0.96 | 1.46 | 432 KB | 0.96 |
| Heavily escaped (200) | 2.16 | 432 KB | 0.96 | 2.16 | 432 KB | 0.96 |

Recurring short strings are deduplicated (−94 % allocation). Strings longer than
`stringCacheMaxLength` (128) bypass the cache.

⚠ **FeatureLoom weaknesses**: long Latin-1 and heavily escaped strings are decoded slower than
STJ (1.5-2.2×). These are known optimization targets.

| Serialize (string arrays, same workload per row) | FeatureLoom | STJ | SpanJson |
|---|---:|---:|---:|
| ASCII (44) | 265 µs | 377 µs | 488 µs |
| Escaped (26) | 613 µs | 1,027 µs | 873 µs |
| Control chars (200) | 3.14 ms | 11.16 ms | 11.39 ms |
| CJK (200) | 3.23 ms | 4.86 ms | 4.57 ms |
| Emoji (400) | 18.84 ms | 37.61 ms | 8.95 ms |
| Short (2) | 194 µs | 166 µs | 186 µs |
| Empty | 177 µs | 78 µs | 115 µs |

⚠ FeatureLoom is slower than STJ for empty and very short strings when writing.

String serialization allocates nothing measurable for all three libraries, so no alloc ratio is shown.

## Checklist

1. Reuse instances; one per thread or a pool under high concurrency.
2. Warm up types that are latency-critical.
3. Keep `referenceCheck = NoRefCheck` for trees; use `AlwaysReplaceByRef` for shared graphs.
4. Keep `referenceResolutionMode` at `DisabledByDefault` or `ForceDisabled`; enable per type.
5. Set `proposedTypeMode = Ignore` if payloads never carry `$type`.
6. Disable the string cache for high-cardinality fields.
7. Replace DOM detours with member mapping, `JsonFragment` or type mappings.

Benchmarks live in `FeatureLoom.PerformanceTests` (BenchmarkDotNet).
