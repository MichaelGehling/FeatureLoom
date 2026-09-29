# Feature matrix

FeatureLoom JSON compared to System.Text.Json (STJ, .NET 8+) and Newtonsoft.Json (13.x).

Legend: ✅ built-in · 🟡 possible with extra code or limitations · ❌ not available · ⚠ competitor limitation that FeatureLoom solves

> Comparisons describe the documented public API of the competitors. Where a competitor feature
> depends on the .NET version, the newest behavior is assumed.

## Data selection & round-tripping

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Private fields round-trip without annotations | ✅ default | ❌ | 🟡 custom `ContractResolver` |
| Auto-property backing fields under clean name | ✅ | ❌ | ❌ |
| Public-properties-only mode (competitor-compatible) | ✅ `PublicFieldsAndProperties` | ✅ default | ✅ default |
| Populate existing member instances | ✅ default | 🟡 `JsonObjectCreationHandling.Populate` (.NET 8) | ✅ `ObjectCreationHandling.Auto` |
| Uninitialized object creation (no ctor needed) | ✅ opt-in, per type | ❌ | ❌ |
| `[JsonIgnore]` / `[JsonInclude]` attributes | ✅ same names (`FeatureLoom.Serialization`) | ✅ | 🟡 `[JsonIgnore]` / `[JsonProperty]` |

For easy migration, FeatureLoom supports the two most common attributes under the familiar names.
They are FeatureLoom's own types, so a model migrated from STJ needs its `using` switched to
`FeatureLoom.Serialization`. `[JsonInclude]` opts in private members for the public-only
`PublicFieldsAndProperties` mode.

⚠ **Default data loss.** STJ and Newtonsoft only write public properties by default. An object whose
state lives in private fields is silently lost on round-trip. FeatureLoom writes full state by
default and offers the public-only mode as an explicit choice.

## Configuration

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Configure types without attributes | ✅ `ConfigureType<T>` | 🟡 contract modifiers (`IJsonTypeInfoResolver`) | 🟡 custom `ContractResolver` |
| Configure types you don't own | ✅ | 🟡 contract modifiers | 🟡 `ContractResolver` |
| Per-member rename / ignore from outside | ✅ `ConfigureMember` | 🟡 contract modifiers | 🟡 `ContractResolver` |
| Per-element settings (items of a collection) | ✅ `ConfigureElement` | ❌ | 🟡 `ItemConverterType` (attribute) |
| Dictionary key formatter per key type | ✅ `ConfigureKey` | 🟡 converter with `WriteAsPropertyName` | 🟡 converter |
| Open generic type configuration | ✅ `ConfigureGenericType` | 🟡 converter factory | 🟡 converter with `CanConvert` |
| Constructed generic layered onto open generic | ✅ per-option merge | ❌ | ❌ |
| Policies applied to a whole subtree | ✅ `ConfigureRecursively` | ❌ options are global | ❌ settings are global |
| Configuration owned by the type itself | ✅ `[JsonTypeConfiguration]`, opt-in | 🟡 attributes only | 🟡 attributes only |
| Different settings for the same type in different contexts | ✅ member / element / context-local | ❌ second options instance | ❌ second serializer |

⚠ **Global-only options.** In STJ and Newtonsoft a setting either applies to the whole serializer or
has to be attached to the type by attribute. "Write enums as strings, but only inside this member"
needs a second options instance or a marker type. FeatureLoom resolves settings in layers
(global → generic → type → member/element → local), merged per option.

⚠ **Attribute lock-in.** Attributes couple the model to one serializer and cannot be applied to
third-party types. FeatureLoom's configuration lives in settings by default. A type *may* own its
configuration, but only when the serializer opts in.

## Custom writers & readers

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Write-only or read-only custom handler | ✅ | ❌ must implement both | ❌ must implement both (`CanRead`/`CanWrite` workaround) |
| Preparation phase (encode names, resolve nested writers once) | ✅ | 🟡 manual `JsonEncodedText` caching | ❌ |
| Declarative object/array builders (serializer owns braces/commas/nulls) | ✅ | ❌ | ❌ |
| Delegate to another type's writer with local settings | ✅ `PrepareTypeWriter(configure)` | 🟡 new `JsonSerializerOptions` | 🟡 new `JsonSerializer` |
| Extend default members instead of replacing | ✅ `AddExistingFields()` | ❌ | ❌ |
| Dynamic properties next to declared ones | ✅ `AddDynamicFields` | 🟡 `[JsonExtensionData]` | 🟡 `[JsonExtensionData]` |
| `$type` envelope kept for custom writers | ✅ automatic | ❌ | ❌ |
| Handler for derived types / by predicate | ✅ one argument | 🟡 converter factory | 🟡 `CanConvert` (order-dependent) |
| Open generic handler | ✅ `SetCustomTypeWriter(typeof(W<>))` | 🟡 factory + `MakeGenericType` | 🟡 reflective body |

⚠ **Hand-written structure.** In STJ and Newtonsoft converters, the converter writes
`WriteStartObject`/`WriteEndObject` itself, so an early return or an exception leaves broken JSON, and
null checks for nested values have to be written by hand. FeatureLoom's builders own the structure.

⚠ **Per-call work.** STJ and Newtonsoft converters encode property names and resolve nested
converters on every call unless you cache them yourself. FeatureLoom does this once, in the
preparation phase.

## Polymorphism & type safety

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Type info only where runtime type deviates | ✅ default | 🟡 only for registered derived types | 🟡 `TypeNameHandling.Auto` |
| No registration of derived types needed | ✅ | ❌ `[JsonDerivedType]` per subtype | ✅ |
| Custom type names | ✅ `SetCustomTypeName` | ✅ discriminator | 🟡 `ISerializationBinder` |
| Infer concrete type from field names | ✅ `AddInstanceTypeMappingOption` | ❌ | ❌ |
| Infer from a discriminator field value (any position) | ✅ field checkers | 🟡 discriminator must be first (before .NET 9) | 🟡 custom converter |
| Infer from the whole value (string, number, array…) | ✅ value mappings | ❌ | ❌ |
| Type whitelist / forbidden types | ✅ built-in | n/a (no arbitrary types) | 🟡 custom `ISerializationBinder` |

⚠ **Closed polymorphism in STJ.** Every derived type has to be announced on the base type, which is
impossible for types from other assemblies and breaks open extension.

⚠ **Unsafe polymorphism in Newtonsoft.** `TypeNameHandling` other than `None` enables arbitrary type
instantiation from payload data, a well-known deserialization attack vector, unless you write a
binder. FeatureLoom applies proposed types only where reasonable by default and has forbidden-type
and whitelist policies built in.

## References

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Preserve shared references | ✅ `AlwaysReplaceByRef` | ✅ `ReferenceHandler.Preserve` | ✅ `PreserveReferencesHandling` |
| Only break cycles, keep duplicates | ✅ `OnLoopReplaceByRef` | ❌ | ❌ |
| Replace cycles with `null` | ✅ `OnLoopReplaceByNull` | ✅ `IgnoreCycles` | ✅ `ReferenceLoopHandling.Ignore` (omits member) |
| JSONPath refs without `$id` on every object | ✅ default format | ❌ | ❌ |
| STJ/Newtonsoft-compatible `$id`/`$ref` | ✅ `IdBased` | ✅ | ✅ |
| Reference resolution enabled per type | ✅ | ❌ global | 🟡 attribute |

⚠ **Noise on every object.**
referenced. JSONPath refs only appear where a reference actually exists.

## Performance-related

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| UTF-8 end to end | ✅ | ✅ | ❌ UTF-16 |
| Per-type compiled handlers | ✅ | ✅ | ✅ |
| Per-member configuration baked in (no per-value checks) | ✅ | 🟡 | ❌ |
| String deduplication cache | ✅ fixed-size, on by default | ❌ | ❌ |
| String cache configurable per type / member / subtree | ✅ | ❌ | ❌ |
| .NET Framework 4.8 support | ✅ | 🟡 via NuGet package | ✅ |

⚠ **One allocation per string value.** STJ and Newtonsoft allocate a new `string` for every string
value read, even if the same `"EUR"` appears 100,000 times. Deduplicating afterwards (e.g. `string.Intern`)
costs the allocation anyway and pins the strings forever. FeatureLoom looks up the UTF-8 bytes in a
fixed-size cache *before* creating the string, so repeated values cost no allocation, and the
cache memory is bounded. It can be switched off for members holding unique values (ids, free
text) where caching would only cost lookups.

**Teaser: preliminary results** (ComplexObject with many types and structures, .NET 10, relative to STJ, lower is better):

| ComplexObject | Time | Allocation |
|---|---:|---:|
| Serialize | **0.57×** (~1.7× faster) | **0.15×** single / **0.77×** array |
| Deserialize | **0.55×** (~1.8× faster) | **0.42×** single / **0.50×** array |

With the string cache enabled only for recurring fields, a mixed string dataset allocates
**0.37×** of STJ and **0.06×** of Newtonsoft. STJ source generation gains only 2-6 % in these
runs. FeatureLoom also has weak spots, such as long Latin-1 or heavily escaped strings. Details,
methodology and all numbers are on [08 Performance](08-performance.md#benchmark-results).

## Dynamic / untyped JSON

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Deserialize unknown JSON to plain .NET objects | ✅ `object` → `Dictionary<string, object>` / arrays / primitives | 🟡 `JsonElement` / `JsonNode` | 🟡 `JObject` / `JArray` |
| Keep parts as raw, lazily processed JSON | ✅ `JsonFragment` (e.g. `Dictionary<string, JsonFragment>`) | ✅ `JsonElement` | 🟡 `JToken` / `JRaw` |
| Map to a different shape in configuration instead of via a DOM | ✅ per-member mapping from source / to target type | ❌ | ❌ |
| Mutable DOM with query API | ❌ | ✅ `JsonNode` | ✅ LINQ to JSON |

⚠ **The DOM detour.** In STJ and Newtonsoft, a JSON shape that does not match the model is usually
handled by reading into a DOM (`JsonNode`, `JObject`) and copying values over by hand, or by building
a DOM before writing. That costs a full intermediate tree and hand-written glue. FeatureLoom's
per-member configuration maps directly: on writing, a member can be written from a different source
type; on reading, into a different target type, with `JsonFragment` for the parts that
should stay raw. The shape transformation is part of the compiled reader/writer, so no
intermediate tree is built.

## Continuous deserialization

| Feature | FeatureLoom | STJ | Newtonsoft |
|---|---|---|---|
| Multiple top-level values from one source | ✅ `IsAnyDataLeft` + `TryDeserialize` loop | 🟡 STJ 9+: `JsonReaderOptions.AllowMultipleValues` (low-level reader) or `DeserializeAsyncEnumerable<T>(stream, topLevelValues: true)`; before 9 only top-level arrays | ✅ `JsonTextReader.SupportMultipleContent` |
| NDJSON | ✅ out of the box | 🟡 STJ 9+ via `topLevelValues: true`; older versions need manual line splitting | ✅ with `SupportMultipleContent` |
| Resync after a broken record | ✅ `SkipBufferUntil(delimiter)` | ❌ reader state invalid after an error | ❌ reader state invalid after an error |
| Skip to arbitrary delimiter / prefix | ✅ | ❌ | ❌ |
| Async streaming | ❌ synchronous reads | ✅ | 🟡 partial |

⚠ **One bad record ends the import.** After a `JsonException`, STJ and Newtonsoft readers cannot
continue on the same stream. Robust NDJSON processing requires pre-splitting the input into lines
(extra copying/allocations). Details: [09 Continuous deserialization](09-continuous-deserialization.md).

## Where competitors are ahead

To be fair, these areas are stronger in the competitors:

- **Source generation / AOT** – STJ's source generator supports trimming and Native AOT.
- **DOM APIs** – STJ `JsonNode`/`JsonDocument`, Newtonsoft `JObject`/LINQ to JSON. FeatureLoom has
  no dedicated DOM, but covers most DOM use cases differently (see Dynamic / untyped JSON).
- **Async streaming** – both read asynchronously from streams; FeatureLoom's continuous
  deserialization uses blocking `Stream.Read`.
- **Ecosystem**
- **Attributes** – if attribute-driven configuration is preferred, both offer a wider attribute set.
