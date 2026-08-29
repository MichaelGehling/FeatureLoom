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

   **Amended by the settings merge (step 6).** Merging a type's general settings into a local
   override re-introduces exactly the loop this item declared impossible: a self referencing type
   whose general settings configure the recursive member would re-inject that member setting at
   every nesting level, so the settings tree would no longer be finite. The merge is therefore
   limited to one level via the `isMerged` flag (`MergeOnto` / `AsInjectedFromGeneralSettings`).
   The argument above still holds for user authored configuration; it does **not** hold for
   machine injected configuration. Covered by
   `MemberOverride_OnSelfReferencingType_Terminates`.
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

### Step 6 — polymorphy and settings merge — DONE

Follow-up work after a defect was found: custom-writer fields declared as `object` or a base type
were written with the declared type's writer, producing `{}` or hiding derived members.

- `CreatePolymorphicValueWriter<TValue>(declaredWriter, contextSettings)` in `JsonSerializer.cs`
  selects the runtime type's writer when the value deviates. Skipped entirely for value types and
  sealed types. Wired into `AddField`, `AddArray`, `PrepareArrayWriter`, `PrepareTypeWriter`.
- `CreateDeviatingWriterResolver(contextSettings)` returns `GetCachedTypeWriter` when there is
  nothing to transfer (no allocation, shared cache), otherwise a per-call-site dictionary of
  context-local writers.
- `BaseTypeWriteSettings.GetTransferableSubset()` defines what follows a deviating runtime type:
  policy fields and `memberSettingsDict` do (a derived type inherits the base type's members);
  `customTypeName` and `customTypeWriterCreator` do not (bound to the declared type).
- `BaseTypeWriteSettings.MergeOnto(generalSettings)`: a local override no longer *replaces* the
  type's general settings but is merged onto them. This also fixed a pre-existing bug — a plain
  `ConfigureMember` previously discarded the type's own `SetCustomTypeName`, custom writer and
  member configuration. Limited to one level, see 5.1.
- `NoRefTypesIncludingRuntimeTypes` replaces direct `CachedTypeWriter.NoRefTypes` reads at the
  custom-writer and built-in member call sites: `NoRefTypes` describes the declared type only, so
  skipping ref bookkeeping for a type that can deviate at runtime was unsound. Note: this is a
  reasoned safety narrowing (it can only turn a `true` into `false`); no test was found that
  reproduces the underestimation.
- New `configure` overloads on `AddField`, `AddArray`, `PrepareArrayWriter` for field-local
  deviating settings.
- Documented in `CUSTOM_TYPE_WRITERS.md` ("How context-local settings combine", "Polymorphic values
  and settings") and in `.github/skills/json-serialization.md`.

## 6. Recursive write settings — IMPLEMENTED

### Goal

Allow a type configuration to define write settings for its complete object subtree without
materializing member settings during configuration:

```csharp
settings.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs =>
{
	rs.SetTypeInfoHandling(TypeInfoHandling.AddAllTypeInfo);
	rs.SetEnumAsString(true);
}));
```

### Settled semantics

- Recursive settings apply to the declaring type itself and all nested member values and container
  elements.
- Local settings always win, but recursive settings remain active for every option the local scope
  does not override. Precedence, highest first:
  member/element override → type configuration → recursive context → global settings.
- A nested `ConfigureRecursively` is layered onto the inherited recursive context. Its explicitly
  configured values win while unspecified values continue to come from the outer context.
- Recursive settings follow values whose runtime type deviates from their declared type.
- Dictionary keys are excluded; dictionary values are included through normal element/value
  propagation.

Settings allowed in `RecursiveWriteSettings`:

- `dataSelection`
- `typeInfoHandling`
- `typeInfoFormat`
- `arrayValueFieldName`
- `enumAsString`
- `writeByteArrayAsBase64String`
- `treatEnumerablesAsCollections`
- `dictionaryShape`

Excluded because they are bound to one particular type or member scope:

- `customTypeName`, `customTypeWriterCreator`
- `keyFormatter`
- `member_ignore`, `member_overrideName`
- `elementSettings`, `memberSettingsDict`

### Storage and preparation-time propagation

- Add `RecursiveWriteSettings` as a restricted settings builder containing only the allowed
  nullable policy fields.
- Add `BaseTypeWriteSettings.ConfigureRecursively(Action<RecursiveWriteSettings>)` and store one
  recursive settings instance on the type settings. Do not create or modify member settings during
  configuration.
- Maintain an ambient recursive context while type writers are prepared. Entering
  `CreateCachedTypeWriter` layers the current type's recursive settings onto the inherited context,
  applies the result below the local settings and restores the previous context in `finally`.
  This naturally forms a preparation-time stack.

### Context identity, layering and recursion-safe caching

Layered contexts must have stable identity: intern each `(outerContext, innerSettings)` combination
so repeated layering returns the same effective `RecursiveWriteSettings` instance.

Add a context-specific cache keyed by `(Type, RecursiveWriteSettings)`. Register the writer before
creating its handler, as with `typeWriterCache`, so self-referencing types resolve the in-progress
writer instead of recursively constructing another one. Writers with genuine member/element-local
overrides remain uncached as today.

### Deferred runtime-type creation

`CreateDeviatingWriterResolver` may create writers lazily during serialization, after the ambient
preparation stack has unwound. It must capture the effective recursive context when the resolver is
prepared and restore that context around deferred writer creation. Review all other runtime-type
writer paths for the same requirement.

### Implementation steps

1. [x] Add `RecursiveWriteSettings`, `ConfigureRecursively` and interned context layering.
2. [x] Add the ambient preparation context and context-specific writer cache.
3. [x] Merge recursive defaults into effective type settings while preserving the settled precedence.
4. [x] Capture recursive context in lazy/deviating writer resolvers.
5. [x] Add tests for declaring-type application, depth propagation, local precedence, nested layering,
   self-referencing types, sibling isolation, members, container values, custom writers and
   deviating runtime types.
6. [x] Update `CUSTOM_TYPE_WRITERS.md` and `.github/skills/json-serialization.md`.

Extended edge coverage in `JsonSerializerRecursiveSettingsTests` now includes every recursively
configurable policy, generic type-definition configuration, conflicting nested contexts, member and
element precedence, context-specific cache ordering/isolation, circular references, null and empty
containers, dictionary-key exclusion, and composition with `AddField`, `AddArray`, `AddObject`,
`AddDynamicFields` and `AddExistingFields`.

### Open generic settings inheritance — DONE

- `ConfigureGenericType` now rejects null, non-generic and already constructed types.
- In `CompiledSettings`, an explicitly configured constructed type is pre-merged onto the settings
  of its generic type definition. Exact values and member entries win, while unspecified generic
  policies, member settings and layered recursive settings remain active.
- `JsonSerializerOpenGenericSettingsTests` covers policies, fixed members, generic-dependent member
  rejection, construction-specific elements and keys, reconfiguration/removal, validation,
  inheritance, precedence and recursive layering.

## 7. Suggested commit split

0. remove `overriddenTypeWriterCache` again (recursion risk disproven)
1. carry settings through creation + store on `CachedTypeWriter` (neutral)
2. consume carried settings at all `Resolve*` sites (neutral)
3. member-local writers, uncached (behavioral)
4. tests
