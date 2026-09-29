# 06 · Polymorphism, type info & type safety

Restore the concrete type of polymorphic values, with type names that suit your consumers, and
keep control over which types a payload may create.

## Why

A member declared as `object`, an interface or a base class loses its concrete type in plain JSON.
The competitors choose between two extremes:

- **STJ**: closed polymorphism. Every derived type must be registered on the base type
  (`[JsonDerivedType]`), which is impossible for types from other assemblies.
- **Newtonsoft**: open polymorphism via `TypeNameHandling`, which writes assembly-qualified names
  and lets the payload create *any* type, a well-known remote code execution vector.

FeatureLoom is **open and safe by default**: type info is written only where needed, derived types
need no registration, and the deserializer applies payload types only where reasonable, with
forbidden types, whitelist and custom names built in.

## Writing type info

```csharp
var settings = new JsonSerializer.Settings
{
	typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo   // default
};

class Drawing { public IShape Main; public object Extra; public Circle Exact; }

serializer.Serialize(new Drawing { Main = new Circle(), Extra = 42, Exact = new Circle() });
// {"Main":{"$type":"MyApp.Circle","Radius":1},"Extra":42,"Exact":{"Radius":1}}
```

`Exact` gets no `$type` because it matches the declared type. `Extra` is an `int` written as a JSON
number, which the reader restores as a number anyway.

| `typeInfoHandling` | Effect |
|---|---|
| `AddNoTypeInfo` | never; smallest output |
| `AddDeviatingTypeInfo` *(default)* | only where the runtime type differs from the declared one |
| `AddAllTypeInfo` | always; fully self-describing |

All of these can be set per type, per member or for a subtree (`ConfigureRecursively`), see page 03.

⚠ STJ writes a discriminator only for types registered via `[JsonDerivedType]`; an unregistered
subtype is written as its base type (data loss) or throws. Newtonsoft's `TypeNameHandling.Auto`
is the closest analogue, but it is global and always assembly-qualified.

### Shape of type info

| `typeInfoFormat` | Objects | Arrays / primitives |
|---|---|---|
| `InlineForObjects` *(default)* | `{"$type":"X","A":1}` | `{"$type":"X","$value":[1,2]}` |
| `AlwaysEnvelope` | `{"$type":"X","$value":{"A":1}}` | same |
| `OnlyInlineForObjects` | `{"$type":"X","A":1}` | no type info (structure stays plain) |

`arrayValueFieldName = ValueFieldName.Values` writes `$values`, as Newtonsoft does. The deserializer
reads all variants.

### Type names

| `typeNameFormat` | Example | Use when |
|---|---|---|
| `Simplified` *(default)* | `System.Collections.Generic.List<System.String>` | readable, framework independent |
| `FullName` | ``System.Collections.Generic.List`1[[System.String, …]]`` | CLR tooling |
| `AssemblyQualified` | `MyApp.Models.Customer, MyApp` | output must be read by Newtonsoft |

`genericTypeNameFormat` can use a different format for generic types.

Short, stable names independent of namespaces:

```csharp
writeSettings.ConfigureType<Circle>(t => t.SetCustomTypeName("circle"));
readSettings.AddCustomTypeName("circle", typeof(Circle));
```

Or with type-owned configuration (page 03), so the name lives with the type.

Predefined alias sets for the reader: `AddDefaultCustomTypeNames()` (`Int32`, `Guid`, …),
`AddCSharpKeywordTypeNames()` (`int`, `string`, …), `AddCommonCrossLanguageTypeNames()`
(`uuid`, `timestamp`, `duration`, `bytes`, …). This is useful for payloads from non-.NET systems.

⚠ STJ discriminators are per-base-type declarations; the same subtype under two bases needs two
declarations. Newtonsoft needs a custom `ISerializationBinder` for anything but CLR names.

## Reading type info

```csharp
var settings = new JsonDeserializer.Settings
{
	proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckWhereReasonable   // default
};
```

| `proposedTypeMode` | Effect |
|---|---|
| `Ignore` | `$type` is never used; fastest and safest |
| `CheckWhereReasonable` *(default)* | used where the declared type can hold a subtype, i.e. non-sealed reference types (interfaces, abstract/base classes, `object`) |
| `CheckAlways` | evaluated for every value |

Per type: `t.SetProposedTypeHandling(false)` ignores `$type` for that type; type mappings (page 05)
then decide alone.

When no `$type` is present, type mappings (page 05) infer the type from field names, discriminator
values or the value shape. **Type info is optional, not a prerequisite.**

## Type safety

```csharp
var settings = new JsonDeserializer.Settings
{
	typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForProposedTypesOnly
};
settings.AddAllowedNamespacePrefix("MyApp.Models");
settings.AddAllowedType<Money>();
settings.AddForbiddenType(typeof(MyDangerousType));
```

- **Forbidden types**: the settings ship with a predefined list of known dangerous types
  (processes, reflection/assembly loading, threading primitives, …), so a basic level of safety is
  active **out of the box** without any configuration. These types can never be created from a
  payload. Extend the list with `AddForbiddenType`.
- **Whitelist** (disabled by default): `ForProposedTypesOnly` checks only payload-proposed types; `ForAllNonIntrinsicTypes`
  checks every non-intrinsic type the deserializer creates.

⚠ **Newtonsoft**: `TypeNameHandling.Auto/Objects/All` without a restrictive `ISerializationBinder`
allows gadget-chain attacks (e.g. via `System.Windows.Data.ObjectDataProvider`). This is a
documented CVE class. The binder must be written by hand.
⚠ **STJ** avoids the problem by not supporting open polymorphism at all.

## Pitfalls & precedence

- `AssemblyQualified` names differ across target frameworks (`mscorlib` vs. `System.Private.CoreLib`).
- Reading order: valid, allowed `$type` (if enabled for the type) → type mappings → declared type.
