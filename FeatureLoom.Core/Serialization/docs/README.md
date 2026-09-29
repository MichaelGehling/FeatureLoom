# FeatureLoom JSON

`FeatureLoom.Serialization.JsonSerializer` and `JsonDeserializer`: a fast, highly configurable JSON
serializer/deserializer for .NET Framework 4.8, .NET Standard 2.0/2.1, .NET 8 and .NET 10.

This documentation is a manual, a feature showcase and a migration guide in one. Every topic page
follows the same layout:

1. **Why** – the problem and what the design buys you.
2. **FeatureLoom** – examples, writing and reading side by side where they correspond.
3. **Compared to STJ / Newtonsoft** – the same task in System.Text.Json and Newtonsoft.Json,
   with their limitations called out explicitly (⚠).
4. **Reference** – settings, methods and defaults.
5. **Pitfalls & precedence** – what wins when configurations overlap.

## Five-second tour

```csharp
var serializer = new JsonSerializer();
var deserializer = new JsonDeserializer();

string json = serializer.Serialize(order);
if (deserializer.TryDeserialize(json, out Order copy)) { /* ... */ }
```

For ad-hoc use, `JsonHelper.DefaultSerializer` / `JsonHelper.DefaultDeserializer` provide shared
default instances.

## Feature highlights

| Area | What you get |
|---|---|
| **Full state by default** | Public *and private* fields are written and read, auto-property backing fields under their clean property name. Objects round-trip without annotations. |
| **Configuration without attributes** | Every type, member, element, dictionary key and generic type definition is configurable from the outside via `Settings` – including types you do not own. |
| **…or owned by the type** | A type can carry its own configuration in a `[JsonTypeConfiguration]` method, opt-in per serializer. |
| **Layered settings** | Global → generic definition → constructed type → member → context-local override, merged per option. `ConfigureRecursively` applies policies to a whole subtree. |
| **Two-phase custom writers/readers** | Preparation runs once per type (name encoding, nested writer lookup), the per-value path only writes. Declarative object/array builders own braces, commas and null handling. |
| **Polymorphism without registration** | `$type` is written only where the runtime type deviates. The reader can additionally infer the concrete type from field names, field values or the value shape – no discriminator required. |
| **Safe type handling** | Forbidden types, whitelist modes and namespace prefixes govern which payload-proposed types may be created. |
| **References** | Duplicate and circular references as compact JSONPath refs (`{"$ref":"$.Items[0]"}`) or the STJ/Newtonsoft-compatible `$id`/`$ref` format. |
| **String deduplication** | Optional fixed-size UTF-8 string cache (on by default): recurring string values such as status codes, currencies or names are materialized once and shared instead of allocated per occurrence. Enabled/disabled globally, per type, per member or recursively for a subtree; size and max string length configurable. |
| **Performance** | UTF-8 end to end, readers/writers compiled once per type, per-member configuration baked into strategy structs. ~1.7× faster than STJ with about half the allocations on a mixed complex object ([details](08-performance.md#benchmark-results)). |
| **Continuous deserialization** | Bind a stream/string/bytes once and read value after value (NDJSON, concatenated JSON); `IsAnyDataLeft()` and `SkipBufferUntil(delimiter)` for resync after broken records ([details](09-continuous-deserialization.md)). |
| **Symmetry** |

## Pages

| Page | Content |
|---|---|
| [Feature matrix](feature-matrix.md) | FeatureLoom vs. System.Text.Json vs. Newtonsoft.Json, incl. competitor limitations |
| [01 Quickstart](01-quickstart.md) | entry points, sources/targets, defaults worth knowing |
| [02 Settings model](02-settings-model.md) | compiled snapshots, reuse, thread safety, layering, global options |
| [03 Type configuration](03-type-configuration.md) | `ConfigureType`, members, elements, keys, generics, recursive settings, self-configuration |
| [04 Custom writers & readers](04-custom-writers-readers.md) | two-phase handlers, object/array builders, delegation, dynamic fields, `$type`, raw mode, open generics |
| [05 Type mappings](05-type-mappings.md) | interface/base mappings, field-name inference, discriminator checkers, value mappings, string recognition |
| [06 Polymorphism & type info](06-polymorphism.md) | `$type` writing/reading, type names, proposed types, forbidden types, whitelist |
| [07 References](07-references.md) | reference check modes, JSONPath vs. `$id` format, per-type resolution |
| [08 Performance](08-performance.md) | cost model, instance reuse, thread safety, relevant settings, string cache, checklist |
| [09 Continuous deserialization](09-continuous-deserialization.md) | data sources, value sequences, NDJSON, delimiter skipping, error resync |
| [Migration from STJ](migration-from-stj.md) |
| [Migration from Newtonsoft](migration-from-newtonsoft.md) | defaults, API, attributes, converters, `TypeNameHandling`/binder, references, gaps |

## Sample model

All pages use the same small domain model:

```csharp
public class Order
{
	public int Id;
	public Customer Customer;
	public Money Total;
	public List<OrderItem> Items = new();
	public string[] Tags;
	private string internalNote;          // private state – round-trips by default
}

public class Customer { public string Name { get; set; } public string Email { get; set; } }
public struct Money { public decimal Amount; public string Currency; }
public class OrderItem { public string Sku; public int Quantity; }

public interface IShape { }
public class Circle : IShape { public double Radius; }
public class Rectangle : IShape { public double Width, Height; }
```
