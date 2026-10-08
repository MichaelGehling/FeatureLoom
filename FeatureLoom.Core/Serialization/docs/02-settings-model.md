# 02 Settings model

## Why

Configuration is resolved **once**, when an instance is created, not on every call. The per-value
path only executes precompiled readers/writers, so rich configuration costs nothing at runtime.

## Lifecycle

```
Settings (mutable)  ──new JsonSerializer(settings)──►  CompiledSettings (immutable snapshot)
															  │
										first use of a type   ▼
													 cached reader/writer per type
```

1. **Build** a `JsonSerializer.Settings` / `JsonDeserializer.Settings` (object initializer or
   `Action<Settings>` callback).
2. **Construct** the instance. The settings are compiled into an internal snapshot.
   Later changes to the `Settings` object have **no effect** on existing instances.
3. **First use of a type** creates its reader/writer from the snapshot and caches it in the
   instance. All later values of that type reuse it.

Consequences:

- To change configuration, create a new instance. A `Settings` object can be reused as a template
  for several instances.
- Instances with identical settings are independent – each has its own caches and buffers.

## Two settings classes

Serializer and deserializer are configured separately, with parallel APIs (`ConfigureType`,
`ConfigureMember`, `ConfigureRecursively`, …). A setup that must round-trip typically configures
both with matching member names, type names and mappings.

## Layering

An option is resolved from the most specific layer that sets it:

| Priority | Layer | Example |
|---|---|---|
| 1 (highest) | context-local override (member/element/key, recursive settings) | `ConfigureMember(x => x.Notes, …)` |
| 2 | constructed type | `ConfigureType<List<Order>>(…)` |
| 3 | generic type definition | `ConfigureGenericType(typeof(List<>), …)` |
| 4 | type-owned configuration (opt-in) | `[JsonTypeConfiguration]` method |
| 5 (lowest) | global settings | `s.dataSelection = …` |

Layers are merged **per option**: a member override that only changes the name keeps all other
options of its type. Details and examples: [03 Type configuration](03-type-configuration.md).

## Thread safety

- `Settings` objects are plain mutable objects – do not modify them concurrently.
- Compiled instances can be shared across threads. Calls on the same instance are serialized by
  an internal lock, because the instance owns its buffers.
- For parallel throughput use one instance per thread or a small pool
  (see [08 Performance](08-performance.md#thread-safety)).
- A deserializer bound to a data source (see [09](09-continuous-deserialization.md)) serves one
  source at a time.

## Global options overview

### Serializer

| Option | Default | Page |
|---|---|---|
| `dataSelection` | `PublicAndPrivateFields_CleanBackingFields` | [03](03-type-configuration.md) |
| `typeInfoHandling` / `typeInfoFormat` / `typeNameFormat` | `AddDeviatingTypeInfo` / `InlineForObjects` / `Simplified` | [06](06-polymorphism.md) |
| `referenceCheck` / `referenceFormat` | `NoRefCheck` / `JsonPath` | [07](07-references.md) |
| `enumAsString` | `false` | – |
| `indent`, `indentationFactor`, `maxIndentationDepth` | `false`, `2`, `50` | – |
| `writeByteArrayAsBase64String` | `true` | – |
| `treatEnumerablesAsCollections` | `true` | – |
| `writeBufferChunkSize`, `tempBufferSize` | 64 KB, 8 KB | [08](08-performance.md) |
| `typeSelfConfigurationMode` | `IgnoreButWarn` | [03](03-type-configuration.md) |

### Deserializer

| Option | Default | Page |
|---|---|---|
| `dataAccess`, `backingFieldMode` | `PublicAndPrivateFields`, `TryBothNames` | [03](03-type-configuration.md) |
| `proposedTypeMode`, `typeWhitelistMode` | `CheckWhereReasonable`, `Disabled` | [06](06-polymorphism.md) |
| `referenceResolutionMode` | `DisabledByDefault` | [07](07-references.md) |
| `unknownFieldPolicy` | `Skip` | – |
| `strict` | `false` (e.g. accepts numbers in strings) | – |
| `rethrowExceptions`, `logCatchedExceptions` | `false`, `true` | – |
| `populateExistingMembers` | `true` | – |
| `allowUninitializedObjectCreation` | `false` | – |
| `castObjectArrayToCommonTypeArray` | `true` | – |
| `useStringCache`, `stringCacheBitSize`, `stringCacheMaxLength` | `true`, `12` (4096 entries), `128` | [03](03-type-configuration.md), [08](08-performance.md) |
| `initialBufferSize` | 128 KB | [08](08-performance.md), [09](09-continuous-deserialization.md) |
| `typeSelfConfigurationMode` | `IgnoreButWarn` | [03](03-type-configuration.md) |

## Configuration tree

Global settings are the root. Everything below is reached through `Configure…` callbacks, and the
same settings classes are reused at every nesting level:

```
Settings
├─ global options (fields, see below)
├─ ConfigureType<T>(TypeSettings<T>)            ─┐
├─ ConfigureType(Type, BaseTypeSettings)         │  per-type layer
├─ ConfigureGenericType(Type, GenericTypeSettings)┘
│    ├─ Set…(…)                     type-level overrides
│    ├─ ConfigureRecursively(…)     policies for the whole subtree below this type
│    ├─ ConfigureMember<TM>(name, MemberSettings<TM>)   ─┐ context-local:
│    ├─ ConfigureElement<TE>(TypeSettings<TE>)           │ only for this member/element/key,
│    └─ ConfigureKey / ConfigureObjectKey                ┘ may nest further (members of members …)
└─ type-name, forbidden/allowed-type lists (deserializer)
```

`MemberSettings<T>` derives from `TypeSettings<T>`, so a member can be configured with everything a
type can (custom handler, mappings, own members/elements), plus member-only options (ignore, name).
Element settings are full `TypeSettings<TElement>`.

## Per-type and nested options – serializer

| Option | Global | Type | Generic def. | Member | Element | Recursive |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `SetDataSelection` / `dataSelection` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetTypeInfoHandling` / `SetTypeInfoFormat` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetArrayValueFieldName` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetEnumAsString` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetWriteByteArrayAsBase64String` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetTreatEnumerablesAsCollections` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetDictionaryShape` (`Auto`, `KeyValuePairArray`) | – | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetCustomTypeName` | – | ✅ | ❌ | ✅ | ✅ | – |
| `SetCustomTypeWriter(…)` | – | ✅ | ✅ (open generic writer type) | ✅ | ✅ | – |
| `ConfigureMember` / `ConfigureElement` / `ConfigureKey` | – | ✅ | ✅ | ✅ | ✅ | – |
| `SetIgnore`, `OverrideName` | – | – | – | ✅ | – | – |
| `OverrideMemberNames(Func<string,string>)` | – | ✅ | ✅ | ✅ | ✅ | ✅ |
| `referenceCheck`

`ConfigureKey<TKey>` accepts `Func<TKey, string>`, `Func<TKey, TextSegment>` or (NET 5+)
`KeyToSpan<TKey>` to format dictionary keys.

## Per-type and nested options – deserializer

| Option | Global | Type | Generic def. | Member | Element | Recursive |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `SetDataAccess` / `dataAccess` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetBackingFieldMode` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetReferenceResolution` (vs. `referenceResolutionMode`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetProposedTypeHandling` (vs. `proposedTypeMode`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetPopulateAsMember` (vs. `populateExistingMembers`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetCastObjectArrayToCommonTypeArray` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetUnknownFieldPolicy` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `SetUseStringCache` (vs. `useStringCache`) | ✅ | ✅¹ | ✅¹ | ✅ | ✅ | ✅ |
| `SetAllowUninitializedObjectCreation` | ✅ | ✅ | ✅ | ✅ | ✅ | – |
| `SetCustomTypeReader(…)` | – | ✅ | ✅ (open generic reader type) | ✅ | ✅ | – |
| `AddConstructor`, `AddCollectionConstructor`, `AddUntypedCollectionConstructor` | – | ✅ | – | ✅ | ✅ | – |
| `SetInstanceTypeMapping` | – | ✅ | ✅ | ✅ | ✅ | – |
| `AddInstanceTypeMappingOption` / `…ValueOption`, `AddDefaultStringValueMappings` | – | ✅ | – | ✅ | ✅ | – |
| `ConfigureMember` / `ConfigureElement` / `ConfigureObjectKey` | – | ✅ | ✅ | ✅ | ✅ | – |
| `SetIgnore`, `OverrideName` | – | – | – | ✅ | – | – |
| `OverrideMemberNames(Func<string,string>)` | – | ✅ | ✅ | ✅ | ✅ | ✅ |
| type names

`ConfigureObjectKey<TKey>(Func<BufferSegment, TKey>)` parses dictionary keys.

¹ `SetUseStringCache` on a string member/element applies to that value. On any other scope it applies
only to the string members/elements read *directly* by that type (e.g. `List<string>` or a class's
string fields), not to nested types; use `ConfigureRecursively` for a whole subtree.
Precedence: member/element > type > recursive > global. The shared cache is allocated only if any
scope enables it.

## Where each option is explained

| Topic | Page |
|---|---|
| data selection/access, backing fields, members/elements/keys, recursive, string cache | [03](03-type-configuration.md) |
| custom writers/readers, constructors | [04](04-custom-writers-readers.md) |
| instance type mappings, string value mappings | [05](05-type-mappings.md) |
| type info, type names, proposed/forbidden/allowed types | [06](06-polymorphism.md) |
| references | [07](07-references.md) |
| buffers, cache sizes | [08](08-performance.md) |

## Comparison

| | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Configuration object | per instance, compiled on construction | `JsonSerializerOptions`, frozen on first use | `JsonSerializerSettings`, read per call |
| Change after first use | new instance | ⚠ throws (`InvalidOperationException`) | allowed, but contract cache may be stale |
| Per-member config without attributes | ✅ | 🟡 contract modifiers (`IJsonTypeInfoResolver`) | 🟡 custom `ContractResolver` |
| Per-option merging across layers | ✅ | ❌ | ❌ |
