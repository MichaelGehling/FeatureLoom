# JsonDeserializer discriminator-based multi-option mapping

## Goal

Extend multi-option instance mappings with an optional discriminator field while preserving the existing
field-name inference mode. All configuration, lookup construction, mapped-reader creation, and precedence
resolution must happen during reader preparation.

Whole-value mapping complements object-field selection for primitives, arrays, and types represented as
JSON strings. Its generic parameters are input-first: `AddInstanceTypeMappingValueOption<TValue,TMap>`.

## Recommended public API

Add an overload that attaches an optional typed field checker to one mapping option:

```csharp
settings.ConfigureType<IShape>(type =>
{
	type.AddInstanceTypeMappingOption<Circle, string>("kind", value => value == "circle");
	type.AddInstanceTypeMappingOption<Rectangle, string>("kind", value => value == "rectangle", mapped =>
		mapped.ConfigureMember<double>(nameof(Rectangle.Width), member => member.OverrideName("w")));
	type.AddInstanceTypeMappingOption<LegacyShape>();
});
```

The checker field should use the normal prepared reader for `TField`, supporting primitive values,
enums, GUIDs and configured custom readers. Each option retains its existing `TypeSettings<TMap>` scope.

The existing API remains unchanged:

```csharp
type.AddInstanceTypeMappingOption<Circle>();
type.AddInstanceTypeMappingOption<Rectangle>();
```

Options without a checker continue to select by matching object field names. Checked and unchecked
options participate in the same multi-option mapping operation.

## Proposed semantics

1. Existing `$ref` and `$type` handling remains outermost. A valid `$type` therefore takes precedence over
   discriminator selection.
2. Field checkers apply only to JSON object input. Existing unknown-object primitive and array behavior
   remains unchanged.
3. A checker field may appear anywhere in the object. Selection performs one scan under an undo-read
   handle, then invokes the already prepared mapped reader from the original object position.
4. When a checker field is encountered, its value is read through the prepared `TField` reader and the
   predicate is evaluated. If the value cannot be read as `TField`, the checker result is false.
5. Mapped-type settings, recursive context, member/element settings, custom readers, constructors,
   references, proposed types, forbidden types, and whitelist checks continue through the normal mapped
   reader preparation path.
6. Configuration rejects incompatible mapped types, null/empty checker field names, and null predicates.
7. Settings snapshots clone checker configuration and remain isolated from later mutations.
8. Type identification parses the object at most once. The selected type then performs the second and
   only other parse for actual deserialization.

## Whole-value mapping

- A predicate option reads the complete JSON value as `TValue`. A true result selects `TMap`, then the
  normal prepared `TMap` reader deserializes from the original position.
- A converter option uses `TryMapValue<TValue,TMap>` and may directly produce the mapped result. On success,
  the inspected value is consumed and no second deserialization pass is needed.
- An unreadable `TValue`, a false predicate, or a converter returning false excludes that option.
- Options are tried in registration order; the first success wins.
- If no whole-value option succeeds, existing primitive/array/object handling and object-field inference
  remain unchanged.
- Direct converter results do not apply mapped-type deserialization settings because normal mapped-type
  deserialization is bypassed.
- `AddDefaultStringValueMappings` is an opt-in convenience over whole-value conversion. Flags currently
  cover canonical `Guid`, offset-bearing `DateTimeOffset`, ISO-8601 `DateTime`, and constant-format
  `TimeSpan` values. Explicit mappings run first, and ambiguous/unrecognized values remain strings.

## Confirmed checker and inference rules

Selection keeps the existing inference ratings while scanning and applies these option-local rules:

- If an option's checker runs and returns `true`, stop the identification scan immediately and select
  that option. Do not search for another matching checker.
- If an option's checker runs and returns `false`, including failure to read the field as `TField`, that
  option is excluded from inference.
- If the checker field is never encountered, the option behaves exactly like an unchecked option and may
  still win through normal field-name inference.
- Unchecked options always retain the existing field-name inference behavior.
- A checker result therefore has decisive meaning only when its configured field is present.
- While any checked option remains unresolved because its checker field has not yet been encountered, a
  provisional inference winner is not decisive. The scan must continue to the end of the object unless a
  checker returns `true`.
- Inference may finish early only after every checked option has been ruled out. Otherwise an unresolved
  checker could still appear later and either select its option immediately or exclude it from inference.
- At the end of the object, checked options whose fields were absent remain eligible and their accumulated
  inference ratings are evaluated together with unchecked options.

When multiple options check the same field, the value must be consumed only once during the identification
scan. Prepared checker dispatch for that field evaluates options in registration order and the first true
predicate wins. False results exclude only their corresponding options.

When checkers use different `TField` types for the same JSON field, each attempted conversion must use a
local undo around the value so the next checker can inspect the same bytes. After all predicates fail, the
selection scan advances over that value once. This preserves the single identification scan, though using
one field type per discriminator field remains the efficient common case.

### Checker field visibility to the mapped reader

Recommended default: treat it as an ordinary input field. Normal readers skip it when it is not a mapped
member under the default unknown-field policy, while a mapped member can capture it. With
`UnknownFieldPolicy.Throw`, a mapped type must declare/configure the discriminator field.

Automatically hiding the field would require a new contextual field-filter mechanism and would make the
same payload behave differently depending on how the mapped reader was reached.

### Duplicate checker fields in the JSON object

Because a true checker selects immediately, the first occurrence that returns true wins. A false occurrence
excludes that option permanently for the current object; a later duplicate cannot restore it. This keeps
selection deterministic and avoids parsing beyond a decisive match.

## Implementation phases

Status: field-checker, whole-value mapping, default string recognition, shared parsing for compatible checker
options, integration tests, and user-facing documentation are implemented. Splitting the selector into separate
preparation and selection strategies remains deferred until sibling-dependent member mapping establishes the
required reusable abstractions.

### A. Settings and API

- [x] Add immutable/runtime-cloneable field-checker configuration to each `MappedType` option.
- [x] Add `AddInstanceTypeMappingOption<TMap,TField>(fieldName, predicate, configure)`.
- [x] Reuse `MappedType` option settings and prepare one typed checker per checked option.
- [x] Define merge precedence: member/element > exact > open generic > inherited mapping context.
- [x] A more specific multi-option mapping replaces the broader option set as it does today.

### B. Prepared reader creation

- [ ] Split current `CreateMultiOptionComplexTypeReader<T>` into shared option preparation plus selection
  strategies. Explicitly deferred until sibling-dependent member mapping establishes the required abstractions;
  this is structural cleanup rather than a behavior gap.
- [x] Keep the current field-rating selector behavior unchanged.
- [x] Extend the existing selector with a prepared field-to-checker lookup.
- [x] Scan arbitrary field order once with the existing outer undo-read mechanism.
- [x] Read a checker field once when compatible checkers share `TField`; otherwise use value-local undo handles.
- [x] Stop immediately when a checker returns true; mark an option ineligible when its checker returns false.
- [x] Apply accumulated field ratings to unchecked options and checked options whose checker field was absent.
- [x] Suppress the existing inference early-exit while at least one checked option is unresolved. Preserve the
  early-exit optimization only when no unresolved checker can still affect selection.
- [x] Return through the selected cached reader without per-value settings lookup or reflection.

### C. Integration behavior

- [x] Verify `$type` precedence; existing reference tests continue to cover the unchanged cached-reader layer.
- [x] Preserve mapped option local settings, constructors, custom readers, and recursive context.
- [x] Preserve dictionary fallback and unknown-object array/primitive behavior where the selected fallback
  policy permits it.
- [x] Enforce forbidden-type and whitelist checks while preparing every mapped option.

### D. Tests

Create `JsonDeserializerDiscriminatorMappingTests` covering:

- string, enum, numeric, and custom-reader checker values;
- checker field first, middle, and last;
- nested object/array fields before the checker field;
- checker true stops immediately, without evaluating later checkers;
- checker false excludes its option from inference;
- absent checker field leaves its option eligible for inference;
- an early unique inference candidate does not stop scanning while any checker remains unresolved;
- inference can stop early after all checked options have been ruled out;
- checked and unchecked options in the same mapping;
- same field with multiple predicates and with different `TField` types;
- duplicate, missing, null, malformed, and incompatible checker values;
- exact/member/element/open-generic scope and precedence;
- option-local member settings, constructors, custom readers, and recursive settings;
- `$type`, `$value`, `$id`, and `$ref` interaction;
- create-new and populate-existing paths;
- dictionary fallback and `object` mappings;
- forbidden-type and whitelist enforcement;
- snapshot isolation and registration-order independence;
- preparation count and repeated reads to guard preparation-only configuration.

## Documentation

- [x] Add the final API and precedence rules to `CUSTOM_TYPE_READERS.md`.
- [x] Update `.github/skills/json-serialization.md` with field-checker selection and its relationship to
  proposed types and automatic field-name selection.
