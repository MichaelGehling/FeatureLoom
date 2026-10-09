# Migration from Newtonsoft.Json

A mapping from Newtonsoft concepts to FeatureLoom, ordered by what you hit first.

## 1. Defaults differ — check them first

| Aspect | Newtonsoft default | FeatureLoom default | To get Newtonsoft behavior |
|---|---|---|---|
| Which members | public fields and properties | **public and private fields** (backing fields under clean property names) | serializer `dataSelection = PublicFieldsAndProperties`, deserializer `dataAccess = DataAccess.PublicFieldsAndProperties` |
| Enums | numbers | numbers | — (same) |
| Type names | off (`TypeNameHandling.None`) | `$type` where the runtime type deviates | `typeInfoHandling = AddNoTypeInfo` |
| Type name format | assembly-qualified | simplified (`Namespace.Type`) | `typeNameFormat = AssemblyQualified` |
| Arrays with type info | `$values` | `$value` | `arrayValueFieldName = ValueFieldName.Values` |
| Errors | throw | `TryDeserialize` returns `false`, exception is logged | deserializer `rethrowExceptions = true` |
| Reuse into existing objects | `PopulateObject` | `TryPopulate`; `populateExistingMembers = true` | — |

## 2. API

| Newtonsoft | FeatureLoom |
|---|---|
| `JsonConvert.SerializeObject(obj, settings)` | `serializer.Serialize(obj)` |
| `JsonConvert.DeserializeObject<T>(json, settings)` | `deserializer.TryDeserialize<T>(json, out var result)` |
| `JsonConvert.PopulateObject(json, target)` | `deserializer.TryPopulate(json, target)` |
| `Formatting.Indented` | `formatting = JsonFormatting.Indented` |
| `JObject` / `JToken` / `dynamic` | `object` → `Dictionary<string, object>` trees, `JsonFragment`, or better: member mapping (page 03) |
| static `JsonConvert.DefaultSettings` | `JsonHelper.DefaultSerializer` / `DefaultDeserializer` |

```csharp
// Newtonsoft
var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, Formatting = Formatting.Indented };
string json = JsonConvert.SerializeObject(drawing, settings);
var copy = JsonConvert.DeserializeObject<Drawing>(json, settings);

// FeatureLoom (type info for deviating types is the default)
var serializer = new JsonSerializer(new JsonSerializer.Settings { formatting = JsonSerializer.JsonFormatting.Indented });
var deserializer = new JsonDeserializer(new JsonDeserializer.Settings());
string json = serializer.Serialize(drawing);
deserializer.TryDeserialize(json, out Drawing copy);
```

## 3. Attributes

| Newtonsoft | FeatureLoom |
|---|---|
| `[JsonIgnore]` | `[JsonIgnore]` from `FeatureLoom.Serialization`; or `ConfigureMember(…, m => m.SetIgnore())` |
| `[JsonProperty]` on a private member | `[JsonInclude]` (or nothing: private fields are included by default) |
| `[JsonProperty("x")]` | `ConfigureMember(name, m => m.OverrideName("x"))` on both sides |
| `[JsonConverter(typeof(X))]` | `SetCustomTypeWriter` / `SetCustomTypeReader` (page 04), or type-owned configuration (page 03) |
| `[JsonConstructor]` | custom reader that collects fields and calls the constructor (page 04) |
| `[OnDeserialized]` etc. | custom reader wrapping the default reader (page 04) |

Newtonsoft attributes are not evaluated. Replace them, or keep both if the model must serve both
serializers.

## 4. Converters → custom writers/readers

| Newtonsoft `JsonConverter` | FeatureLoom |
|---|---|
| `WriteJson` with `JsonWriter` per value | `PrepareObjectWriter` / `PrepareValueWriter` run once; the delegate only writes |
| `ReadJson`: `JObject.Load(reader)` + `ToObject` | `PrepareObjectReader` + `AddField` / `AddExistingFields`, no DOM |
| `CanConvert(Type)` | `SetCustomTypeWriter(prepare, supportsType)` or per-type registration |
| `serializer.Serialize(writer, child)` inside a converter | `prep.PrepareTypeWriter<X>()` during preparation |
| `ContractResolver` to rename/skip/select members | `ConfigureType` / `ConfigureMember` / `ConfigureRecursively` |

⚠ Converters built on `JObject.Load` + `ToObject` parse twice and allocate a DOM. Replacing them
typically gives the largest speed-up of the migration.

## 5. Polymorphism and security

| Newtonsoft | FeatureLoom |
|---|---|
| `TypeNameHandling.Auto` | default `AddDeviatingTypeInfo` |
| `TypeNameHandling.All` / `Objects` | `AddAllTypeInfo` |
| `ISerializationBinder` (whitelist, custom names) | forbidden types (on by default), `typeWhitelistMode`, `AddAllowedType`, `AddAllowedNamespacePrefix`, `AddCustomTypeName` |
| `$type` with assembly-qualified names | read directly; the payload can stay unchanged |
| `JsonSubTypes` / custom converter for discriminators | type mappings (page 05) |

Existing Newtonsoft JSON with `$type`, `$values`, `$id` and `$ref` can be read. Use a whitelist
(`typeWhitelistMode = ForProposedTypesOnly`) when the payload comes from outside, as you should
have with Newtonsoft too.

## 6. References

| Newtonsoft | FeatureLoom |
|---|---|
| `PreserveReferencesHandling.Objects/All` | `referenceCheck = AlwaysReplaceByRef`, `referenceFormat = IdBased` |
| `ReferenceLoopHandling.Ignore` | `OnLoopReplaceByNull` (writes `null` instead of dropping the member) |
| `ReferenceLoopHandling.Serialize` | `NoRefCheck` (with the same stack overflow risk) |
| `ReferenceLoopHandling.Error` | `OnLoopThrowException` |

## 7. Not (directly) available

Check these before migrating:

- **Naming strategies** (`CamelCaseNamingStrategy` etc.): no built-in naming policy. Rename per member with `OverrideName` or by rule with `OverrideMemberNames(Func<string,string>)` (also recursive).
- **Case-insensitive matching** (Newtonsoft default): not a documented option. Verify with your payloads.
- **`NullValueHandling.Ignore` / `DefaultValueHandling`**: not a documented option.
- **Date formats and `DateParseHandling`**: dates use ISO 8601; custom formats need a custom writer/reader.
- **LINQ to JSON** (`JObject` querying, `SelectToken`): no equivalent. Use typed models, dictionary trees or `JsonFragment`.
- **BSON**: not supported.
