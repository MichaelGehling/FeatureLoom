# 07 · References

Handle shared and circular object references: write each object once, break cycles, and restore
object identity when reading.

## Why

Object graphs are not always trees. Without reference handling, shared objects are duplicated and
cycles cause stack overflows. The competitors offer one all-or-nothing mode (`$id` on *every*
object) plus "ignore cycles". FeatureLoom offers finer modes, a compact path-based format, and lets
the reader resolve references only for the types that need it.

## Writing

```csharp
var settings = new JsonSerializer.Settings
{
	referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
	referenceFormat = JsonSerializer.ReferenceFormat.JsonPath          // default
};

var ann = new Customer { Name = "Ann" };
var orders = new[] { new Order { Customer = ann }, new Order { Customer = ann } };
serializer.Serialize(orders);
// [{"Customer":{"Name":"Ann"}},{"Customer":{"$ref":"$[0].Customer"}}]
```

| `referenceCheck` | Duplicates | Cycles | Notes |
|---|---|---|---|
| `NoRefCheck` *(default)* | written again | stack overflow | fastest; only for trees |
| `OnLoopThrowException` | written again | exception | detect unexpected cycles |
| `OnLoopReplaceByNull` | written again | `null` | no ref syntax in output |
| `OnLoopReplaceByRef` | written again | `$ref` | only when duplicates must stay separate instances |
| `AlwaysReplaceByRef` | `$ref` | `$ref` | preserves identity; often *faster* than loop modes on acyclic graphs because output shrinks |

| `referenceFormat` | Output |
|---|---|
| `JsonPath` *(default)* | `{"$ref":"$.Items[0]"}`; no extra members on referenced objects |
| `IdBased` | `$id` on every tracked object, `{"$ref":"1"}`; arrays as `{"$id":"1","$values":[…]}`; STJ/Newtonsoft compatible |

⚠ **Competitors**: STJ `ReferenceHandler.Preserve` and Newtonsoft `PreserveReferencesHandling` only
support the `$id` format. It tags *every* object, even those never referenced, which inflates
output and makes it harder to read. Neither offers "break cycles but keep duplicates"
(`OnLoopReplaceByRef`) or "throw on cycles" as an explicit mode (STJ throws only as a side effect
of max depth).

## Reading

```csharp
var settings = new JsonDeserializer.Settings
{
	referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.DisabledByDefault   // default
};
settings.ConfigureType<Customer>(t => t.SetReferenceResolution(true));
```

| `referenceResolutionMode` | Effect |
|---|---|
| `ForceDisabled` | never resolve; lowest overhead, even per-type settings are ignored |
| `DisabledByDefault` *(default)* | resolve only for types/members that enable it |
| `EnabledByDefault` | resolve for all reference types (except strings); disable per type where not needed |

Both formats (`JsonPath` and `$id`) are read. JSON written by STJ or Newtonsoft with preserved
references can therefore be read directly.

⚠ STJ and Newtonsoft enable reference handling globally. Paying the tracking cost only for the
few types that are actually shared is not possible.

## References inside custom handlers

Custom writers and readers keep reference handling intact when they use the builder APIs. See
[page 04](04-custom-writers-readers.md) (section *References*).

## Pitfalls

- `NoRefCheck` on a cyclic graph ends in a stack overflow. Use it only for known trees.
- `OnLoopReplaceByRef` pays full tracking cost but outputs the same as `NoRefCheck` on acyclic
  graphs. Prefer `AlwaysReplaceByRef` unless duplicates must stay separate.
- `JsonPath` refs are FeatureLoom-specific. Use `IdBased` when STJ/Newtonsoft must read the output.
- Reading references requires the referenced object to appear earlier in the document.
- With `TypeSelfConfigurationMode.Enabled`, the deserializer does not downgrade reference
  resolution to `ForceDisabled` automatically, because a type's own configuration might enable it.
  Use `EnabledKeepRefTrackingOff` to keep that optimization.
