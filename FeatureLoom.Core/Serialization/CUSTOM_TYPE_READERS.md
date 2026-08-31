# JsonDeserializer custom readers and type mappings

`JsonDeserializer.Settings.ConfigureType<T>` configures prepared readers and concrete mappings for a declared
input type. Configuration is compiled when a `JsonDeserializer` is constructed.

## Single and inferred mappings

Use a single mapping when one concrete implementation always applies:

```csharp
settings.ConfigureType<IShape>(type =>
	type.SetInstanceTypeMapping<Circle>());
```

Use multiple options for automatic JSON object field-name inference:

```csharp
settings.ConfigureType<IShape>(type =>
{
	type.AddInstanceTypeMappingOption<Circle>();
	type.AddInstanceTypeMappingOption<Rectangle>();
});
```

## Option-local field checkers

A typed field checker participates in the same multi-option inference operation:

```csharp
settings.ConfigureType<IShape>(type =>
{
	type.AddInstanceTypeMappingOption<Circle, string>("kind", value => value == "circle");
	type.AddInstanceTypeMappingOption<Rectangle, string>("kind", value => value == "rectangle", mapped =>
		mapped.ConfigureMember<double>(nameof(Rectangle.Width), member => member.OverrideName("w")));
	type.AddInstanceTypeMappingOption<LegacyShape>();
});
```

Selection rules:

- A true checker selects its option immediately.
- A false checker or a value unreadable as `TField` excludes only that option.
- If the checker field is absent, the option remains eligible for field-name inference.
- Unresolved checkers keep the object identification scan running because the field may occur later.
- Checkers for the same field run in registration order; the first true result wins.
- A false result remains excluded if the JSON object repeats the field.
- The checker field remains an ordinary field during actual mapped-type deserialization.

The identification scan is undone once, after which the selected prepared reader performs actual
deserialization.

## Whole-value mappings

Whole-value options support primitives, strings, arrays, and objects. Generic parameters are input first and
mapped type second.

A predicate selects a normal mapped reader:

```csharp
type.AddInstanceTypeMappingValueOption<long, int>(
	value => value >= int.MinValue && value <= int.MaxValue);
```

The JSON value is inspected as `TValue`, rewound, and then deserialized through the prepared `TMap` reader.
Mapped option settings therefore apply.

A converter can return the result directly:

```csharp
type.AddInstanceTypeMappingValueOption<string, ItemId>(
	(string input, out ItemId result) => ItemId.TryParse(input, out result));
```

A successful converter consumes the inspected value and skips a second deserialization pass. Because it owns
construction, mapped-type deserialization settings are not subsequently applied. Failed or unreadable options
fall through to later options and existing unknown-value handling.

## Default string recognition

Object values can opt into strict recognition of types that `JsonSerializer` normally writes as strings:

```csharp
settings.ConfigureType<object>(type =>
	type.AddDefaultStringValueMappings(
		JsonDeserializer.StringValueMappings.Guid |
		JsonDeserializer.StringValueMappings.DateTimeOffset |
		JsonDeserializer.StringValueMappings.TimeSpan));
```

Supported flags are `Guid`, `DateTimeOffset`, `DateTime`, `TimeSpan`, and `All`. Recognition is disabled by
default. Explicit whole-value mappings run before built-in recognizers. Ambiguous or unsupported strings remain
strings; URI and enum inference are intentionally not included.

## Precedence and policy

- Valid, allowed `$type` metadata is handled before checker/inference selection.
- Option-local settings are merged through the normal mapped-reader preparation path.
- Forbidden-type and whitelist policies apply to every mapped reader.
- Predicate/converter exceptions follow the deserializer's exception policy.
- Construct a new `JsonDeserializer` after changing `Settings`; existing instances use their compiled snapshot.

## Custom readers

Use `SetCustomTypeReader` for representations that need complete control. Preparation callbacks can compose
normal prepared readers without repeated reflection or settings lookup:

```csharp
settings.ConfigureType<ItemId>(type =>
	type.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
		preparation.PrepareValueReader(api =>
		{
			if (!api.TryReadStringValueOrNull(out string value)) throw new Exception("Expected string");
			return new ItemId(value);
		})));
```

`PreparationApi` also provides prepared object, array, normal, and context-local reader composition. Custom
readers must consume exactly one JSON value.
