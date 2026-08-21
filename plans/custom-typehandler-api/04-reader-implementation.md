# Phase 2 — Reader Side (pending)

Part of the [Custom Type Handler API redesign](../custom-typehandler-api-redesign.md).

Status: **not started.** The writer side is done; the deserializer still uses the old model.
The target API is specified in [api shape](02-api-shape.md#reader-side-specification--not-implemented) —
this file tracks what has to happen to get there.

## Goal

Bring the reader to the same level as the writer: a `Prepare*` phase, declarative object/array
builders, `ConfigureType<T>`-scoped registration, and the same precedence rules. Symmetry is a
primary requirement — learning one side should teach the other.

## Current state of the reader

- `JsonDeserializer.ExtensionApi` is **still live** and used by `TypeReaderCreation.cs`
  (`customReader.ReadValue(this.extensionApi)`). It is a separate type from the serializer-side
  one that was deleted; expect it to fold away the same way once the reader gets the
  preparation-API treatment.
- `SetCustomTypeReader` still has three overloads distinguished only by delegate shape
  (`Func<ExtensionApi, T>`, `Func<ExtensionApi, T, T>`,
  `Func<PreparationApi, Func<ExtensionApi, T, T>>`), with populate capability as an invisible
  side effect — the exact problem resolved decision 2 targets.
- `CreateCustomTypeReader<T>` hardcodes `childrenMustWriteRefPath = true` with a TODO,
  pessimizing every custom reader. Resolved decision 3 removes this.

## Tasks

- [ ] Reader preparation API (`ReaderPreparationApi`, `ValueReadApi`, `ReadApi`)
- [ ] `PrepareValueReader` / `PrepareObjectReader` / `PrepareArrayReader` / `PrepareRawReader`
- [ ] `ObjectReaderBuilder<T>`: `Field`, `Construct`, `Populatable`, `OnComplete`
- [ ] Reuse the built-in field-lookup machinery (`itemFieldWritersIndexLookup` +
	  `expectedFieldIndex` fast path) so out-of-order/unknown/missing fields behave as with
	  generated readers
- [ ] Derive `childrenMustWriteRefPath` from declared fields; drop the hardcoded `true`
- [ ] Collapse the three `SetCustomTypeReader` overloads onto the single prepare-based form
- [ ] Make populate capability explicit (`Populatable()` / `ReadOnly()`) instead of implicit
- [ ] Open generic readers: mirror `CustomTypeWriterDefinition<T>` with
	  `CustomTypeReaderDefinition<T>` and the same `ConfigureGenericType(typeof(X<>), …)` form
- [ ] Remove `JsonDeserializer.ExtensionApi` if it becomes unreachable
- [ ] Tests mirroring `JsonSerializerCustomTypeWriterTests`
- [ ] Benchmark: a true apples-to-apples custom-vs-built-in **read** comparison (the existing
	  deserialize pair is not one — see [performance](05-performance.md))

## Open points to settle during implementation

- Whether `ValueReadApi` and `ReadApi` are two types or one with the raw members marked as the
  escape hatch. The writer side chose two (`ValueWriteApi` / `RawWriteApi`) so the value case
  cannot accidentally emit structural tokens; the same argument likely applies.
- Whether the reader needs `PrepareTypeReader<TOther>(configure)`, the mirror of the writer's
  preparation-local settings deviation.
