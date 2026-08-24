# Phase 1 — Writer Side (implemented)

Part of the [Custom Type Handler API redesign](../custom-typehandler-api-redesign.md).

Status: **complete**. User-facing documentation lives in
`FeatureLoom.Core/Serialization/CUSTOM_TYPE_WRITERS.md`; this file records what was built and
why it deviates from the [original API shape](02-api-shape.md).

## Delivered surface

Single registration entry point, three forms:

```csharp
// 1. lambda — the common case; the only form supporting predicate widening
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<Money>((v, m) => v.WriteString(m.ToString())),
	supportsType: null));

// 2. definition instance — can carry constructor state
settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(new PersonWriter("who")));

// 3. open generic definition type
settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
```

Preparation API: `PrepareValueWriter`, `PrepareObjectWriter`, `PrepareArrayWriter` (type-writer,
nested-builder and raw-item overloads), `PrepareRawWriter`, `PrepareTypeWriter<TOther>()` and
`PrepareTypeWriter<TOther>(configure)`.

Object builder: `AddField`, `AddObject`, `AddArray`, `AddRawField`.

## Deviations from the original spec

- `Field`/`Raw` became **`AddField`/`AddRawField`**. `AddValueField` existed briefly and was
  removed — it duplicated `AddField` with no added expressiveness; a conversion inside
  `AddField` covers the same cases, and `AddRawField` is the real escape hatch.
- **`AddObject`/`AddArray` were added.** Not in the original spec; declarative nesting turned
  out to be much more readable than hand-written token emission for nested structures.
- **`FieldIf` was not implemented.** Conditional fields are currently expressible through
  `AddRawField`. Revisit if it comes up in practice.
- **`PrepareTypeWriter<TOther>(configure)`** was added for a preparation-local settings
  deviation. It bypasses the shared per-type cache, so the override cannot leak into the
  writer other call sites see. Predicate registration on such a local override is
  intentionally unsupported.
- **`IWriter` was removed** rather than curated: `JsonUTF8StreamWriter` is internal-only and
  reached through `ValueWriteApi` / `RawWriteApi`.

## Raw/value API refinement (follow-up)

A review of `RawWriteApi` found three separate problems, fixed together:

1. **`PrepareFieldName` sat on the wrong API.** It is a preparation-phase operation, not a write,
   and its doc comment had to warn readers not to call it while writing — a comment apologising for
   the class it lives in. Moved to `WriterPreparationApi`, joined by a new `PrepareRawJson`.
2. **`RawWriteApi` was an arbitrary subset of `ValueWriteApi`.** It lacked `ulong`, `decimal`,
   `float`, `Guid`, `DateTime` and the smaller integer types for no reason — the *more* permissive
   API offered *fewer* types. It is now a superset.
3. **Everything was `string`/`byte[]`-only**, so writing part of an existing buffer forced a
   substring or array copy.

### Types accepted now

| Method | Also accepts |
|---|---|
| `WriteString` | `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WriteFieldName` | `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WriteRawJson` | `JsonFragment`, `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WritePrepared` | `ByteSegment`, `ReadOnlySpan<byte>`¹ |

¹ `#if !NETSTANDARD2_0`, following the existing convention in `ByteSegment.AsSpan()` and the
writer's own string paths.

Decisions behind that table:

- **`JsonFragment` does not replace the segment/span overloads.** It holds a whole `string` *or* a
  whole `byte[]` with no offset/length, so it cannot express a slice; `ByteSegment`'s conversion
  from arbitrary memory is `ToArray()`, i.e. a copy. `JsonFragment` earns its place only for a
  complete, pre-encoded fragment — where its string↔utf8 caching is the point. It did replace the
  originally planned `PreparedJson` wrapper type.
- **Spans were kept despite the netstandard2.0 cost.** Plenty of BCL and third-party types hand out
  a span and nothing else, and those cannot be converted to `TextSegment`/`ByteSegment` without an
  allocation — which would defeat the purpose.
- **Prepared field names stayed `byte[]`, not `JsonFragment`.** A prepared name is `"name":` —
  quotes and colon, not valid JSON standalone. Typing it as `JsonFragment` would let a plain
  `"name"` string flow in through the implicit conversion and emit unquoted garbage silently.
- **`PrepareRawJson` forces UTF-8 eagerly.** `JsonFragment` is a mutable struct that caches its
  converted form into itself; a copied struct would silently re-encode on every write.

### Implementation notes

`WriteEscapedStringWithQuotes` and the raw `WriteString` were refactored into span-based primitives
with the old `(string, startIndex, length)` form kept as the `netstandard2.0` fallback, so the
optimized escape/transcode loops exist once, not twice.

Two accepted breakages:

- `WriteString(null)` / `WriteRawJson(null)` no longer compile (ambiguous between the `string` and
  span overloads). The replacement is `WriteNull()`, which states the intent better than a `null`
  that happened to be handled downstream.
- `TryPreparePrimitiveWriteDelegate` / `TryGetPrimitiveWriteMethod` looked up write methods by
  **name only**, which the new `WriteStringValue` overload made ambiguous at runtime. Both now
  resolve by exact parameter type. This was pre-existing fragility, surfaced by the change.

## Open generic custom writers

Decision: **definition class**, closed by the serializer per constructed type.

```csharp
class WrapperWriter<T> : JsonSerializer.CustomTypeWriterDefinition<Wrapper<T>>
{
	protected override CustomWriter<Wrapper<T>> Prepare(WriterPreparationApi api) =>
		api.PrepareObjectWriter<Wrapper<T>>(obj => obj.AddField("v", w => w.Value));
}

settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
```

Why a `Type` and not an instance: an unbound generic type has no instances, so whatever is
registered must be something the serializer can close later. The alternatives (a closed dummy
instance re-opened via `GetGenericTypeDefinition`, or a `Func<Type[], …>` factory) either need a
dummy type argument satisfying the constraints or push `MakeGenericType` onto the user.

`ITypeHandlerCreator` turned out to be the right seam: it is already invoked exactly once per
concrete type, so closing, `Activator.CreateInstance` and `Prepare` are all off the write path.
No change to `CreateCachedTypeWriter` was needed — `TryGetTypeSettings` already falls back to the
generic type definition entry, and the closed-type lookup happens first.

Rules:
- arity of the definition must equal that of the configured type definition; arguments are passed
  **positionally** (a definition declaring them in another order is rejected).
- any arity works (tested with 1, 2 and 3 parameters).
- exact match on the generic type definition; no derived-type widening.
- validated at registration: both are generic definitions, non-abstract, derived from
  `CustomTypeWriterDefinition<>`, matching arity. Validated at writer creation: `MakeGenericType`
  failures (constraints) are rewrapped naming the types, and the closed definition's handled type
  must equal the constructed type.

Accepted trade-off: `SetCustomTypeWriter` now has three forms. They divide by capability, not
taste — only the lambda supports `supportsType` widening, only the instance can carry constructor
state, only the `Type` form works for open generics.

## Dead code removed afterwards

The redesign left the old type-handler plumbing unreachable. Deleted:

| Removed | Reason |
|---|---|
| `JsonSerializer.ExtensionApi` | only reached via `ITypeHandlerCreator`; the custom writer creator just unwrapped it to get the serializer back |
| `GenericTypeHandlerCreator` | no implementations left; superseded by `OpenGenericTypeWriterCreator` |
| `TryCreateItemHandlerDelegate<T>` | unused delegate |
| `ICachedTypeHandler` | only implementer and consumer was `CachedTypeWriter` itself |
| `JsonDataTypeCategory` | only used by `ExtensionApi.SetItemHandler` |

`ITypeHandlerCreator` remains, but is now internal and concretely typed:
`CreateTypeHandler(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type)`, which also
removed the runtime `is not CachedTypeWriter` casts from both creator paths.

Note: `JsonDeserializer.ExtensionApi` is a **different, still-live type** and was not touched.

## Task list

- [x] Implement writer side (closed types)
- [x] Rewrite consumers/tests in the new API (`JsonSerializerCustomTypeWriterTests`,
	  `JsonSerializerPrimitiveTests`, `CustomTypeHandlerTest` benchmark; `Playground/JsonTest.cs`
	  dropped instead of ported)
- [x] Exact-type-first precedence, documented on `SetCustomTypeWriter`
- [x] Collapse to a single entry point; `ConfigureTypesWhere` and `AddCustomTypeHandlerCreator`
	  removed
- [x] Remove `IWriter`
- [x] Builder surface finalised: `AddField`, `AddObject`, `AddArray`, `AddRawField`
- [x] `PrepareArrayWriter` nested-builder and raw-item overloads
- [x] `PrepareTypeWriter<TOther>(configure)`
- [x] User-facing doc with Newtonsoft/STJ comparison
- [x] Dead code removal
- [x] Open generic custom writers + definition-instance overload
- [x] `PrepareFieldName` moved to the preparation API; `PrepareRawJson` added
- [x] `RawWriteApi` completed to a superset of `ValueWriteApi`
- [x] Segment/span overloads for `WriteString`, `WriteFieldName`, `WriteRawJson`, `WritePrepared`
- [x] `$type` behaviour pinned by tests for all four shapes
- [x] `PrepareTypeInfo(string)` / `PrepareTypeInfo<TOther>()` for writers that claim a foreign type

## Type info for custom writers

Verified by test, not changed: type info already worked for every shape, because
`ApplyCustomWriter` routes all four shapes through the same `CreatePrimitiveItemWriter` /
`CreateObjectItemWriter` / `CreateArrayItemWriter` wrappers the built-in writers use. A custom
writer never writes `$type` itself. Seven tests now pin this down, including the empty-object case
(comma after the type info rolled back) and `AddDeviatingTypeInfo`.

The one real gap was **mutating** the written type — mimicking a DTO or an older class version.
Rejected the idea of a dedicated "override the type name" setting: the type name is only half of
it, since such a writer usually also changes the member shape, and a second naming mechanism next
to `SetCustomTypeName` would compete with the existing precedence rules.
(Follow-up: the redundant global `AddCustomTypeName` / `ClearCustomTypeNames` were removed
altogether, so `SetCustomTypeName` is now the single naming mechanism. `ConfigureType(Type, ...)`
was added to keep the runtime-`Type` case covered.)

Instead the existing suppression is combined with a new preparation-phase encoder:

1. `SetTypeInfoHandling(AddNoTypeInfo)` on the type or member scope removes the built-in envelope.
2. `PrepareTypeInfo(string)` or `PrepareTypeInfo<TOther>()` encodes `"$type":"..."` once, and the
   writer emits it via `WritePrepared`.

`PrepareTypeInfo<TOther>()` resolves through `ResolveTypeName`, so a configured custom name or the
configured name format is honored rather than hardcoded; this required widening `ResolveTypeName`
from `private` to `internal`. Both overloads keep the encoding in phase 1, consistent with
`PrepareFieldName` / `PrepareRawJson`.

The `string` overload takes a name, never a `Type`, so the mimicked type does not have to exist in
this process — the usual case is a type that only the consuming (legacy or foreign) system knows.
No validation is applied to the name for that reason.

Both steps also work on member settings, because `MemberWriteSettings<T>` derives from
`TypeWriteSettings<T>` and therefore inherits `SetTypeInfoHandling` and `SetCustomTypeWriter`. So a
single member can claim a foreign type while other members of the same type are unaffected; this
needed no new API and is covered by a test using two `Money` members on one type.

Known sharp edge, documented rather than guarded: emitting `$type` *without* suppressing the
built-in one writes it twice. Detecting that would mean inspecting the user's token stream, which
is exactly what the raw API declines to do.
