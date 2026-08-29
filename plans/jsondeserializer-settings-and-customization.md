# Plan: JsonDeserializer settings and customization parity

## Goal

Bring `JsonDeserializer` to a comparable configuration depth as `JsonSerializer`, using the same
scope and precedence concepts where reading and writing are semantically symmetric:

- global settings;
- open-generic type settings;
- constructed/concrete type settings;
- member settings;
- container element/value settings;
- recursive subtree settings;
- prepared custom readers, including open-generic readers;
- dictionary-key customization where an inverse parser can be supplied.

The APIs should use serializer terminology where that is meaningful, but should not add settings
that only choose an output representation. The reader already accepts multiple input shapes in
several such cases.

## Current state and gaps

### Already available

- `ConfigureType<T>` and `ConfigureGenericType`.
- Per-member settings (`ConfigureMember<TMember>`), including ignore/name, data access, backing-field
  mode, reference resolution, proposed types, population, constructors/mappings and string cache.
- Context-local readers: `GetCachedTypeReader(type, settings)` / `CreateCachedTypeReader`.
- `CachedTypeReader.TypeSettings` carries the effective settings.
- Custom readers via `ICustomTypeReader<T>` or delegates, with `PreparationApi` and low-level
  `ExtensionApi`.
- Type mappings and multiple mapping options.
- Dictionary input already accepts JSON-object and key/value-pair-array forms.
- Type-info input already accepts inline objects and `$value` / `$values` envelopes.

### Missing or inconsistent

1. A member-local settings object replaces rather than merges onto the member type's own settings.
2. An exact constructed-generic entry replaces rather than merges onto its open-generic entry.
3. `ConfigureGenericType` does not validate null/non-definition/constructed arguments.
4. No runtime `ConfigureType(Type, Action<BaseTypeSettings>)` overload.
5. No `ConfigureElement<TElement>` for arrays, lists, enumerables or dictionary values.
6. No recursive subtree configuration.
7. Container readers call `GetCachedTypeReader(elementType)` directly, so element/local/recursive
   context cannot currently flow into elements.
8. Proposed runtime types are resolved lazily and currently use shared readers without preserving a
   member/element/recursive context.
9. No open-generic custom-reader definition equivalent to serializer open-generic writers.
10. Custom readers are powerful but low-level; there is no declarative object/array builder analogous
	to `PrepareObjectWriter`, `AddField`, `AddExistingFields`, `AddDynamicFields` and `AddArray`.
11. No inverse dictionary-key parser corresponding to a serializer key formatter.
12. The compiled-settings deep clone must be reconciled with identity-based recursive-context caches.

## Semantic mapping from serializer to deserializer

| Serializer concept | Deserializer counterpart |
|---|---|
| `DataSelection` | `DataAccess` + `BackingFieldMode` |
| Type-info handling/format | `ProposedTypeMode` / `SetProposedTypeHandling`; input shapes are accepted rather than selected |
| Reference writing | `ReferenceResolutionMode` / `SetReferenceResolution` |
| `ConfigureMember` | Existing `ConfigureMember` |
| `ConfigureElement` | Add equivalent element/value settings |
| `ConfigureRecursively` | Add restricted recursive read settings |
| Dictionary shape | No setting needed; accept object and pair-array forms |
| Dictionary key formatter | Optional inverse key parser API |
| Custom type writer | Existing custom type reader, extended to open generics and declarative builders |
| `AddExistingFields` | Read configured normal members, then allow additional/custom fields |
| `AddDynamicFields` | Capture or process otherwise-unmatched property names at runtime |

## Settled/recommended precedence

Use the same precedence rule as the serializer, highest first:

1. member or element settings;
2. exact concrete/constructed type settings;
3. open-generic type-definition settings;
4. inherited recursive context;
5. global settings.

A more specific scope overrides only the fields it explicitly configures. Broader settings remain
active for all unspecified fields. Member dictionaries merge by member name. Recursive settings
layer per field and remain ambient below the local scope.

Type mappings, constructors and custom readers are type-bound. A local mapping/constructor/reader
wins as a whole and must not be propagated to unrelated descendant or proposed types.

## Track A: settings-resolution parity

### A1. Establish explicit merge semantics

Add to `BaseTypeSettings`:

- `HasValueShapingOverrides` (or a reader-specific equivalent);
- `MergeOnto(BaseTypeSettings broader)`;
- one-level injection/cycle protection corresponding to serializer `isMerged` /
  `AsInjectedFromGeneralSettings`;
- a transferable subset for proposed runtime types.

Merge scalar settings per field. Merge `memberSettingsDict` by name. Define type-bound exclusions:

- do not transfer `constructor`, `collectionConstructor`, `customTypeReader` or concrete mappings to
  a different proposed runtime type;
- transfer read policies (`dataAccess`, `backingFieldMode`, reference/proposed-type policy,
  population policy, recursively scoped string-cache policy) where meaningful;
- transfer member rules to compatible proposed derived types, matching serializer behavior.

Preserve snapshot isolation: perform all exact/open-generic pre-merges after the settings map has
been deep-cloned, not on the mutable source objects.

### A2. Normalize type configuration APIs

- Add `Settings.ConfigureType(Type, Action<BaseTypeSettings>)`, rejecting generic definitions and
  interoperating with `ConfigureType<T>` entries.
- Validate `ConfigureGenericType` exactly like the serializer:
  - null -> `ArgumentNullException`;
  - non-generic or constructed generic -> `ArgumentException`;
  - null callback removes the entry.
- During `CompiledSettings` creation, pre-merge every explicitly configured constructed type onto
  its open-generic definition. The resulting object must be stable for reader cache use.
- Keep exact settings more specific than generic settings for forbidden-type exceptions, mappings,
  custom readers and constructors; document any security-sensitive exception explicitly.

### A3. Merge local member/mapping overrides with type settings

At the top of `CreateCachedTypeReader`:

- distinguish a genuine local override from resolved type settings;
- resolve exact/open-generic settings once;
- merge a local override onto the resolved type settings;
- retain the current rule that local variants do not enter the shared type cache.

Review mapped-type settings separately: settings supplied by `SetInstanceTypeMapping` and mapping
options are local to the mapped target and should merge onto that target's normal settings rather
than replace them.

Add recursion tests before changing this path. Existing member-settings recursion relies on a finite
settings tree; machine-injected broader settings need the same one-level cycle guard used by the
serializer.

## Track B: container element/value settings

### B1. API and storage

Add to `BaseTypeSettings`:

- `elementSettings`;
- `elementSettingsType`.

Expose `ConfigureElement<TElement>(Action<TypeSettings<TElement>>)` on `TypeSettings<T>` and
`GenericTypeSettings`.

Semantics should mirror the serializer:

- arrays/lists/sequences -> element type;
- dictionary JSON-object form -> value type;
- dictionary pair-array form -> key/value pair reader, while key and value readers still receive
  their own applicable context;
- mismatching open-generic element configurations are ignored for nonmatching constructions;
- closed-type mismatches throw during configuration.

### B2. Reader preparation integration

Centralize element lookup:

- `GetCachedTypeReaderForElement(elementType, containerSettings)`;
- `GetElementSettings(elementType, containerSettings)`.

Use it in every container path, including:

- generic and non-generic arrays;
- specialized numeric arrays/lists (disable a bulk fast path when element settings change observable
  behavior or reference/proposed-type handling);
- mutable generic collections;
- constructor-based generic/non-generic enumerables;
- dictionaries in object and pair-array forms;
- populate-existing collection paths.

No per-element settings checks should remain on the read hot path; select the proper reader/strategy
at preparation time.

## Track C: recursive read settings

### C1. Restricted API

Add `RecursiveReadSettings` and
`BaseTypeSettings.ConfigureRecursively(Action<RecursiveReadSettings>)`.

Recommended recursively allowed settings:

- `dataAccess`;
- `backingFieldMode`;
- `enableReferenceResolution`;
- `applyProposedTypes`;
- `populateAsMember`;
- string-cache policy for string members/elements (promote the current member-only naming to a
  scope-neutral internal field while keeping `SetUseStringCache` on member settings).

Excluded because they bind to one CLR type or construction path:

- `mappedType`, `multiOptionMappedTypes`;
- `constructor`, `collectionConstructor`;
- `customTypeReader`;
- member ignore/name metadata;
- explicit `memberSettingsDict` and `elementSettings`.

Global parser/security options (`strict`, whitelist/forbidden types, buffer sizes, exception policy,
uninitialized construction) remain global unless a separate, justified type-level feature is
requested. Recursive settings must never weaken whitelist or forbidden-type enforcement.

### C2. Ambient context and caches

Mirror the serializer design:

- ambient recursive context during reader preparation;
- nested recursive blocks layer onto inherited context, inner values winning;
- recursive settings apply to the declaring type/member/element itself and its descendants;
- intern layered contexts so equal `(outer, inner)` pairs have stable identity;
- add a contextual cache keyed by `(Type, RecursiveReadSettings)`;
- register contextual readers before initialization so self-referencing types terminate;
- keep genuine member/element-local overrides out of shared caches.

Because deserializer settings are deep-cloned, build/intern recursive contexts only from the cloned
compiled settings. Do not retain references to mutable source settings.

### C3. Deferred proposed-type context

`CachedTypeReader` resolves `$type` readers lazily and caches the last proposed reader. It must
capture the effective recursive/member/element context during preparation and use a context-aware
resolver at read time.

The resolver should:

- apply only the transferable subset to the proposed runtime type;
- merge with the proposed type's own exact/open-generic settings;
- preserve whitelist and forbidden-type checks;
- cache per prepared call site or contextual key;
- never transfer a custom reader, constructor, mapping or type-bound setting from the declared type.

Also review deferred readers used by unknown-object reading, mapping options and populate-existing
runtime-type paths.

## Track D: dictionary key customization

The serializer's formatter cannot be inverted automatically. Add an explicitly inverse API only:

- `ConfigureKey<TKey>(Func<string, TKey> parseKey)` as the baseline;
- optionally `TextSegment` / span-based forms to avoid allocations, following multi-target guards;
- bind and validate the parser at reader preparation time;
- apply only to dictionary JSON-object property names;
- pair-array keys continue through the normal `TKey` value reader;
- closed dictionary key mismatches throw; open-generic mismatches apply only to matching
  constructions.

Do not call this feature symmetric round-tripping unless both serializer formatter and deserializer
parser are configured by the caller.

## Track E: custom-reader parity

### E1. Open-generic custom readers

Add a definition model analogous to serializer `CustomTypeWriterDefinition<T>`:

- `CustomTypeReaderDefinition<T>` with a protected preparation method;
- `GenericTypeSettings.SetCustomTypeReader(Type readerDefinition)`;
- close definitions positionally with the constructed type's generic arguments;
- validate generic arity, target type and public parameterless construction;
- instantiate and prepare once per constructed type;
- exact constructed custom reader wins over the open-generic definition;
- derived types are not implicitly covered.

Existing `ICustomTypeReader<T>` and delegate APIs remain supported.

### E2. Preparation API parity

Keep `PrepareTypeReader<T>` and add configure-callback overloads matching serializer ergonomics:

- `PrepareTypeReader<T>(Action<TypeSettings<T>> configure)`;
- explicit `PrepareNonCustomTypeReader<T>` remains the escape hatch;
- prepared delegates must use the effective local/open-generic/recursive settings and remain
  isolated from the shared reader.

Fix the typo in `GetContructor<T>` by adding `GetConstructor<T>` and retaining the old method as a
compatibility-forwarder before considering deprecation.

### E3. Declarative object reader builder

Design `PrepareObjectReader<T>(Action<ObjectReaderBuilder<T>> build)` as a preparation-time API.
Recommended primitives:

- `AddField<TValue>(name, setter)` and overload with local settings;
- `AddObject` / `AddArray` convenience forms where they improve readability;
- `AddExistingFields()` to reuse the normal configured member pipeline;
- `AddDynamicFields` to process unmatched property names, or a dictionary-capture convenience;
- explicit unknown-field policy (`Skip`, `Throw`, callback), defaulting to current skip behavior;
- optional populate-existing behavior when a constructor/populate delegate exists.

Unlike writing, JSON field order is not guaranteed. The builder must compile a name lookup and read
an object loop; declarations must not imply input order. Duplicate-field behavior should follow the
normal reader contract and be documented.

`AddExistingFields` should reuse extracted normal member-reader creation logic, including:

- data access and backing-field matching;
- `JsonIgnore` / `JsonInclude` semantics;
- member name overrides and ignore settings;
- member-local/recursive settings;
- populate-existing and reference-path behavior.

### E4. Declarative array/value readers

Add prepared helpers where they add real parity over the existing low-level API:

- `PrepareValueReader<T>` for primitive/custom token shapes;
- `PrepareArrayReader<TCollection,TElement>` using a prepared element reader and constructor;
- raw reader remains available through `ExtensionApi`.

Avoid forcing a one-to-one mirror of writer methods when reading semantics differ. In particular,
there is no reader equivalent of pre-encoded output names or raw token emission.

## Track F: tests

Create focused files instead of extending the already large settings test class:

1. `JsonDeserializerElementSettingsTests`
   - arrays, lists, enumerable constructors, dictionaries, pair arrays;
   - member-local element settings;
   - open-generic matching/mismatching constructions;
   - reference/proposed-type and populate paths;
   - specialized numeric fast-path bypass when needed.

2. `JsonDeserializerRecursiveSettingsTests`
   - declaring type, member and element subtree roots;
   - depth, nested layering and local precedence;
   - self-referencing types and contextual-cache isolation/order;
   - proposed runtime types, mappings and unknown-object paths;
   - reference resolution and population;
   - no sibling/global leakage;
   - security settings cannot be weakened.

3. `JsonDeserializerOpenGenericSettingsTests`
   - argument validation;
   - exact-over-generic per-field/member merge;
   - recursive layering;
   - fixed member/element/key support and generic-dependent limitations;
   - constructors, mappings and custom-reader precedence;
   - removal/reconfiguration and snapshot isolation.

4. `JsonDeserializerCustomTypeReaderBuilderTests`
   - existing fields plus added fields;
   - dynamic/unmatched fields;
   - arbitrary property order and missing fields;
   - duplicate and unknown fields;
   - nested objects/arrays, nulls and populate-existing;
   - local settings, recursive settings, references and proposed types;
   - open-generic definitions and exact precedence.

All tests must use public APIs. Run focused suites after every phase and the full project suite before
completion.

## Track G: documentation

- Add `FeatureLoom.Core/Serialization/CUSTOM_TYPE_READERS.md` alongside the writer guide.
- Update `.github/skills/json-serialization.md` with reader precedence, element and recursive
  contexts, proposed-type transfer rules and custom-reader architecture.
- Document deliberate asymmetries:
  - reader accepts shapes instead of selecting dictionary/type-info output format;
  - key parsing requires an explicit inverse function;
  - security policy remains global/non-recursive;
  - field order is irrelevant when reading.

## Recommended implementation order

1. Baseline focused tests for current member/mapping/custom-reader behavior.
2. A1-A3: merge semantics, runtime type overload, generic validation/inheritance.
3. B1-B2: element settings across all container paths.
4. C1-C3: recursive settings, contextual cache, proposed-type propagation.
5. D: inverse dictionary-key parser.
6. E1-E2: open-generic custom readers and preparation overload parity.
7. E3-E4: declarative custom-reader builders.
8. Full edge/permutation test matrix.
9. Documentation and full-suite validation.

Step 1 completed with `JsonDeserializerSettingsFlowBaselineTests`: member-setting combinations,
member-local reader/mapping isolation, mapped-target isolation, prepared-reader reuse, compiled
snapshot isolation, open-generic reuse, finite recursive member settings and populate-existing are
covered. The mapped-target isolation test exposed and fixed a root fast-cache keying defect: the
last-reader cache must be keyed by the requested type, not the mapped reader's `ReaderType`.

Each phase should build and pass independently. Do not begin the declarative builder work until
settings flow, element propagation and recursive context behavior are stable, because the builder
must reuse those mechanisms rather than create a parallel settings pipeline.

## Confirmed decisions

1. `RecursiveReadSettings.SetUseStringCache` applies to all nested string members and elements.
2. Inverse dictionary-key parsing is included as a separate implementation phase.
3. The declarative object reader skips unknown fields by default, matching the normal deserializer.
4. Dynamic fields provide a full callback receiving the field name, read API and target, plus a
   dictionary-capture convenience overload.
5. Constructors and mappings cannot be configured recursively. They remain bound to an explicit
   target type or local member/element scope.

## Test quality requirement

Every implementation phase requires broad public-API coverage before proceeding to the next phase.
Tests must cover not only each setting independently, but also precedence and interaction matrices:

- global × open generic × exact type × member × element × recursive scopes;
- settings declared in different registration orders;
- shared readers prepared before and after contextual readers;
- self-referencing and mutually recursive types;
- repeated and distinct proposed runtime types within one context;
- custom readers combined with members, elements, recursion, mappings, constructors and references;
- null, empty, missing, duplicate, reordered, unknown and malformed fields/elements;
- create-new and populate-existing paths;
- JSON-object and key/value-pair dictionary shapes;
- matching and mismatching open-generic member, element and key configurations;
- snapshot isolation after mutable source settings are changed;
- all reference-resolution modes and applicable `$type`, `$value`, `$values`, `$id` and `$ref`
  combinations;
- whitelist and forbidden-type enforcement under local, recursive, mapped and proposed-type paths;
- optimized versus general reader paths, ensuring settings cannot be bypassed by a fast path;
- multi-target behavior where conditional APIs or runtime behavior differ.

When a test reveals an existing defect or an undocumented behavior, preserve it as a focused
regression test. Do not weaken assertions merely to match the implementation; first determine and
document the intended contract.
