---
applyTo: "**/Serialization/**"
---
# JSON Serializer / Deserializer

## Architecture
- `FeatureLoom.Core/Serialization/`, namespace `FeatureLoom.Serialization`.
- `JsonSerializer` and `JsonDeserializer` are `sealed partial` classes split by concern. Find the right partial before reading:
  - `JsonDeserializer.cs` — entry points, ctor, fields
  - `.Settings.cs` — global settings, `ConfigureType<T>` / `ConfigureMember<TMember>` per-member overrides
  - `.Parsing.cs` — primitives, numbers, strings
  - `.Buffer.cs` / `.BufferHandling.cs` — byte-level reads, recording, refill
  - `.ReaderStrategies.cs` — per-member reader structs (one variant per configuration combination)
  - `.CachedTypeReader.cs` / `.TypeReaderCreation.cs` — compiled per-type readers
  - `JsonSerializer.*.cs` mirrors this (`WriterStrategies`, `PrimitiveWriters`, `JsonUTF8StreamWriter`, handlers)
- Manual UTF-8 parsing over pooled buffers. Readers/writers are compiled once per type and cached; per-member configuration is baked into strategy structs at that point, not checked per value.
- `Utf8StringCache` (`FeatureLoom.Core/Collections/`) dedupes recurring string values; usable globally or per member.

## Rules
- Work in UTF-8 bytes. Do not add `Encoding.GetString` on a hot path just to reuse a `string` API.
- Do not add per-value branches for something decidable at reader-creation time — add a strategy variant instead.
- Keep hot methods small. Move `throw` into separate `ThrowXxx()` methods so the caller stays inlinable.
- Fast path + slow fallback is the established pattern (e.g. `TryReadSimpleStringBytes` for escape-free strings). Keep the fast path allocation-free and let the fallback stay simple.
- Multi-target: .NET Framework 4.8 / netstandard2.0 / netstandard2.1 / .NET 8 / .NET 10. `Span`-, `Utf8Parser`- or `Math`-based APIs may need `#if NET6_0_OR_GREATER` (or similar) with a working fallback.
- Standard conformance beats micro-optimization. If a fast path can produce a different value than the spec-correct path, it must be guarded and covered by a test.
- Try keep patterns consistent between serializer and deserializer. If a change is made to one, check the other for symmetry.

## Writer settings resolution
- A context-local override (member settings, `PrepareTypeWriter(configure)`, `AddField/AddArray(…, configure)`) is **merged onto** the type's general settings via `BaseTypeWriteSettings.MergeOnto`, not used as a replacement. Local value wins per field and per member name.
- Merged writers are **not** put into the shared per-type cache — they are only valid in the context the override came from.
- Termination for recursive types relies on the merge being one level deep (`isMerged` flag). Do not remove that guard without a replacement cycle check.
- Runtime polymorphy: a value whose runtime type deviates is written by the runtime type's writer. `GetTransferableSubset()` decides what follows it — policy fields and `memberSettingsDict` do, `customTypeName` and `customTypeWriterCreator` do not.
- `CachedTypeWriter.NoRefTypes` describes the **declared** type only. Use `NoRefTypesIncludingRuntimeTypes` when the declared type can deviate at runtime, otherwise ref-path bookkeeping can be skipped unsoundly.

## Definition of done
Any parsing/formatting change needs a regression test in `FeatureLoom.Tests/Serialization/` (see the testing instructions), and a benchmark run if it was performance motivated.
