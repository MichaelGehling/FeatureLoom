# 03 · Type configuration

Configure how types, members, collection elements and dictionary keys are written and read,
without touching the types themselves. Optionally, a type can carry its own configuration.

## Why

Real-world models are rarely under your full control: third-party types, shared DTO libraries,
legacy classes. And even your own types often need to be written differently in different places.
FeatureLoom therefore makes **settings the primary configuration source** and resolves them in
layers, merged per option:

```
global settings
  └─ recursive settings of an enclosing scope (ConfigureRecursively)
	  └─ open generic definition (ConfigureGenericType)
		  └─ exact type (ConfigureType<T>)          ← or type-owned config, overridden by this
			  └─ member / element (ConfigureMember / ConfigureElement)
				  └─ context-local override (custom writers, PrepareTypeWriter(configure))
```

Everything is resolved when a type's writer/reader is created, **once per type**. At runtime there
are no per-value lookups.

## Configuring a type

**FeatureLoom** – writing and reading use the same shape:

```csharp
var writeSettings = new JsonSerializer.Settings();
writeSettings.ConfigureType<Customer>(t =>
{
	t.SetEnumAsString(true);
	t.ConfigureMember<string>(nameof(Customer.Email), m => m.OverrideName("mail"));
});

var readSettings = new JsonDeserializer.Settings();
readSettings.ConfigureType<Customer>(t =>
{
	t.ConfigureMember<string>(nameof(Customer.Email), m => m.OverrideName("mail"));
});
```

`Customer` has no attributes and does not know about JSON at all.

**System.Text.Json**

```csharp
// Either attributes on the type…
public class Customer { [JsonPropertyName("mail")] public string Email { get; set; } }

// …or a contract modifier for types you don't own (.NET 7+)
options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
{
	Modifiers =
	{
		ti =>
		{
			if (ti.Type != typeof(Customer)) return;
			foreach (var p in ti.Properties)
				if (p.Name == "Email") p.Name = "mail";
		}
	}
};
```

**Newtonsoft.Json**

```csharp
class Resolver : DefaultContractResolver
{
	protected override JsonProperty CreateProperty(MemberInfo m, MemberSerialization ms)
	{
		var p = base.CreateProperty(m, ms);
		if (m.DeclaringType == typeof(Customer) && m.Name == "Email") p.PropertyName = "mail";
		return p;
	}
}
settings.ContractResolver = new Resolver();
```

⚠ **Competitor limitations**
- Configuration from the outside is only possible through low-level resolver hooks that work on
  *all* types; you filter by `Type` and member name strings yourself.
- `SetEnumAsString` for one type only: STJ needs a `JsonStringEnumConverter` on that type or
  member via attribute; Newtonsoft likewise. There is no "for this type" option setting.

## Members

```csharp
settings.ConfigureType<Order>(t =>
{
	t.ConfigureMember<string>("internalNote", m => m.SetIgnore());         // private field
	t.ConfigureMember<Money>(nameof(Order.Total), m => m.OverrideName("sum"));
});
```

Member names refer to the CLR member. Private fields and auto-property backing fields are
addressed by their clean name. Member settings can hold the full type settings of the member's
type, e.g. `m.SetEnumAsString(true)` for just this member.

⚠ STJ/Newtonsoft: per-member behavior outside of attributes requires the resolver hooks shown above.
Private fields are not visible to STJ at all without `[JsonInclude]` (and only on properties/fields
it already considers).

## Collection elements

```csharp
settings.ConfigureType<Order>(t =>
	t.ConfigureMember<List<OrderItem>>(nameof(Order.Items), m =>
		m.ConfigureElement<OrderItem>(e =>
			e.ConfigureMember<int>(nameof(OrderItem.Quantity), q => q.OverrideName("qty")))));
```

`OrderItem` is written with `qty` only inside `Order.Items`; everywhere else it keeps `Quantity`.

⚠ Neither STJ nor Newtonsoft can apply different settings to the same type depending on where it
occurs, short of a wrapper type or a second serializer instance.

## Dictionary keys (serializer)

```csharp
settings.ConfigureType<Dictionary<Guid, Customer>>(t =>
	t.ConfigureKey<Guid>(k => k.ToString("N")));
```

`SetDictionaryShape` chooses between object shape (`{"key":value}`) and array shape.

## Generic types

```csharp
settings.ConfigureGenericType(typeof(Page<>), t => t.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo));
settings.ConfigureType<Page<Order>>(t => t.SetEnumAsString(true));
```

`Page<Order>` gets both: its own settings are **merged onto** the open generic definition; the
constructed type wins per option it sets explicitly. The same applies to custom writers/readers
(see page 04).

⚠ STJ requires a `JsonConverterFactory` with `MakeGenericType` for anything generic; Newtonsoft a
converter with a reflective `CanConvert`. Neither layers settings of a constructed type onto the
generic definition.

## Recursive settings

Apply type-independent policies to a type **and everything below it**:

```csharp
settings.ConfigureType<AuditLog>(t => t.ConfigureRecursively(r =>
{
	r.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo);
	r.SetEnumAsString(true);
}));
```

Every value reachable from `AuditLog` inherits these, unless its own type/member/element settings
say otherwise. Nested recursive blocks layer onto outer ones. Dictionary keys are excluded.

Available on the writer: data selection, type info handling/format, array value field name,
enum as string, byte arrays as Base64, enumerables as collections, dictionary shape.
On the reader: data access, backing field mode, reference resolution, proposed type handling,
populate as member, object-array casting, string cache, unknown field policy.

⚠ STJ and Newtonsoft have no scoped options at all: an option applies to the whole serializer.

## String deduplication (deserializer)

The deserializer checks string values against a fixed-size cache keyed by their UTF-8 bytes
**before** allocating a `string`. Recurring values (currencies, status codes, country names,
category tags…) are therefore materialized once and shared by all objects.

```csharp
var settings = new JsonDeserializer.Settings
{
    useStringCache = true,        // default
    stringCacheBitSize = 12,      // 2^12 = 4096 entries (default), fixed memory
    stringCacheMaxLength = 128    // longer strings bypass the cache (default)
};

// Only cache where values actually repeat:
settings.ConfigureType<Money>(t =>
    t.ConfigureMember<string>(nameof(Money.Currency), m => m.SetUseStringCache(true)));
settings.ConfigureType<Customer>(t =>
    t.ConfigureMember<string>(nameof(Customer.Email), m => m.SetUseStringCache(false)));

// Or for a whole subtree:
settings.ConfigureType<AuditLog>(t => t.ConfigureRecursively(r => r.SetUseStringCache(false)));
```

Properties of the cache:
- **Bounded memory**: capacity is fixed at construction; entries are replaced by an age-based
  policy, never grown. No unbounded interning.
- **Allocation-free hits**: a hit returns the existing instance without creating a string.
- **Tuned for throughput**, not a perfect hit ratio: direct-index probing with two candidate slots.
- The decision whether a member uses the cache is baked into its reader, so there is no per-value
  check.

Use it for low-cardinality values; switch it off for unique values (ids, free text, hashes), where
it would only add lookups.

⚠ STJ and Newtonsoft allocate every string value anew. Post-hoc deduplication (`string.Intern`,
custom pools in a converter) still pays the allocation and, for `Intern`, keeps the strings forever.

## Attributes for migration

For compatibility, FeatureLoom's own `[JsonIgnore]` and `[JsonInclude]` attributes
(`FeatureLoom.Serialization` namespace) are respected on writing and reading:

```csharp
using FeatureLoom.Serialization;

public class Customer
{
    public string Name { get; set; }
    [JsonIgnore] public string PasswordHash { get; set; }
    [JsonInclude] private string note;   // relevant for PublicFieldsAndProperties mode
}
```

This makes moving an STJ-annotated model mostly a matter of changing the `using`. For anything beyond
ignore/include, use settings or type-owned configuration.

## Type-owned configuration

Settings are the primary source, but a type can also carry its configuration itself: a static
method marked `[JsonTypeConfiguration]` that takes the type's settings object.

```csharp
public class Customer
{
	public string Name;
	public string Email;
	private string passwordHash;

	[JsonTypeConfiguration]
	static void ConfigureWriting(JsonSerializer.TypeWriteSettings<Customer> s)
		=> s.ConfigureMember<string>(nameof(passwordHash), m => m.SetIgnore());

	[JsonTypeConfiguration]
	static void ConfigureReading(JsonDeserializer.TypeSettings<Customer> s)
		=> s.ConfigureMember<string>(nameof(Email), m => m.OverrideName("mail"));
}
```

**It is opt-in per serializer/deserializer:**

```csharp
var s = new JsonSerializer.Settings { typeSelfConfigurationMode = TypeSelfConfigurationMode.Enabled };
```

| `typeSelfConfigurationMode` | Effect |
|---|---|
| `Ignore` | Type-owned configuration is never looked at (no reflection). |
| `IgnoreButWarn` *(default)* | Not applied; a warning is logged once per type that has one. |
| `Enabled` | Applied, overridden per option by settings entries. |
| `EnabledKeepRefTrackingOff` | Like `Enabled`; on the deserializer, a globally disabled reference resolution stays disabled. |

To use the configuration of selected types only, keep the mode at `Ignore` and copy it upfront:

```csharp
settings.ApplyTypeSelfConfiguration<Customer>();
```

Rules:
- The method must be `static`, may be private, and its parameter type must match the declaring
  type exactly. An invalid signature throws when the type's handler is created.
- **Settings win**: a `ConfigureType<T>` entry is merged onto the type-owned configuration, per
  option and per member.
- Not inherited: a derived type does not use the base type's method.
- Closed generics work: `Wrapper<T>` configures `Wrapper<int>`, `Wrapper<string>`, … individually.
- Nullable structs use the configuration of the underlying struct.

⚠ **Competitor comparison.** STJ and Newtonsoft offer attributes, which are always active, cannot be
overridden from settings without resolver hooks, and cannot express anything beyond the attribute
set. FeatureLoom's type-owned configuration is full code (the same API as settings), is off unless
the host opts in, and settings always have the last word.

## Pitfalls & precedence

- Settings are compiled when the `JsonSerializer`/`JsonDeserializer` is constructed. Changing a
  `Settings` object afterwards does not affect existing instances.
- Per option, the more specific layer wins: member/element > exact type (> type-owned) > generic
  definition > recursive scope > global.
- Settings for a base type do not apply to derived types, except for policies transferred to a
  polymorphic runtime writer (see page 06).
- Member names are CLR names, not JSON names.
