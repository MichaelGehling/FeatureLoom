# 01 Quickstart

## Entry points

```csharp
using FeatureLoom.Serialization;

var serializer = new JsonSerializer();
var deserializer = new JsonDeserializer();

string json = serializer.Serialize(order);
if (deserializer.TryDeserialize(json, out Order copy)) { /* ... */ }
```

Create instances once and reuse them. Each instance caches compiled readers/writers and buffers
(see [08 Performance](08-performance.md)).

For ad-hoc use, shared instances are available:

```csharp
string json = JsonHelper.DefaultSerializer.Serialize(order);          // indented output
JsonHelper.DefaultDeserializer.TryDeserialize(json, out Order copy);
```

## Serializing

| Target | Call |
|---|---|
| `string` | `serializer.Serialize(item)` |
| `Stream` (sync) | `serializer.Serialize(stream, item)` |
| `Stream` (async) | `await serializer.SerializeAsync(stream, item)` (double-buffered async writes, see [08](08-performance.md#async-serialization-to-streams)) |

## Deserializing

All variants return `bool` instead of throwing (configurable, see below).

| Source | Call |
|---|---|
| `string` | `TryDeserialize(json, out T item)` |
| UTF-8 bytes | `TryDeserialize(byte[] / ByteSegment, out T item)` |
| `Stream` | `TryDeserialize(stream, out T item)` |
| `JsonFragment` | `TryDeserialize(fragment, out T item)` |
| runtime type | `TryDeserialize(json, typeof(Order), out object item)` (all sources) |
| bound source | `SetDataSource(...)` + `TryDeserialize(out T item)` – see [09](09-continuous-deserialization.md) |

Unknown JSON can be read into `object` (→ `Dictionary<string, object>`, `List<object>`, primitives)
or kept raw as `JsonFragment`.

## Configuring

Settings are passed on construction – either as object or via a callback:

```csharp
var serializer = new JsonSerializer(s =>
{
	s.indent = true;
	s.enumAsString = true;
});

var deserializer = new JsonDeserializer(s =>
{
	s.rethrowExceptions = true;
});
```

See [02 Settings model](02-settings-model.md) for how settings are compiled and layered.

## Defaults worth knowing

| Topic | Default | Change with |
|---|---|---|
| Members | public **and private** fields; auto-property backing fields under the property name | serializer `dataSelection`, deserializer `dataAccess` |
| Type info | `$type` only where the runtime type deviates from the declared type | serializer `typeInfoHandling` |
| Proposed types on reading | applied where reasonable, forbidden types always blocked | deserializer `proposedTypeMode`, `typeWhitelistMode` ([06](06-polymorphism.md)) |
| References | not written / not resolved | serializer `referenceCheck`, deserializer `referenceResolutionMode` ([07](07-references.md)) |
| Enums | numbers | serializer `enumAsString` |
| Indentation | off (`JsonHelper.DefaultSerializer`: on) | serializer `indent` |
| Unknown fields | skipped | deserializer `unknownFieldPolicy` |
| Numbers in strings | accepted | deserializer `strict = true` |
| Errors | `TryDeserialize` returns `false`, exception is logged | deserializer `rethrowExceptions`, `logCatchedExceptions` |
| String cache | on | deserializer `useStringCache` ([03](03-type-configuration.md)) |

## Next steps

- Shape the output per type/member: [03 Type configuration](03-type-configuration.md)
- Full control over a type: [04 Custom writers & readers](04-custom-writers-readers.md)
- Coming from another library: [STJ](migration-from-stj.md), [Newtonsoft](migration-from-newtonsoft.md)
