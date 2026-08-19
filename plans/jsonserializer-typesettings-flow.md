# Plan: Align JsonSerializer type-settings flow with JsonDeserializer

Status: steps 0-4 **implemented and green** (2050 passed, 0 failed). Step 5 (benchmark
verification) is still open. Risk 1 (recursion) is resolved - see section 5.1.

## 1. How the deserializer does it (target model)

```
GetCachedTypeReader(Type)                       -> cache lookup only
GetCachedTypeReader(Type, BaseTypeSettings)     -> bypasses cache when settings != null
CreateCachedTypeReader(Type, BaseTypeSettings)  -> settings flow through the whole creation
   -> TypeReaderPreInitializer(this, type, typeSettings)
   -> TypeReaderInitializer.Create(..., typeSettings)
   -> CachedTypeReader.typeSettings  (exposed as .TypeSettings)
```

Key properties:
- The settings object is a **parameter**, resolved once at the top of `CreateCachedTypeReader`
  (explicit override > exact type > generic type definition) and then simply carried along.
- A non-null override means **do not touch `typeReaderCache`** — the result is a local variant,
  not the shared one for that type.
- The reader keeps its settings (`CachedTypeReader.TypeSettings`), so nested creations
  (`ReaderStrategies` array/element readers) can pass the parent's settings on.
- Member settings are just settings: `MemberSettings<T> : TypeSettings<T>`, and
  `GetCachedTypeReader(fieldType, memberSettings)` produces a **member-local field type reader**.

## 2. What the serializer does today

```
GetCachedTypeWriter(Type)          -> cache lookup only, no settings parameter at all
CreateCachedTypeWriter(Type)       -> no settings parameter
```

Instead of flowing, each creation site re-resolves from the compiled settings by type:

| Site | Call |
|---|---|
| `CachedTypeWriter` ctor | `settings.ResolveTypeInfoHandling(handlerType)` |
| `ArrayWriters` (3x) | `settings.ResolveWriteByteArrayAsBase64String(typeWriter.HandlerType)` |
| `WriterStrategies:516` | `settings.ResolveWriteByteArrayAsBase64String(typeof(byte[]))` |
| `PrimitiveWriters:1526` | `settings.ResolveEnumAsString(typeof(T))` |
| `EnumerableHandler:17` | `settings.ResolveTreatEnumerablesAsCollections(itemType)` |
| `ComplexHandler:33,167` | `settings.TryGetTypeSettings(itemType, out var typeSettings)` |

Each `Resolve*` internally repeats the full `TryGetTypeSettings` fallback chain
(exact type → nullable underlying → generic type definition).

### The actual functional gap
`ComplexHandler` *does* look up `memberSettings` (`GetMemberSettings`), but only uses them for
`member_ignore` and `member_overrideName`. The field's type writer is obtained with
`GetCachedTypeWriter(fieldType)` — **without** the member settings. So on the serializer side a
per-member variant of a type writer is impossible, while the deserializer supports exactly that
(`GetCachedTypeReader(fieldType, memberSettings)`). Anything a user configures on a member that
concerns *how the value type is written* (e.g. `enumAsString`, `writeByteArrayAsBase64String`,
`typeInfoHandling`) is silently ignored today.

`WriterStrategies:516` is a related symptom: it resolves the base64 setting for the literal
`typeof(byte[])` rather than for the element context it is actually building.

## 3. Answer to the open question

The two options are not alternatives — the deserializer uses **both**, and they solve different halves:

- **Pass through creation** (parameter on `Create/GetCachedTypeWriter`) is what makes local
  variants *possible* and keeps the resolution in exactly one place.
- **Store on `CachedTypeWriter`** is what makes them *followable*: nested/deferred creation steps
  read `typeWriter.TypeSettings` instead of re-resolving from the global settings by type.

Recommendation: do both, mirroring the deserializer.

## 4. Proposed steps

Ordered so each step builds and the suite stays green.

### Step 1 — carry the settings (no behavior change) — DONE
- Add `readonly BaseTypeWriteSettings typeSettings` + `public BaseTypeWriteSettings TypeSettings`
  to `CachedTypeWriter`; add it as a ctor parameter.
- `CreateCachedTypeWriter(Type itemType, BaseTypeWriteSettings typeSettings = null)`:
  resolve once at the top exactly like the reader does
  (`if (typeSettings == null) settings.TryGetTypeSettings(itemType, out typeSettings)`),
  then hand it to the `CachedTypeWriter` ctor.
- Resolve `typeInfoHandling` in the ctor from the passed settings instead of calling
  `settings.ResolveTypeInfoHandling(handlerType)`.

At this point nothing passes a non-null override yet, so behavior is identical.

### Step 2 — consume the carried settings instead of re-resolving — DONE
Replace the per-site lookups with resolution against `typeWriter.TypeSettings`:
- `ArrayWriters` (3 sites) and `WriterStrategies:516` → base64 flag from the writer's settings.
- `PrimitiveWriters:1526` (`CreateEnumItemHandler`) → needs the `typeHandler` in scope; it is
  created via `CreateAndSetItemHandlerViaReflection`, which already passes `typeHandler`.
- `EnumerableHandler:17` → from the writer's settings.
- `ComplexHandler:33,167` → use `typeHandler.TypeSettings` instead of `settings.TryGetTypeSettings`.

Add `Resolve*(BaseTypeWriteSettings)` overloads next to the existing `Resolve*(Type)` ones in
`CompiledSettings` so the fallback-to-global logic stays in one place. Keep the `Type` overloads
for any call site that genuinely has no writer.

### Step 3 — enable local variants — DONE
- `GetCachedTypeWriter(Type itemType, BaseTypeWriteSettings typeSettings)`:
  bypass `typeWriterCache` when `typeSettings != null`, mirroring `GetCachedTypeReader`.
- In `CreateCachedTypeWriter`, only do `typeWriterCache[itemType] = typeHandler` when there is no
  override (the recursion pre-registration must stay for the non-override path).
- No "in progress" set / keyed override cache is required — see 5.1. The
  `overriddenTypeWriterCache` added earlier in `JsonSerializer.cs` was motivated by the
  now-disproven recursion risk and should be **removed** again: keying a cache on a settings
  instance would keep a writer alive per settings object without any benefit.
- `ComplexHandler`: `GetCachedTypeWriter(fieldType, memberSettings)` in both
  `CreateTypedComplexItemHandler<T>` and `CreateTypedComplexItemHandler_ForNullableStruct<T>`.

### Step 4 — tests — DONE
New file `FeatureLoom.Tests/Serialization/JsonSerializerMemberSettingsTests.cs`:
- enum member with `SetEnumAsString` opposite to the global setting → only that member differs;
- `byte[]` member with `SetWriteByteArrayAsBase64String` overridden;
- member override does **not** leak into the shared writer for the same type used elsewhere
  (same type as another member without override, and as a root value);
- `SetTypeInfoHandling` on a member;
- ~~recursive type with a member override still terminates~~ — already covered by
  `JsonRecursiveTypeWithMemberSettingsTests` (done).

Plus the existing suite as regression for step 1/2 (which must be behavior-neutral).

### Step 5 — verify no hot-path regression — OPEN
Steps 1–3 only move work into writer creation, which is cached per type, so the write path should
be untouched or slightly cheaper. Confirm with the existing complex-object and enum serialization
benchmarks (baseline before step 1, compare after step 3). Report before/after.

## 5. Risks / open points

1. ~~**Cache-key semantics / infinite recursion through an overridden member.**~~ **RESOLVED — not
   an issue.** The earlier claim ("a type recursing through a configured member would loop
   forever") was wrong.

   Why it terminates: creation recursion is driven by the **settings tree**, not by the type
   graph. Every `ConfigureMember` call allocates a *fresh* settings object, so a settings object
   can never (transitively) contain itself — the configuration is a finite tree by construction.
   Each descent into a member-local writer/reader consumes exactly one level of explicitly written
   configuration. At the deepest configured level `memberSettingsDict` is empty, the member falls
   back to the plain `GetCachedTypeWriter(fieldType)` / `GetCachedTypeReader(fieldType)`, and that
   path is the cached, pre-registered, recursion-safe one. So the depth is bounded by the
   configuration depth the user literally typed — not by data depth or type recursion.

   Consequence: member-local writers can be created uncached exactly like member-local readers.
   No "in progress" set, no keyed override cache, no restriction on overrides in recursive
   positions.

   Proven by `FeatureLoom.Tests/Serialization/JsonRecursiveTypeWithMemberSettingsTests.cs`
   (3 tests, passing): recursive `Node` with a member setting on the recursive member; the same
   with 3 levels of nested `ConfigureMember` on the recursive member; and the serializer over the
   same recursive shape.

   Invariant to protect: **settings objects must never be aliased or reused across members.** If
   that ever changes, the tests above hang rather than fail — intentional, a hang is the clearer
   signal. The test file documents this.
2. **Duplicate writers.** Each member override creates its own `CachedTypeWriter`. Same trade-off
   the reader already accepts; worth noting in XML docs.
3. **Public surface.** `ExtensionApi.GetCachedTypeHandler(Type)` is public. Add an overload rather
   than changing the signature.
4. **`GenericTypeWriteSettings` fallback.** The reader distinguishes `genericTypeSettings` because
   some rules must not apply to a generic-definition match. Check whether the serializer needs the
   same distinction before collapsing the resolution into one place.
5. **`ResolveTypeName`** also consults `settings` by type; decide whether it should move to the
   carried settings too (it uses an intentionally exact match, see its remarks).

## 7. Implementation notes (as built)

- `CachedTypeWriter` gained `public BaseTypeWriteSettings TypeSettings` plus a ctor parameter;
  `typeInfoHandling` is now resolved from it instead of from `HandlerType`.
- `CreateCachedTypeWriter(Type, BaseTypeWriteSettings = null)` resolves the settings once at the
  top and only writes to `typeWriterCache` when there is no local override.
- `overriddenTypeWriterCache` was removed again (see 5.1).
- New `BaseTypeWriteSettings.HasValueShapingOverrides`: distinguishes settings that change how a
  value is written from pure member metadata (`member_ignore`, `member_overrideName`). Only the
  former justify a member-local writer \u2014 otherwise every renamed member would get a duplicate
  writer for nothing.
- New `GetCachedTypeWriterForMember(Type, BaseTypeWriteSettings)` in `JsonSerializer.cs`, used by
  both `CreateTypedComplexItemHandler<T>` and `CreateTypedComplexItemHandler_ForNullableStruct<T>`.
- `WriterStrategies:516` now resolves the base64 flag from `elementHandler.TypeSettings` instead of
  from the literal `typeof(byte[])`.
- Behavior change: member-level `SetEnumAsString`, `SetWriteByteArrayAsBase64String`,
  `SetTypeInfoHandling` etc. previously had **no effect** on the serializer side. They now work.

## 6. Suggested commit split

0. remove `overriddenTypeWriterCache` again (recursion risk disproven)
1. carry settings through creation + store on `CachedTypeWriter` (neutral)
2. consume carried settings at all `Resolve*` sites (neutral)
3. member-local writers, uncached (behavioral)
4. tests
