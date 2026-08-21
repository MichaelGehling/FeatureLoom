# Custom Type Handler API — Design Rationale

Part of the [Custom Type Handler API redesign](../custom-typehandler-api-redesign.md).

Why the redesign looks the way it does. Historical record — the decisions here are settled;
change them only with a reason that was not considered below.

## Goal

Design the custom reader/writer API from scratch, optimizing for usability first and
performance second. Full break with the current API is accepted. Symmetry between the
reader and writer side is a primary requirement, so that learning one teaches the other.

## What the old design got right (worth keeping)

Kept because it is load-bearing, not because it exists:

- **Two-phase creation.** Phase 1 runs once per type and returns the phase-2 delegate.
  Both sides already do this. It is the reason a custom handler can be fast at all, and it
  stays the backbone of the new design.
- **Curated API objects over raw internals.** The reader side's `PreparationApi` /
  `ExtensionApi` split is the right idea; the writer side should get the same treatment
  instead of exposing `IWriter`.
- **Per-type registration through `TypeSettings<T>`** (reader side). Composes with the rest
  of the type configuration; the writer's global creator list does not.
- **`CachedTypeReader`/`CachedTypeWriter` carrying their own `TypeSettings`** — already
  refactored, and the new API builds on it.

## What must go

- **`api.Writer` / `IWriter` exposure.** ~80 members, no guidance, trivially misused.
- **`AddCustomTypeHandlerCreator` / `ITypeHandlerCreator` as the public registration surface.**
  The *capabilities* it provides (predicate and assignable matching) are kept and re-exposed
  through the new API — see [API shape](02-api-shape.md#registration-one-concept-two-matching-strategies).
  Only the raw creator-interface form goes.
- **`JsonDataTypeCategory` as a user-facing argument** — see below.

## What `JsonDataTypeCategory` actually encoded (important)

It was not decoration. `ExtensionApi.SetItemHandler` mapped it onto two orthogonal decisions:

1. **Which wrapper** to build around the user's body writer:
   - `CreatePrimitiveItemWriter` — optional type-info object only.
   - `CreateArrayItemWriter` — item-info/`$id`/`$ref` handling, `[` `]`, type-info,
	 plus the `$values` wrapping when the array itself needs an `$id`.
   - `CreateObjectItemWriter` — item-info/`$id`/`$ref` handling, `{` `}`, type-info.
2. **Whether children may contain references** (`SetItemWriter(..., bool)`), i.e. the
   `_WithoutRefChildren` variants, plus `ForceNoRefTypes()` for primitives.

So the enum bundled *output shape* with a *reference-tracking hint*. Removing it meant both
facts had to come from somewhere else. The fluent design derives (1) from **which builder the
user asks for** and (2) from **what the user declares/does inside that builder** — which is
exactly why the fluent form is better than an enum: the information becomes implicit in the
call the user was already making.

Outcome: the enum was first bypassed, then deleted entirely along with `ExtensionApi`.

## Resolved decisions

**1. Field order — reuse built-in lookup, document the ordering benefit.**
The object reader builder reuses the built-in field-lookup machinery
(`itemFieldWritersIndexLookup` plus the `expectedFieldIndex` fast path in
`TypeReaderCreation.cs`), so out-of-order, unknown and missing fields behave exactly as with
generated readers. Declaration order is a *performance hint*, never a correctness
requirement: the built-in path first probes the next expected index and only falls back to a
dictionary lookup on a miss. Every `Field(...)` method gets an XML comment stating that
declaring fields in the order they appear in the JSON is faster.

**2. Populate support — keep the concept, make it explicit.**
The mechanism already exists on the reader side and works: `CustomTypeReader<T>` derives
`CanPopulateExistingValue` from *which* delegate shape was supplied
(`Func<ExtensionApi, T>` = read-only, `Func<ExtensionApi, T, T>` = can populate). The
problem is purely that it is implicit — three overloads named `SetCustomTypeReader`
differing only in delegate shape, with the populate capability as an invisible side effect.
The new builders make it visible in the method name and in the fluent chain
(`ReadOnly()` vs `Populatable()` / `.OnPopulate(...)`), so the user chooses the capability
rather than deducing it.

**3. Ref-path derivation — not a user decision.**
The builder derives `childrenMustWriteRefPath` from the declared fields, exactly as the
built-in complex handler derives `allFieldsNoRefs`. This removes the existing
`// TODO: CustomTypes must be enabled to configure if children must write ref paths`
hardcoded `true` in `CreateCustomTypeReader<T>`, which currently pessimizes every custom
reader.

**4. Open generic types — POSTPONED, then RESOLVED.**
`ConfigureGenericType(typeof(IList<>), ...)` and `GenericTypeSettings` already existed and are
used for the built-in collection mappings, so the groundwork was there. The gap was that
`SetCustomTypeReader` lives on the typed `TypeSettings<T>` while `GenericTypeSettings` is
untyped and cannot carry a custom handler.

Originally deferred on the assumption that an open generic would need a name-based field API,
which would have compromised the closed-type design. **That assumption was wrong.** Declaring
the writer as a generic class (`WrapperWriter<T> : CustomTypeWriterDefinition<Wrapper<T>>`)
puts the type parameters back in scope, so the existing strongly typed builder applies
unchanged. The closed-type builder signatures did not have to change, and no second builder
entry point was introduced.

The reflection path this decision wanted to preserve (`GenericTypeHandlerCreator`) was
superseded and has since been deleted; `OpenGenericTypeWriterCreator` replaces it.
See [writer implementation](03-writer-implementation.md#open-generic-custom-writers).

**5. Accessor form: `Func<T, TField>`, not `Expression<Func<T, TField>>`.**
Considered using expressions so plain member access could be detected and routed through the
existing `MemberInfo` path. Rejected as the *primary* form:

- It only pays off for the trivial `p => p.Name` shape. Anything computed
  (`p => p.Name.ToUpper()`, a dictionary lookup, a unit conversion) falls back anyway — and
  computed values are a main reason to write a custom handler at all.
- `FieldIf` and derived values have no `MemberInfo` to bind to.
- Expression trees in the public signature leak into every call site and hurt the
  "easy to use" goal that ranks first.

Note: an `Expression` overload can be added later as an opt-in fast path without changing the
`Func` signatures. Not doing it now.

## Level model

Each level is reachable without rewriting the one below it.

- **L0 raw** — `PrepareRawWriter`/`PrepareRawReader`: direct token access, user is fully
  responsible. Escape hatch, documented as such.
- **L1 value** — map a type to/from a single JSON value. One line.
- **L2 object/array** — the fluent builders.
- **L3 settings-driven** — reuse the existing fluent `TypeSettings<T>`/`MemberSettings<T>`
  to adjust the *generated* handler (rename, ignore, per-member settings) without writing a
  custom handler at all. Mostly exists; the plan is to make it the documented top level and
  ensure it composes with L2.
