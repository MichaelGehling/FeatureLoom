# 04 · Custom writers & readers

Take full control of how a type is written or read — without giving up what the serializer does
for you (structure, nulls, type info, references, nested settings, performance).

## Why

Converters in STJ and Newtonsoft are imperative: you get a writer/reader, you emit or consume
tokens, and you are on your own. That has costs:

- You own braces, commas and null handling. An early `return` or exception leaves broken JSON.
- Names are encoded and nested converters are resolved **per call**.
- The serializer loses all knowledge of your output: no automatic `$type`, no reference tracking
  inside, no settings for nested values.
- You must implement read *and* write, even if you need only one.

FeatureLoom splits a custom handler into **two phases**:

- **Preparation** runs *once per type*: encode field names, resolve nested writers/readers,
  build delegates.
- **Execution** runs *per value* and does nothing but write/read.

And the preparation API tells the serializer the **shape** of your output (value, object, array,
raw), so it keeps its guarantees.

## Writing and reading side by side

A value type as a string:

```csharp
// Writing
writeSettings.ConfigureType<Money>(t => t.SetCustomTypeWriter(prep =>
	prep.PrepareValueWriter<Money>((w, m) => w.WriteString($"{m.Amount} {m.Currency}"))));

// Reading
readSettings.ConfigureType<Money>(t => t.SetCustomTypeReader(prep =>
	prep.PrepareValueReader(api =>
	{
		if (!api.TryReadStringValueOrNull(out string s)) throw new FormatException("Expected string");
		if (s == null) return default;   // JSON null
		var parts = s.Split(' ');
		return new Money { Amount = decimal.Parse(parts[0], CultureInfo.InvariantCulture), Currency = parts[1] };
	})));
```

An object with selected fields:

```csharp
// Writing
writeSettings.ConfigureType<Customer>(t => t.SetCustomTypeWriter(prep =>
	prep.PrepareObjectWriter<Customer>(o => o
		.AddField("name", c => c.Name)
		.AddField("mail", c => c.Email))));

// Reading
readSettings.ConfigureType<Customer>(t => t.SetCustomTypeReader(prep =>
	prep.PrepareObjectReader<Customer>(o => o
		.AddField<string>("name", (c, v) => { c.Name = v; return c; })
		.AddField<string>("mail", (c, v) => { c.Email = v; return c; }))));
```

Each direction is independent. Configure only the one you need.

**System.Text.Json** (Newtonsoft is analogous)

```csharp
public class CustomerConverter : JsonConverter<Customer>
{
	public override void Write(Utf8JsonWriter w, Customer c, JsonSerializerOptions o)
	{
		w.WriteStartObject();
		w.WriteString("name", c.Name);
		w.WriteString("mail", c.Email);
		w.WriteEndObject();
	}

	public override Customer Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
	{
		if (r.TokenType != JsonTokenType.StartObject) throw new JsonException();
		var c = new Customer();
		while (r.Read() && r.TokenType != JsonTokenType.EndObject)
		{
			string name = r.GetString(); r.Read();
			switch (name)
			{
				case "name": c.Name = r.GetString(); break;
				case "mail": c.Email = r.GetString(); break;
				default: r.Skip(); break;
			}
		}
		return c;
	}
}
options.Converters.Add(new CustomerConverter());
```

⚠ **Competitor limitations**
- Both directions must be implemented, even if only one is needed.
- Structure, token-type checks, unknown-field skipping and null handling are hand-written.
- Nested values (`JsonSerializer.Serialize(writer, value.Name, o)`) resolve their converter on
  each call.

## Output shapes (writer)

| Method | Shape | Who writes the delimiters |
|---|---|---|
| `PrepareValueWriter<T>` | single JSON value | — (no structural tokens available) |
| `PrepareObjectWriter<T>` | object | serializer |
| `PrepareArrayWriter<T, TItem>` | array | serializer |
| `PrepareRawWriter<T>` | anything | you |

A `ValueWriteApi` deliberately has no structural tokens, so a value writer cannot emit unbalanced
JSON.

## Reader building blocks

| Method | Purpose |
|---|---|
| `PrepareValueReader<T>(api => …)` | read exactly one JSON value |
| `PrepareObjectReader<T>(o => …)` | declarative fields; unknown fields per `UnknownFieldPolicy` |
| `PrepareArrayReader<TCollection, TElement>(ctor)` | elements via the configured element reader |
| `PrepareTypeReader<T>()` / `(configure)` | reuse the normal reader for another type, optionally with local settings |
| `PrepareNonCustomTypeReader<T>()` | the default reader of `T` itself, bypassing its custom reader |

`ExtensionApi` offers primitive reads (`TryReadStringValueOrNull`, `TryReadSignedIntegerValue`,
`TryReadFloatingPointValue`, `TryReadBoolValue`, `TryReadNullValue`), untyped reads
(`TryReadObjectValue`, `TryReadArrayValue`) and raw access (`TryReadRawJsonValue`).

## Nested objects and arrays (writer)

```csharp
writeSettings.ConfigureType<Order>(t => t.SetCustomTypeWriter(prep =>
	prep.PrepareObjectWriter<Order>(o => o
		.AddField("id", x => x.Id)
		.AddObject("total", x => x.Total, m => m
			.AddField("amount", t => t.Amount)
			.AddField("currency", t => t.Currency))
		.AddArray("tags", x => x.Tags)
		.AddArray("items", x => x.Items, i => i
			.AddField("sku", it => it.Sku)
			.AddField("qty", it => it.Quantity)))));
```

```json
{"id":7,"total":{"amount":12,"currency":"EUR"},"tags":["a","b"],"items":[{"sku":"X","qty":2}]}
```

`null` objects, items and collections become JSON `null`. You write no null checks, and the code's
nesting depth does not grow with the JSON's.

⚠ STJ/Newtonsoft: the same output means writing every `WriteStartObject`/`WritePropertyName`/null
branch by hand, or allocating a DTO per value.

### Flattening and unflattening

The inverse also works: child values can be written as fields of the parent. The accessor is just a
lambda, so it can reach into nested objects:

```csharp
writeSettings.ConfigureType<Order>(t => t.SetCustomTypeWriter(prep =>
    prep.PrepareObjectWriter<Order>(o => o
        .AddField("id", x => x.Id)
        .AddField("customerName", x => x.Customer?.Name)
        .AddField("amount", x => x.Total.Amount)
        .AddField("currency", x => x.Total.Currency))));
```

```json
{"id":7,"customerName":"Ann","amount":12,"currency":"EUR"}
```

Reading it back into the nested structure:

```csharp
readSettings.ConfigureType<Order>(t => t.SetCustomTypeReader(prep =>
    prep.PrepareObjectReader<Order>(o => o
        .AddField<int>("id", (x, v) => { x.Id = v; return x; })
        .AddField<string>("customerName", (x, v) => { (x.Customer ??= new Customer()).Name = v; return x; })
        .AddField<decimal>("amount", (x, v) => { x.Total.Amount = v; return x; })
        .AddField<string>("currency", (x, v) => { x.Total.Currency = v; return x; }))));
```

No intermediate DTO and no DOM: the mapping between the flat JSON and the nested model is part of
the prepared handler.

⚠ STJ/Newtonsoft: flattening needs a DTO/projection per value, or a converter that writes and parses
every token by hand. `[JsonExtensionData]` only covers unknown fields, not the mapping of nested ones.

## Extending instead of replacing

`AddExistingFields()` emits (writer) or accepts (reader) exactly the members the default handler
would, so you can add around them:

```csharp
writeSettings.ConfigureType<Customer>(t => t.SetCustomTypeWriter(prep =>
	prep.PrepareObjectWriter<Customer>(o => o
		.AddField("kind", _ => "customer")
		.AddExistingFields())));

readSettings.ConfigureType<Customer>(t => t.SetCustomTypeReader(prep =>
	prep.PrepareObjectReader<Customer>(o => o
		.AddExistingFields()
		.AddDynamicFields((name, c) => { /* read the value of any unknown field */ return c; }))));
```

Member settings (`SetIgnore`, `OverrideName`, …) configured for the type still apply inside
`AddExistingFields()`.

⚠ STJ and Newtonsoft have no "default members plus mine". A converter replaces everything, and
calling the default serializer from inside a converter for the same type recurses infinitely.

## Dynamic properties

For names known only at runtime:

```csharp
prep.PrepareObjectWriter<Person>(o => o
	.AddField("name", p => p.Name)
	.AddDynamicFields((dyn, p) => { foreach (var kv in p.Extras) dyn.WriteField(kv.Key, kv.Value); }));
// {"name":"Ann","age":42,"city":"Berlin"}
```

`AddDynamicObject("extras", p => p.Extras, …)` nests them in their own object. Commas and nulls are
handled; values go through the writer of their runtime type.

On the reader, `AddDynamicFields((name, item) => …)` receives every undeclared field.

## Delegating to another type's handler

```csharp
writeSettings.ConfigureType<Envelope>(t => t.SetCustomTypeWriter(prep =>
{
	var writePayload = prep.PrepareTypeWriter<Payload>();       // resolved once
	return prep.PrepareRawWriter<Envelope>((raw, e) =>
	{
		raw.OpenObject();
		raw.WriteFieldName("payload");
		writePayload(e.Payload);
		raw.CloseObject();
	});
}));
```

Both `PrepareTypeWriter<T>(configure)` and `PrepareTypeReader<T>(configure)` accept **local
settings** that apply only here. The result bypasses the shared per-type cache, so `Payload`
elsewhere is unaffected. The same `configure` overloads exist on `AddField`, `AddArray` and
`PrepareArrayReader`.

⚠ STJ: a local deviation needs a second `JsonSerializerOptions` (expensive, catastrophic if created
per call). Newtonsoft: a second `JsonSerializer`.

## `$type` is not your problem

Because the shape is declared, the serializer still writes type info for custom writers:

| Shape | With `AddAllTypeInfo` |
|---|---|
| value / raw | `{"$type":"money","$value":<your output>}` |
| object | `{"$type":"person",<your fields>}` |
| array | `{"$type":"tags","$value":[<your items>]}` |

Polymorphic members (declared as `object` or a base type) are written by the writer of the
**runtime** type, including its custom writer.

To make a value claim a *different* type (legacy/foreign DTO names), suppress the built-in envelope
with `SetTypeInfoHandling(AddNoTypeInfo)` and write your own via `prep.PrepareTypeInfo("MoneyDto")`.
This can also be done per member.

⚠ STJ/Newtonsoft converters bypass polymorphism metadata entirely; a converter must write its own
discriminator if it wants round-tripping.

## References are not your problem either

Reference tracking (page 07) keeps working through custom handlers:

- **Writing**: values written via `AddField`, `AddObject`, `AddArray`, `PrepareArrayWriter`,
  `AddDynamicFields` or `PrepareTypeWriter<T>()` go through the serializer's normal writers, so
  shared or circular objects inside a custom writer's output become `$ref`s according to
  `referenceCheck`, with correct JSON paths.
- **Reading**: values read via `AddField`, `AddExistingFields`, `PrepareArrayReader` or
  `PrepareTypeReader<T>()` use the normal readers, so `$ref`s are resolved according to the
  reference resolution settings.
- **Cost-aware**: the declared shape tells the serializer at preparation time whether a custom
  writer's output *can* contain references at all. A value writer cannot, and an object writer whose
  fields are all reference-free (e.g. primitives, strings) cannot either. For these, ref-tracking
  bookkeeping is skipped entirely. Only raw writers are conservatively assumed to contain references.

```csharp
writeSettings.referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef;
writeSettings.ConfigureType<Order>(t => t.SetCustomTypeWriter(prep =>
    prep.PrepareObjectWriter<Order>(o => o
        .AddField("buyer", x => x.Customer)
        .AddField("recipient", x => x.Recipient))));   // same Customer instance
// {"buyer":{"Name":"Ann",...},"recipient":{"$ref":"$.buyer"}}
```

⚠ In STJ and Newtonsoft, a converter is a black box for reference handling. With
`ReferenceHandler.Preserve` / `PreserveReferencesHandling`, values written by hand inside a
converter get no `$id`/`$ref` handling at all, and objects read by a converter are not registered
for later `$ref`s, unless the converter calls back into the serializer for every nested value.

## Raw escape hatch

```csharp
writeSettings.ConfigureType<Weird>(t => t.SetCustomTypeWriter(prep =>
{
	byte[] aName = prep.PrepareFieldName("a");                  // encoded once
	return prep.PrepareRawWriter<Weird>((raw, w) =>
	{
		raw.OpenObject();
		raw.WritePrepared(aName);
		raw.WriteInt(w.A);
		raw.CloseObject();
	});
}));
```

This is the only mode that behaves like an STJ/Newtonsoft converter. Even here, names and constant
fragments (`PrepareRawJson`) are prepared once. `WriteString`, `WriteFieldName` and `WriteRawJson`
accept `TextSegment`/`ReadOnlySpan<char>`, so slices can be written without substring allocations.

For any type beyond primitives, `string`, `Guid` and `DateTime`, use `prep.PrepareTypeWriter<X>()`
instead of looking for a raw overload. That path respects settings and nested custom writers.

## One handler for many types

```csharp
// all derived types
t.SetCustomTypeWriter(prep => …, handlesDerivedTypes: true);

// by predicate, e.g. an attribute
t.SetCustomTypeWriter(prep => …, supportsType: type => type.IsDefined(typeof(CompactAttribute)));
```

The predicate runs once per type, not per value. An exact-type registration always wins over a
predicate match, regardless of order.

⚠ Newtonsoft scans converters in registration order, so a broad converter registered first beats
an exact one registered later. STJ needs a `JsonConverterFactory` + generic converter +
`MakeGenericType`.

## Handlers as classes, and open generic types

```csharp
class WrapperWriter<T> : JsonSerializer.CustomTypeWriterDefinition<Wrapper<T>>
{
	protected override CustomWriter<Wrapper<T>> Prepare(WriterPreparationApi api) =>
		api.PrepareObjectWriter<Wrapper<T>>(o => o.AddField("v", w => w.Value));
}

writeSettings.ConfigureGenericType(typeof(Wrapper<>), t => t.SetCustomTypeWriter(typeof(WrapperWriter<>)));
```

The definition is closed, instantiated and prepared once per constructed type (`Wrapper<int>`,
`Wrapper<Money>`, …). Readers work the same via `JsonDeserializer.CustomTypeReaderDefinition<T>` and
`SetCustomTypeReader(typeof(...))`. Arity and parameter order are validated with errors naming the
types.

| Registration form | Use when |
|---|---|
| `SetCustomTypeWriter(prep => …)` | common inline case; only form with `supportsType` widening |
| `SetCustomTypeWriter(definitionInstance)` | handler needs constructor state |
| `SetCustomTypeWriter(typeof(MyWriter<>))` | open generic types |

⚠ STJ requires a factory writing the `MakeGenericType` plumbing yourself; Newtonsoft's
`CanConvert` gives no typed access, so the body becomes reflective.

## Pitfalls & precedence

- A custom reader must consume **exactly one** JSON value.
- Context-local settings (`AddField(…, configure)`, member settings) are **merged onto** the type's
  settings per option, not a replacement.
- A custom writer/reader is not transferred to a deviating runtime type. That type uses its own.
- Registration precedence: exact type > constructed generic > open generic > predicate
  (first registered wins among predicates).
- Declared field names are encoded once; dynamic names are encoded per value. Prefer declared ones.
