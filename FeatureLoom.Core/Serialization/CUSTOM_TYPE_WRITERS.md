# Custom Type Writers

How to control the JSON output of a specific type with `FeatureLoom.Serialization.JsonSerializer`,
compared to Newtonsoft.Json and System.Text.Json.

> Scope: this document covers **writing** only. Reading is configured separately on
> `JsonDeserializer`.

## The model in one minute

A custom writer is registered per type and built in **two phases**:

```csharp
var settings = new JsonSerializer.Settings();
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<Money>((value, item) => value.WriteString($"{item.Amount} {item.Currency}"))));

var serializer = new JsonSerializer(settings);
```

- **Phase 1 (`prep`)** runs *once per type*. Field names get UTF-8 encoded, nested type writers
  get resolved, delegates get built.
- **Phase 2** is the returned writer, invoked *per value*. It should do nothing but write.

That split is the whole point: the per-value path contains no lookups, no string encoding and no
reflection. Newtonsoft and STJ converters have no preparation phase — every `WriteJson` /
`Write` call re-does name encoding and sub-converter resolution.

The `prep` API also tells the serializer the **shape** of your output (value / object / array / raw),
so the serializer can still emit braces, brackets and commas — and still knows whether reference
tracking has to look inside your value.

| Method | Shape | Who writes the delimiters |
|---|---|---|
| `PrepareValueWriter<T>` | single JSON value | — |
| `PrepareObjectWriter<T>` | object | serializer |
| `PrepareArrayWriter<T, TItem>` | array | serializer |
| `PrepareRawWriter<T>` | anything | you |

A writer can also be declared as a class instead of a lambda, which is what makes writers for
**open generic types** possible — see [section 7](#7-writers-as-classes-and-open-generic-types).

### Type info (`$type`) is not your problem

Declaring the shape has a second effect: the serializer keeps emitting the `$type` envelope for
you, exactly as it does for built-in writers. A custom writer never writes `$type` itself.

| Shape | With `AddAllTypeInfo` |
|---|---|
| value / raw | `{"$type":"money","$value":<your output>}` |
| object | `{"$type":"person",<your fields>}` |
| array | `{"$type":"tags","$value":[<your items>]}` |

`AddDeviatingTypeInfo` applies the same envelope, but only when the runtime type deviates from the
declared one. The name comes from the usual sources — `SetCustomTypeName` or the configured
`typeNameFormat` — so a custom writer needs no extra configuration to stay
round-trippable.

Two consequences worth knowing:

- For an object writer the type info is written as the **first member**, before your fields. If you
  declare no fields at all, the output is just `{"$type":"..."}` — the separating comma is rolled
  back.
- `PrepareRawWriter` gives you control over the tokens *inside* the envelope, not over the envelope
  itself. Writing your own `$type` member there would produce a duplicate — unless you suppress the
  built-in one, which is how a value can claim a foreign type
  ([section 5](#claiming-a-different-type)).

Runtime polymorphy is handled for you as well: a field declared as `object` or as a base type is
written with the writer of the value's **actual** type, so its real members appear and the deviating
`$type` is emitted. This applies to `AddField`, `AddArray`, `PrepareArrayWriter` and
`PrepareTypeWriter` alike — see
[Polymorphic values and settings](#polymorphic-values-and-settings).

---

## 1. Value type written as a string

**FeatureLoom**

```csharp
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<Money>((value, m) => value.WriteString($"{m.Amount} {m.Currency}"))));
```

**Newtonsoft.Json**

```csharp
public class MoneyConverter : JsonConverter<Money>
{
	public override void WriteJson(JsonWriter writer, Money value, Newtonsoft.Json.JsonSerializer s)
		=> writer.WriteValue($"{value.Amount} {value.Currency}");

	public override Money ReadJson(...) => throw new NotImplementedException();
}

settings.Converters.Add(new MoneyConverter());
```

**System.Text.Json**

```csharp
public class MoneyConverter : JsonConverter<Money>
{
	public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions o)
		=> writer.WriteStringValue($"{value.Amount} {value.Currency}");

	public override Money Read(...) => throw new NotImplementedException();
}

options.Converters.Add(new MoneyConverter());
```

**Difference:** both alternatives force you to declare a class and to implement `Read`, even when
you only care about writing. FeatureLoom keeps it a lambda, and the `ValueWriteApi` deliberately
offers *no* structural tokens — you cannot accidentally emit an unbalanced brace from a value writer.

---

## 2. Object with selected fields

**FeatureLoom**

```csharp
settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareObjectWriter<Person>(obj => obj
		.AddField("name", p => p.Name)
		.AddField("age", p => p.Age))));
```

`AddField` routes the value through the serializer's writer for its type, so nested custom
writers, enum settings and type-info handling still apply. Field names are encoded once.

**Newtonsoft.Json**

```csharp
public override void WriteJson(JsonWriter writer, Person value, Newtonsoft.Json.JsonSerializer s)
{
	writer.WriteStartObject();
	writer.WritePropertyName("name");
	s.Serialize(writer, value.Name);
	writer.WritePropertyName("age");
	s.Serialize(writer, value.Age);
	writer.WriteEndObject();
}
```

**System.Text.Json**

```csharp
public override void Write(Utf8JsonWriter writer, Person value, JsonSerializerOptions o)
{
	writer.WriteStartObject();
	writer.WritePropertyName("name");
	JsonSerializer.Serialize(writer, value.Name, o);
	writer.WritePropertyName("age");
	JsonSerializer.Serialize(writer, value.Age, o);
	writer.WriteEndObject();
}
```

**Difference:** in both alternatives *you* own the braces, so an early `return` or an exception
mid-way produces broken JSON. In FeatureLoom the braces belong to the serializer. STJ can avoid the
per-call name encoding with a cached `JsonEncodedText`, but you have to know that and do it by hand;
FeatureLoom does it for you because the builder runs in the preparation phase.

---

## 3. Nested objects and arrays

This is where the declarative style pays off.

**FeatureLoom**

```csharp
settings.ConfigureType<Order>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareObjectWriter<Order>(obj => obj
		.AddField("id", o => o.Id)
		.AddObject("total", o => o.Total, m => m
			.AddField("amount", t => t.Amount)
			.AddField("currency", t => t.Currency))
		.AddArray("tags", o => o.Tags)
		.AddArray("items", o => o.Items, i => i
			.AddField("sku", x => x.Sku)
			.AddField("qty", x => x.Quantity)))));
```

```json
{"id":7,"total":{"amount":12,"currency":"EUR"},"tags":["a","b"],"items":[{"sku":"X","qty":2}]}
```

A `null` nested object, a `null` item and a `null` collection are each written as the JSON `null`
literal — you do not write those checks.

The array field has two forms:

```csharp
.AddArray("tags", o => o.Tags)                      // items via the serializer's writer for the item type
.AddArray("items", o => o.Items, i => i.AddField(…)) // items as inline-built objects
```

Whole types can be arrays too, with the same three flavours:

```csharp
prep.PrepareArrayWriter<Basket, Money>(b => b.Items)                                    // type writer per item
prep.PrepareArrayWriter<Basket, Money>(b => b.Items, m => m.AddField("amount", …))      // nested builder
prep.PrepareArrayWriter<Tags, string>(t => t.Values, (raw, v) => raw.WriteString(v))    // raw per item
```

`AddField`, `AddArray` and `PrepareArrayWriter` each take an optional `configure` callback that
writes the values with settings deviating from the ones configured for that type — local to this
field, without affecting how the type is written anywhere else:

```csharp
.AddField("item", o => o.Item, s => s.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo))
```

See [How context-local settings combine](#how-context-local-settings-combine) for how these
interact with the type's own configuration and with polymorphic values.

**Newtonsoft.Json / System.Text.Json**

There is no declarative equivalent. You either write the whole tree imperatively:

```csharp
writer.WriteStartObject();
writer.WritePropertyName("total");
if (value.Total == null) writer.WriteNullValue();
else
{
	writer.WriteStartObject();
	writer.WritePropertyName("amount");
	writer.WriteNumberValue(value.Total.Amount);
	// …
	writer.WriteEndObject();
}
writer.WritePropertyName("items");
if (value.Items == null) writer.WriteNullValue();
else
{
	writer.WriteStartArray();
	foreach (var item in value.Items) { /* … */ }
	writer.WriteEndArray();
}
writer.WriteEndObject();
```

…or you introduce a DTO and serialize that, which costs an allocation and a copy per value.

**Difference:** the nesting depth of the *code* no longer grows with the nesting depth of the
*JSON*, and null handling is not your problem.

---

## 4. Delegating to another type's writer

Sometimes a custom writer needs to emit a value of a different type.

**FeatureLoom**

```csharp
settings.ConfigureType<Envelope>(ts => ts.SetCustomTypeWriter(prep =>
{
	var writePayload = prep.PrepareTypeWriter<Payload>();   // resolved once
	return prep.PrepareRawWriter<Envelope>((raw, e) =>
	{
		raw.OpenObject();
		raw.WriteFieldName("payload");
		writePayload(e.Payload);
		raw.CloseObject();
	});
}));
```

There is also an overload that resolves the writer with **deviating settings**, without affecting
how that type is written anywhere else:

```csharp
var writeTotal = prep.PrepareTypeWriter<Money>(ms => ms.SetCustomTypeWriter(
	p => p.PrepareValueWriter<Money>((value, m) => value.WriteInt(m.Amount))));
```

The resulting writer is local to this preparation and bypasses the shared per-type cache, so
`Money` serialized on its own still uses its regular writer.

**Newtonsoft.Json**

`serializer.Serialize(writer, value)` — resolved per call, and settings are global. A local
deviation requires a second `JsonSerializer` instance with its own settings, or a marker type.

**System.Text.Json**

`options.GetConverter(typeof(Payload))` can be cached in the converter, but a *local settings
deviation* means constructing a second `JsonSerializerOptions` — which is expensive and, if done
per call, catastrophically so.

**Difference:** FeatureLoom makes both the lookup and the local settings override a preparation-time
concern, so neither is on the write path.

---

## 5. Escape hatch: raw tokens

When nothing else fits:

```csharp
settings.ConfigureType<Weird>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareRawWriter<Weird>((raw, w) =>
	{
		raw.OpenArray();
		raw.WriteInt(w.A);
		raw.WriteComma();
		raw.WriteRawJson(w.PrecomputedJson);
		raw.CloseArray();
	})));
```

Available per field too:

```csharp
.AddRawField("weird", (raw, item) => raw.WriteRawJson(item.PrecomputedJson))
```

`WriterPreparationApi` exposes `PrepareFieldName` and `PrepareRawJson`, so names and constant
fragments are encoded once in the preparation phase and emitted later with a single buffer copy via
`WritePrepared` / `WriteRawJson`:

```csharp
settings.ConfigureType<Weird>(ts => ts.SetCustomTypeWriter(prep =>
{
	byte[] aName = prep.PrepareFieldName("a");   // encodes "a": once
	JsonFragment header = prep.PrepareRawJson("{\"kind\":\"weird\"}");

	return prep.PrepareRawWriter<Weird>((raw, w) =>
	{
		raw.OpenObject();
		raw.WritePrepared(aName);
		raw.WriteInt(w.A);
		raw.CloseObject();
	});
}));
```

### Avoiding allocations

`WriteString`, `WriteRawJson`, `WriteFieldName` and `WritePrepared` accept more than just `string`
and `byte[]`, so a slice of an existing buffer can be written without materializing a copy:

| Method | Also accepts |
|---|---|
| `WriteString` | `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WriteFieldName` | `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WriteRawJson` | `JsonFragment`, `TextSegment`, `ReadOnlySpan<char>`¹ |
| `WritePrepared` | `ByteSegment`, `ReadOnlySpan<byte>`¹ |

¹ not available on `netstandard2.0`; use the segment types there for portable code.

```csharp
raw.WriteString(fullText.AsSpan(7, 3));            // no substring
raw.WriteString(new TextSegment(fullText, 7, 3));  // same, on every target
```

Because `string` and `ReadOnlySpan<char>` overloads coexist, a bare `null` literal is ambiguous.
Write `WriteNull()` when you mean a JSON null — it is clearer anyway.

### Which types can I write directly?

`ValueWriteApi` and `RawWriteApi` expose exactly the types the writer can emit as a JSON token on its
own: the numeric primitives, `bool`, `string`, `Guid` and `DateTime`. For **anything else**, do not
look for a missing overload — prepare a writer for that type instead:

```csharp
settings.ConfigureType<Weird>(ts => ts.SetCustomTypeWriter(prep =>
{
	var writeTimeSpan = prep.PrepareTypeWriter<TimeSpan>();

	return prep.PrepareRawWriter<Weird>((raw, w) => writeTimeSpan(w.Duration));
}));
```

That path respects settings, nested custom writers and reference tracking, which a raw overload
could not.

**Difference:** this is the *only* mode in FeatureLoom that behaves like a Newtonsoft/STJ converter —
you own the tokens and the serializer conservatively assumes your output may contain references.
The other modes give the serializer enough information to keep its guarantees. In Newtonsoft and STJ
every converter is in this mode, always.

### Claiming a different type

Sometimes the JSON has to claim a type the CLR object is not — mimicking a DTO, or an older
version of a class. That needs two steps, because the serializer's envelope and yours would
otherwise both be written:

1. Suppress the built-in envelope for that type scope with `SetTypeInfoHandling(AddNoTypeInfo)`.
2. Emit your own, prepared once via `PrepareTypeInfo`.

```csharp
settings.ConfigureType<Money>(ts =>
{
	ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
	ts.SetCustomTypeWriter(prep =>
	{
		byte[] typeInfo = prep.PrepareTypeInfo("MoneyDto");   // or PrepareTypeInfo<MoneyDto>()
		byte[] amount = prep.PrepareFieldName("amount");
		return prep.PrepareRawWriter<Money>((raw, item) =>
		{
			raw.OpenObject();
			raw.WritePrepared(typeInfo);
			raw.WriteComma();
			raw.WritePrepared(amount);
			raw.WriteInt(item.Amount);
			raw.CloseObject();
		});
	});
});
// {"$type":"MoneyDto","amount":12}
```

`PrepareTypeInfo(string)` writes the name verbatim. `PrepareTypeInfo<TOther>()` resolves it the
same way the serializer does, so a custom type name or the configured `typeNameFormat` is honored
instead of hardcoded. Both encode to UTF-8 once, in the preparation phase.

The verbatim overload does **not** require the named type to exist in this process — it is a
string, never a `Type`. That is the point: the target type often lives only in the consuming
system, e.g. a legacy or foreign application.

```csharp
byte[] typeInfo = prep.PrepareTypeInfo("Legacy.Money, LegacyApp");
// {"$type":"Legacy.Money, LegacyApp", ... }
```

Because the suppression is part of the type scope, it stays local to that settings object — a
serializer without this configuration keeps writing the normal envelope for the same type.

### Per member instead of per type

Both steps work on member settings too, so one member can claim a foreign type while every other
value of the same type stays untouched:

```csharp
settings.ConfigureType<Invoice>(ts => ts.ConfigureMember<Money>(nameof(Invoice.Total), ms =>
{
	ms.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
	ms.SetCustomTypeWriter(prep => { /* as above */ });
}));
// {"$type":"invoice","Total":{"$type":"MoneyDto","amount":12},
//                    "Paid":{"$type":"money","Amount":7,"Currency":null}}
```

`Total` and `Paid` have the same type; only `Total` is remapped.

> `$type` must be the first member for other serializers to recognize it — and if you emit `$type`
> without suppressing the built-in one, the output contains it twice.

### How context-local settings combine

A member override, or a `PrepareTypeWriter(configure)` / `AddField(..., configure)` override, states
only what it *changes*. Everything else still comes from the settings configured for the type
itself:

```csharp
settings.ConfigureType<Money>(ts => ts.ConfigureMember<int>(nameof(Money.Amount),
	ms => ms.OverrideName("amount")));

settings.ConfigureType<Invoice>(ts => ts.ConfigureMember<Money>(nameof(Invoice.Total),
	ms => ms.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)));

// {"Total":{"amount":12}}   <- the override changed only the type info handling,
//                              Money's own member configuration still applies
```

Conflicts resolve in favor of the more specific context: the local override wins per field, and per
member name in the case of member configuration.

> **One-level limit.** For a type that references itself, the type's *general* member settings are
> merged in for one nesting level below an override, not indefinitely. That limit is what makes
> writer creation terminate for recursive types.

### Polymorphic values and settings

When a value's runtime type deviates from its declared type — a member declared as `object`, or a
base type holding a derived instance — the serializer writes it with the writer of the **runtime**
type, so the actual members appear instead of an empty `{}`.

Context-local settings follow the value to that runtime type, as far as that is meaningful:

| Setting | Follows a deviating runtime type? |
|---|---|
| `SetDataSelection`, `SetTypeInfoHandling`, `SetEnumAsString`, `SetWriteByteArrayAsBase64String`, `SetTreatEnumerablesAsCollections` | yes — type independent policy |
| `ConfigureMember` entries | yes — a derived type inherits the base type's members; entries naming a member the runtime type does not have simply never match |
| `SetCustomTypeName` | no — the name configured for the declared type would mislabel the deviating one |
| `SetCustomTypeWriter` | no — a custom writer is bound to the type it was written for |

```csharp
settings.ConfigureType<ItemHolder>(ts => ts.ConfigureMember<BaseItem>(nameof(ItemHolder.Item),
	ms => ms.ConfigureMember<int>(nameof(BaseItem.Code), cs => cs.OverrideName("code"))));

// BaseItem    -> {"Item":{"code":3}}
// DerivedItem -> {"Item":{"Extra":9,"code":3}}   <- inherited member is renamed too
```

Writers built for such an override are local to the call site and are not shared through the
per-type cache, so they never leak into how the type is written elsewhere.

### Configuring container elements

`ConfigureElement<TElement>` is to a container what `ConfigureMember` is to an object: it configures
the values a container writes into its JSON array or object, without touching how the same type is
written anywhere else.

```csharp
settings.ConfigureType<List<Item>>(ts => ts.ConfigureElement<Item>(
	es => es.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
```

What counts as "the element" depends on the container:

| Container | Configured value |
|---|---|
| `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `IEnumerable<T>` | `T` |
| `IDictionary<K,V>` / `IReadOnlyDictionary<K,V>` written as a JSON object | `V` — the value, keys are written by the key writer and are not configurable |
| a dictionary whose key cannot become a property name | `KeyValuePair<K,V>`, because it is written as an array of pairs |

`TElement` is verified against the container's actual element type, so a mismatch throws at
configuration time rather than being silently ignored. Element settings behave like member settings
in every other respect: they merge onto the element type's own settings, they follow a deviating
runtime element type under the same rules as the table above, and the writers built for them stay
local to the container.

> Element settings apply to the direct elements only. They do not propagate further down into the
> elements' own members or nested containers.


### How the others do it

**Newtonsoft.Json** — a converter takes over the value completely, so Newtonsoft writes no `$type`
for it. The suppression is implicit, and you emit the discriminator by hand:

```csharp
public override void WriteJson(JsonWriter writer, Money value, Newtonsoft.Json.JsonSerializer s)
{
	writer.WriteStartObject();
	writer.WritePropertyName("$type");
	writer.WriteValue("MoneyDto");          // re-encoded on every call
	writer.WritePropertyName("amount");
	writer.WriteValue(value.Amount);
	writer.WriteEndObject();
}
```

For renaming only, Newtonsoft has a purpose-built hook:

```csharp
public class DtoBinder : ISerializationBinder
{
	public void BindToName(Type type, out string assemblyName, out string typeName)
	{
		assemblyName = null;
		typeName = type == typeof(Money) ? "MoneyDto" : type.FullName;
	}
	public Type BindToType(string assemblyName, string typeName) => ...;
}

settings.SerializationBinder = new DtoBinder();
settings.TypeNameHandling = TypeNameHandling.Objects;
```

FeatureLoom covers that same case directly, without a custom writer:

```csharp
settings.ConfigureType<Money>(ts => ts.SetCustomTypeName("MoneyDto"));
```

`ResolveTypeName` checks the custom name before falling back to `typeNameFormat`, so this is the
equivalent of `BindToName` and is the preferred way when a rename is all you need.

Unlike `BindToName`, it is not limited to a global per-type mapping: set on a member scope it
renames only that member, so the same CLR type can claim different names at different places.

```csharp
settings.ConfigureType<Invoice>(ts => ts.ConfigureMember<Money>(nameof(Invoice.Total),
    ms => ms.SetCustomTypeName("MoneyDto")));   // only Invoice.Total is renamed
```

The two-step pattern above is therefore only needed when the payload has to be *reshaped* along
with the name, not for renaming alone.

**System.Text.Json** — the discriminator is tied to the polymorphism feature (.NET 7+):

```csharp
[JsonDerivedType(typeof(Money), typeDiscriminator: "MoneyDto")]
public abstract class MoneyBase { }
```

This only works for a declared *base* type and its declared subtypes, and the discriminator must be
the first property. To make a non-polymorphic type claim a foreign name you drop to a converter and
write the property by hand, as in the Newtonsoft snippet above. `JsonPolymorphismOptions` in a
custom `IJsonTypeInfoResolver` allows building it dynamically, but still per type, not per member.

**Difference:** all three can emit a foreign discriminator from a converter. What differs is the
cost and the scope. FeatureLoom encodes `"$type":"MoneyDto"` once during preparation and copies the
bytes per value, while both alternatives re-write the property name and value on every call. And
because FeatureLoom's suppression and writer both live in the settings *scope*, the remapping can
be limited to a single member — neither `ISerializationBinder` nor `JsonDerivedType` can do that.

| | Rename a type's discriminator | Claim a non-existing type | Per member |
|---|---|---|---|
| FeatureLoom | `AddNoTypeInfo` + `PrepareTypeInfo` | yes | yes |
| Newtonsoft | `ISerializationBinder` | yes | no |
| System.Text.Json | `JsonDerivedType` (polymorphic types only) | converter only | no |

---

## 6. Applying a writer to more than one type

**FeatureLoom**

```csharp
// all subtypes
settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<BaseItem>((v, i) => v.WriteInt(i.Code)),
	handlesDerivedTypes: true));

// or by convention, e.g. an attribute
settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
	prep => …,
	supportsType: t => t.GetCustomAttribute<CompactAttribute>() != null));
```

The predicate only **widens** the registration — `BaseItem` itself is always covered. It is
evaluated when a type's writer is created, so it is off the write path.

Precedence: a writer registered for the exact type is found by direct lookup and therefore always
beats a predicate match, regardless of registration order. Among predicate matches, the first
registered one wins.

**Newtonsoft.Json**

```csharp
public override bool CanConvert(Type objectType) => typeof(BaseItem).IsAssignableFrom(objectType);
```

Evaluated per converter, per type resolution — and the converter list is scanned in order, so an
exact-type converter registered later loses to a broad one registered earlier.

**System.Text.Json**

```csharp
public class CompactConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type t) => typeof(BaseItem).IsAssignableFrom(t);
	public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o)
		=> (JsonConverter)Activator.CreateInstance(typeof(CompactConverter<>).MakeGenericType(t));
}
```

A factory plus a generic converter plus reflection-based instantiation.

**Difference:** FeatureLoom expresses the same thing as one optional predicate argument, and gives
exact registrations deterministic precedence instead of order-dependent precedence.

---

## 7. Writers as classes, and open generic types

The lambda form cannot express a writer for an *open* generic type such as `Wrapper<>`: there is no
concrete `T` to write the lambda against. For that case a writer is declared as a class instead.

**FeatureLoom**

```csharp
class WrapperWriter<T> : JsonSerializer.CustomTypeWriterDefinition<Wrapper<T>>
{
	protected override CustomWriter<Wrapper<T>> Prepare(WriterPreparationApi api) =>
		api.PrepareObjectWriter<Wrapper<T>>(obj => obj
			.AddField("v", w => w.Value)
			.AddField("tag", w => w.Tag));
}

settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
```

The definition is closed with the generic arguments of the constructed type, instantiated and
prepared **once per constructed type**, when that type's writer is created — never per value. Inside
`Prepare` the full builder API is available, including `AddArray`, `AddObject` and
`PrepareTypeWriter<T>()`, so `Wrapper<Money>` picks up whatever writer `Money` is configured with.

Any number of type parameters is supported, as long as the definition has the same arity as the
configured type definition:

```csharp
class TripleWriter<T1, T2, T3> : JsonSerializer.CustomTypeWriterDefinition<Triple<T1, T2, T3>> { … }
settings.ConfigureGenericType(typeof(Triple<,,>), ts => ts.SetCustomTypeWriter(typeof(TripleWriter<,,>)));
```

The generic arguments are passed on **positionally**, so the definition must declare them in the
same order as the type it writes. `class Swapped<T1, T2> : CustomTypeWriterDefinition<Pair<T2, T1>>`
is rejected with an error naming both types.

Precedence: a writer registered for a *constructed* type via `ConfigureType<Wrapper<int>>` is found
by direct lookup and therefore wins over the open generic registration. Derived types are not
covered — this is an exact match on the generic type definition.

The same class can be registered for a closed type as an instance, which also lets it carry state:

```csharp
settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(new PersonWriter(nameField: "who")));
```

So there are three registration forms, and they divide by capability rather than by taste:

| Form | Use when |
|---|---|
| `SetCustomTypeWriter(prep => …)` | the common inline case; the only form supporting `supportsType` widening |
| `SetCustomTypeWriter(definitionInstance)` | the writer needs constructor state, or the class is shared with an open generic registration |
| `SetCustomTypeWriter(typeof(MyWriter<>))` | open generic types — no instance can exist |

**Newtonsoft.Json**

```csharp
public class WrapperConverter : JsonConverter
{
	public override bool CanConvert(Type t) =>
		t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(Wrapper<>);
	// non-generic body: member access via reflection, or a generic helper dispatched per call
}
```

`CanConvert` gives no typed access to the constructed type, so the converter body is either
reflective or has to dispatch into a generic helper itself.

**System.Text.Json**

```csharp
public class WrapperConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type t) =>
		t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(Wrapper<>);
	public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o)
		=> (JsonConverter)Activator.CreateInstance(
			typeof(WrapperConverter<>).MakeGenericType(t.GetGenericArguments()));
}
```

This is the closest analogue — and essentially what FeatureLoom does internally. The difference is
that you write the factory, the `MakeGenericType` call and the constraint handling yourself, once
per generic type; in FeatureLoom that is the registration itself.

**Difference:** you write only the definition class. Closing it, instantiating it and validating
that it actually writes the registered type is handled by the serializer, with errors that name the
involved types.

---

## Summary

| | FeatureLoom | Newtonsoft.Json | System.Text.Json |
|---|---|---|---|
| Declaration | lambda, inline | converter class | converter class |
| Write-only support | yes | must stub `ReadJson` | must stub `Read` |
| Preparation phase | yes (per type) | no | no |
| Field name encoding | once, at preparation | per write | manual (`JsonEncodedText`) |
| Delimiters | serializer (except raw mode) | you | you |
| Null handling for nested/array fields | automatic | manual | manual |
| Declarative nesting | `AddObject` / `AddArray` | no | no |
| Nested type writer lookup | once, at preparation | per call | cacheable, manually |
| Local settings deviation | `PrepareTypeWriter<T>(configure)`, `AddField/AddArray(…, configure)` | second serializer instance | second options instance |
| Settings on polymorphic values | policy and member rules follow the runtime type | global only | global only |
| Multi-type match | `supportsType` predicate | `CanConvert` | `JsonConverterFactory` |
| Exact-type precedence | deterministic | registration order | registration order |
| Open generic types | `typeof(MyWriter<>)`, typed | reflective converter | hand-written factory |
| Writing buffer slices | `TextSegment` / `ReadOnlySpan<char>` | `string` only | `ReadOnlySpan<char>` |
| Claiming a foreign `$type` | prepared once, per type *or member* | `ISerializationBinder`, per type | `JsonDerivedType`, polymorphic types only |

The trade-off: FeatureLoom's writer API is intentionally **not** symmetric with a general-purpose
converter interface. You describe *what* the JSON looks like and the serializer decides *how* to
emit it. That is what allows the field names to be pre-encoded, the delimiters to be guaranteed and
the reference-tracking decision to be made once instead of per value.
