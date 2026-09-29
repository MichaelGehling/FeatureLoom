# Migration from System.Text.Json

A mapping from STJ concepts to FeatureLoom, ordered by what you hit first.

## 1. Defaults differ — check them first

| Aspect | STJ default | FeatureLoom default | To get STJ behavior |
|---|---|---|---|
| Which members | public properties | **public and private fields** (backing fields under clean property names) | serializer `dataSelection = PublicFieldsAndProperties`, deserializer `dataAccess = DataAccess.PublicFieldsAndProperties` |
| Enums | numbers | numbers | — (same) |
| Polymorphic values | written as declared type | `$type` where the runtime type deviates | `typeInfoHandling = AddNoTypeInfo` |
| Unknown JSON fields | skipped | skipped | — |
| Numbers in strings (`"42"`) | error (unless `JsonNumberHandling.AllowReadingFromString`) | accepted (non-strict mode) | deserializer `strict = true` to reject |
| Errors | throw | `TryDeserialize` returns `false`, exception is logged | deserializer `rethrowExceptions = true` |
| Instances | stateless static API | instances with own buffers | reuse instances (see [page 08](08-performance.md)) |

The FeatureLoom default round-trips private state without annotations. If the JSON is a public
contract consumed by others, switch to `PublicFieldsAndProperties` to keep the contract explicit.

## 2. API

| STJ | FeatureLoom |
|---|---|
| `new JsonSerializerOptions { … }` | `new JsonSerializer.Settings { … }` / `new JsonDeserializer.Settings { … }` |
| `JsonSerializer.Serialize(obj, options)` | `serializer.Serialize(obj)` → `string`; `Serialize(stream, obj)`, `SerializeAsync(stream, obj)` |
| `JsonSerializer.Deserialize<T>(json, options)` | `deserializer.TryDeserialize<T>(json, out var result)` (string, `byte[]`, `Stream`, `ByteSegment`, `JsonFragment`) |
| populate via `JsonObjectCreationHandling.Populate` | `deserializer.TryPopulate(json, existing)` |
| `JsonDocument` / `JsonNode` | `object` → `Dictionary<string, object>` trees, `JsonFragment`, or better: member mapping (page 03) |

```csharp
// STJ
var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
string json = System.Text.Json.JsonSerializer.Serialize(order, options);
var copy = System.Text.Json.JsonSerializer.Deserialize<Order>(json, options);

// FeatureLoom
var serializer = new JsonSerializer(new JsonSerializer.Settings { enumAsString = true });
var deserializer = new JsonDeserializer(new JsonDeserializer.Settings());
string json = serializer.Serialize(order);
deserializer.TryDeserialize(json, out Order copy);
```

The deserializer reads enums from both names and numbers, so no reader-side setting is needed.

## 3. Attributes

| STJ | FeatureLoom |
|---|---|
| `[JsonIgnore]` | `[JsonIgnore]` from `FeatureLoom.Serialization`; or `ConfigureMember(…, m => m.SetIgnore())` |
| `[JsonInclude]` | `[JsonInclude]` from `FeatureLoom.Serialization` |
| `[JsonPropertyName("x")]` | `ConfigureMember(name, m => m.OverrideName("x"))` on both sides |
| `[JsonConverter(typeof(X))]` | `SetCustomTypeWriter` / `SetCustomTypeReader` (page 04), or type-owned configuration (page 03) |
| `[JsonDerivedType]` / `[JsonPolymorphic]` | not needed: `$type` is automatic; for foreign discriminators use type mappings (page 05) |
| `[JsonConstructor]` | custom reader that collects fields and calls the constructor (page 04) |

Only the attribute **names** match. The STJ attributes in `System.Text.Json.Serialization` are
not evaluated. Replace the `using`, or keep both if the model must serve both serializers.

```csharp
// Rename, shared by both sides
serializerSettings.ConfigureType<Customer>(t => t.ConfigureMember<string>("Email", m => m.OverrideName("mail")));
deserializerSettings.ConfigureType<Customer>(t => t.ConfigureMember<string>("Email", m => m.OverrideName("mail")));
```

## 4. Converters → custom writers/readers

| STJ `JsonConverter<T>` | FeatureLoom |
|---|---|
| `Write(Utf8JsonWriter, T, options)` runs everything per value | `PrepareObjectWriter` / `PrepareValueWriter` run once; the returned delegate only writes |
| `Read(ref Utf8JsonReader, …)`: manual token loop | `PrepareObjectReader` + `AddField` / `AddExistingFields` |
| `JsonConverterFactory` for generics | open generic handler classes (page 04) |
| `options.GetConverter(typeof(X))` inside a converter | `prep.PrepareTypeWriter<X>()` / `PrepareTypeReader<X>()` during preparation |
| manually writing a discriminator | handled by the serializer |

Most STJ converters exist to rename, reshape or skip a few members. In FeatureLoom that is
member configuration (page 03), not a converter.

## 5. Polymorphism

| STJ | FeatureLoom |
|---|---|
| `[JsonDerivedType(typeof(Circle), "circle")]` on the base | `SetCustomTypeName("circle")` on `Circle` (serializer) + `AddCustomTypeName("circle", typeof(Circle))` (deserializer) |
| `$type` must come first (or `AllowOutOfOrderMetadataProperties`) | `$type` must come first by default; per type, a discriminator field (incl. `$type`) can be matched at any position via type mappings (see [page 05](05-type-mappings.md#discriminator-fields-at-any-position)) |
| no discriminator → not supported | type inference from fields/values (page 05) |

## 6. References

| STJ | FeatureLoom |
|---|---|
| `ReferenceHandler.Preserve` | `referenceCheck = AlwaysReplaceByRef`, `referenceFormat = IdBased` (same wire format) |
| `ReferenceHandler.IgnoreCycles` | `referenceCheck = OnLoopReplaceByNull` |

JSON written by STJ with `Preserve` is read directly by FeatureLoom when reference resolution is
enabled for the types (page 07).

## 7. Not (directly) available

Check these before migrating:

- **Naming policies** (`CamelCase`, `SnakeCaseLower`): there is no global naming policy. Rename
  per member with `OverrideName`.
- **Case-insensitive property matching**: not a documented option. Verify with your payloads.
- **Source generation / trimming / Native AOT**: FeatureLoom compiles handlers at runtime and is
  not trimming-safe.
- **ASP.NET Core integration**: STJ is the built-in formatter. FeatureLoom needs a custom
  input/output formatter.
