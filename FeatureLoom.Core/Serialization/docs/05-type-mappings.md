# 05 · Type mappings (reading interfaces, base types and ambiguous JSON)

Tell the deserializer which concrete type to create for an interface, abstract class or `object`
— from configuration, from field names, from field values or from the shape of the value itself.

## Why

JSON from other systems rarely carries .NET type information. A member declared as `IShape`,
`object` or a base class must still become a concrete instance. Competitors require either a
discriminator field in a fixed format or a hand-written converter. FeatureLoom resolves this
declaratively, and the decision is compiled into the reader.

## Single mapping

```csharp
settings.ConfigureType<IShape>(t => t.SetInstanceTypeMapping<Circle>());
```

Every `IShape` is read as `Circle`. Option-local settings can be passed:
`SetInstanceTypeMapping<Circle>(c => c.ConfigureMember<double>(nameof(Circle.Radius), m => m.OverrideName("r")))`.

For generic definitions: `SetInstanceTypeMapping(typeof(MyList<>))`.

⚠ STJ: interfaces cannot be deserialized at all without a converter or `[JsonDerivedType]` plus
a discriminator. Newtonsoft: a converter or `TypeNameHandling` (see the security note on page 06).

## Inference from field names

```csharp
settings.ConfigureType<IShape>(t =>
{
	t.AddInstanceTypeMappingOption<Circle>();
	t.AddInstanceTypeMappingOption<Rectangle>();
});
```

```json
{"Radius": 2}              → Circle
{"Width": 3, "Height": 4}  → Rectangle
```

The deserializer scans the object's field names, selects the option they fit, rewinds once and
reads with the prepared reader of the selected type. No discriminator is needed.

⚠ Neither STJ nor Newtonsoft can infer a type from the set of present fields.

## Discriminator fields at any position

```csharp
settings.ConfigureType<IShape>(t =>
{
	t.AddInstanceTypeMappingOption<Circle, string>("kind", v => v == "circle");
	t.AddInstanceTypeMappingOption<Rectangle, string>("kind", v => v == "rectangle", r =>
		r.ConfigureMember<double>(nameof(Rectangle.Width), m => m.OverrideName("w")));
	t.AddInstanceTypeMappingOption<LegacyShape>();   // fallback via field-name inference
});
```

Rules:
- A checker returning `true` selects its option immediately.
- `false`, or a value not readable as `TField`, excludes only that option.
- An absent field leaves the option eligible for field-name inference.
- The field may appear **anywhere** in the object. The scan continues until it is resolved.
- Checkers for the same field run in registration order; the first `true` wins.
- The discriminator stays a normal field for the selected type.

The predicate is arbitrary code: ranges, prefixes, version numbers (`v => v >= 2`) work too.

⚠ STJ's `[JsonPolymorphic]` supports only exact string/int discriminators under one property name,
declared on the base type, and before .NET 9 the discriminator had to be the first property. Newtonsoft
needs a custom converter that loads the object into a `JObject` first.

## Whole-value mappings

When the *value itself* decides — a string, a number, an array:

```csharp
settings.ConfigureType<object>(t =>
{
	// predicate: inspect as long, then read with the normal int reader
	t.AddInstanceTypeMappingValueOption<long, int>(v => v >= int.MinValue && v <= int.MaxValue);

	// converter: produce the result directly
	t.AddInstanceTypeMappingValueOption<string, ItemId>(
		(string s, out ItemId id) => ItemId.TryParse(s, out id));
});
```

- A **predicate** option rewinds and reads through the prepared reader of the mapped type, so its
  settings apply.
- A **converter** option consumes the value and returns the result, with no second pass.
- Failed or unreadable options fall through to the next one.

⚠ No equivalent in STJ or Newtonsoft other than a converter for the whole declared type.

## Recognizing strings as typed values

With `object` targets, strings that the serializer writes for `Guid`, `DateTime` etc. can be
restored strictly:

```csharp
settings.ConfigureType<object>(t => t.AddDefaultStringValueMappings(
	JsonDeserializer.StringValueMappings.Guid |
	JsonDeserializer.StringValueMappings.DateTimeOffset));
```

Flags: `Guid`, `DateTimeOffset`, `DateTime`, `TimeSpan`, `All`. Off by default. Explicit value
mappings run first; ambiguous strings stay strings.

⚠ Newtonsoft's `DateParseHandling` guesses dates in *all* strings globally, which is a common
source of corrupted string data. FeatureLoom's recognition is opt-in, scoped per type and strict.

## Pitfalls & precedence

1. A valid, allowed `$type` in the payload wins over mappings (see page 06), unless proposed types
   are deactivated for that type (`t.SetProposedTypeHandling(false)`) — then the mappings decide
   alone, whatever the payload claims.
2. Then field checkers and field-name inference, or whole-value options, in registration order.
3. Forbidden-type and whitelist policies apply to every mapped type.
4. Exceptions in predicates/converters follow the deserializer's exception policy
   (`rethrowExceptions`, `logCatchedExceptions`).
5. The identification scan rewinds the input once; for large objects prefer a discriminator
   near the start or a single mapping.
