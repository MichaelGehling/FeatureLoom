# JSON serializer/deserializer settings API consistency

## Goal

Make `JsonSerializer.Settings` and `JsonDeserializer.Settings` predictable and symmetric while breaking changes are still acceptable. Keep configuration callback-based rather than converting settings to a fluent API, because a type-preserving fluent hierarchy would require substantial duplication or self-referential generic base classes.

This work concerns configuration API shape only. It must not add settings lookups or branches to serialization/deserialization hot paths.

## Decisions

### Configuration style

- Keep settings callbacks as the primary composition model:

```csharp
settings.ConfigureType<MyType>(type =>
{
	type.SetDataSelection(...);
	type.ConfigureMember<string>(nameof(MyType.Name), member => member.SetIgnore());
});
```

- Do not make settings methods fluent.
- Do not remove fluent returns from actual builders such as `ObjectWriterBuilder<T>` and `ObjectReaderBuilder<T>`. Those APIs build ordered structures and chaining is useful there; they are not settings APIs.
- Settings mutators consistently return `void`.

### Global configuration

Replace mutable public fields with named methods and read-only properties. Global setters use non-nullable values because global settings always have an effective value.

Example target shape:

```csharp
settings.SetEnumAsString(true);
bool value = settings.EnumAsString;
```

Use PascalCase public properties. Keep mutable storage private/internal. Do not retain the old public fields after migration.

### Scoped inheritance and reset

Scoped type/member/element/recursive overrides use nullable parameters where `null` means “inherit from the broader scope”:

```csharp
type.SetEnumAsString(true);
type.SetEnumAsString(null);
```

- Do not add separate `Reset...` methods.
- Enum options use nullable enum parameters.
- Existing optional booleans become `bool?` where resetting inheritance is meaningful.
- Settings that install objects/delegates already use `null` to remove the override and retain that convention.

### Directional naming

Use explicit read/write names symmetrically:

| Current | Target |
|---|---|
| `JsonSerializer.BaseTypeWriteSettings` | unchanged |
| `JsonSerializer.TypeWriteSettings<T>` | unchanged |
| `JsonSerializer.MemberWriteSettings<T>` | unchanged |
| `JsonSerializer.GenericTypeWriteSettings` | unchanged |
| `JsonSerializer.RecursiveWriteSettings` | unchanged |
| `JsonDeserializer.BaseTypeSettings` | `BaseTypeReadSettings` |
| `JsonDeserializer.TypeSettings<T>` | `TypeReadSettings<T>` |
| `JsonDeserializer.MemberSettings<T>` | `MemberReadSettings<T>` |
| `JsonDeserializer.GenericTypeSettings` | `GenericTypeReadSettings` |
| `JsonDeserializer.RecursiveReadSettings` | unchanged |

`DataSelection` and `DataAccess` remain distinct: writing selects representation/member shape, while reading controls accessible assignment targets.

### Dictionary key naming

Use `ConfigureKey<TKey>` on both serializer and deserializer.

- Serializer callback formats a key to an object property name.
- Deserializer callback parses an object property name to a key.
- XML documentation must clearly state that these APIs apply only to object-shaped dictionaries.
- Remove `ConfigureObjectKey<TKey>` after migrating repository callers.

### Constructor naming

Use `Set...`, because each call replaces one configured constructor:

| Current | Target |
|---|---|
| `AddConstructor` | `SetConstructor` |
| `AddCollectionConstructor` | `SetCollectionConstructor` |
| `AddUntypedCollectionConstructor` | `SetUntypedCollectionConstructor` |

Remove the old names after repository migration. `CustomTypeReader<T>.SetConstructor` remains unchanged.

### Runtime-type facade

Do not expose the implementation-oriented base settings classes through runtime type configuration.

Introduce:

- `JsonSerializer.RuntimeTypeWriteSettings`
- `JsonDeserializer.RuntimeTypeReadSettings`

Target signatures:

```csharp
void ConfigureType(Type type, Action<RuntimeTypeWriteSettings> configure);
void ConfigureType(Type type, Action<RuntimeTypeReadSettings> configure);
```

The facades expose only operations valid without a compile-time `T`. They delegate to the underlying concrete settings object used by compilation.

Expected write facade operations:

- scoped value-shaping setters available on `BaseTypeWriteSettings`;
- `ConfigureRecursively`;
- no typed member/element/key callbacks;
- no typed custom writer callback.

Expected read facade operations:

- scoped read-policy setters available on `BaseTypeReadSettings`;
- `ConfigureRecursively`;
- no typed member/element/key callbacks;
- no typed constructor or custom reader callback;
- runtime-safe mapping methods only where assignability can be validated from `Type` values.

The facade must not create a second settings model; it wraps the same underlying settings instance.

## API consistency rules

1. Mutators start with `Set`, `Add`, `Configure`, or `Clear` according to behavior:
   - `Set`: replace one value;
   - `Add`: append/register another entry;
   - `Configure`: execute a nested callback or install a formatter/parser;
   - `Clear`: remove a collection of registrations.
2. `Set` methods accept nullable values only for inherited scoped settings.
3. `Configure...(null)` removes the nested configuration.
4. `Add` methods reject `null` unless `null` has an explicitly documented meaning.
5. Serializer/deserializer counterparts use the same method name when they represent inverse operations.
6. Public APIs receive XML documentation describing scope, inheritance, and removal behavior.
7. Existing prepared-reader/writer behavior and precedence remain unchanged.

## Implementation phases

### Phase A — Inventory and contract tests

- Inventory every public settings field, property, and mutator on both sides.
- Classify each as global-only, scoped inheritable, type-bound, member-only, resource configuration, security policy, or registration collection.
- Add reflection-based API-shape tests for agreed symmetry and return types.
- Add behavioral tests for nullable scoped reset-to-inherit semantics.

### Phase B — Global methods and properties

- Replace serializer global public fields with private/internal storage, PascalCase read-only properties, and `Set...` methods.
- Do the same for deserializer globals.
- Keep collection registration APIs (`Add...`, `Clear...`) as methods.
- Update internal compilation to use the new storage/properties.
- Migrate all repository object initializers and direct assignments.

Proposed serializer global methods:

- `SetTypeInfoHandling`
- `SetDataSelection`
- `SetReferenceCheck`
- `SetReferenceFormat`
- `SetTypeInfoFormat`
- `SetArrayValueFieldName`
- `SetEnumAsString`
- `SetTreatEnumerablesAsCollections`
- `SetWriteBufferChunkSize`
- `SetTempBufferSize`
- `SetIndent`
- `SetMaxIndentationDepth`
- `SetIndentationFactor`
- `SetWriteByteArrayAsBase64String`
- `SetTypeNameFormat`
- `SetGenericTypeNameFormat`

Proposed deserializer global methods:

- `SetDataAccess`
- `SetReferenceResolutionMode`
- `SetProposedTypeMode`
- `SetBackingFieldMode`
- `SetUnknownFieldPolicy`
- `SetAddCaseVariantsForCustomTypeNames`
- `SetInitialBufferSize`
- `SetCastObjectArrayToCommonTypeArray`
- `SetRethrowExceptions`
- `SetLogCaughtExceptions`
- `SetStrict`
- `SetPopulateExistingMembers`
- `SetUseStringCache`
- `SetStringCacheBitSize`
- `SetStringCacheMaxLength`
- `SetAllowUninitializedObjectCreation`
- `SetTypeWhitelistMode`

Correct the existing `logCatchedExceptions` grammar to `LogCaughtExceptions` / `SetLogCaughtExceptions`.

### Phase C — Scoped nullable setters

- Change inherited serializer scoped setters to nullable parameters.
- Change inherited deserializer scoped setters to nullable parameters.
- Preserve non-nullable parameters where `null` cannot mean inheritance.
- Update merge/copy/recursive equality logic only as required; internal fields are already nullable in most cases.
- Test local value, broader fallback, and reset-to-inherit for type/member/element/recursive scopes.

### Phase D — Directional type names

- Rename deserializer settings classes to the `...ReadSettings` names above.
- Update callback signatures, XML references, tests, documentation, and examples.
- Do not leave obsolete aliases; this is an intentional breaking cleanup.

### Phase E — Naming cleanup

- Rename deserializer constructor methods from `Add...` to `Set...`.
- Rename deserializer `ConfigureObjectKey` to `ConfigureKey`.
- Normalize parameter names (`value` for simple setters; descriptive names where useful).
- Centralize deserializer member validation/configuration in one helper, matching serializer structure.
- Compare serializer/deserializer exception types and messages for corresponding invalid configuration calls and align where practical.

### Phase F — Runtime facades

- Add the write/read runtime-type facade classes.
- Change non-generic `ConfigureType(Type, ...)` overloads to expose facades.
- Ensure repeated runtime configuration edits the existing typed settings instance rather than replacing or downgrading it.
- Test generic/non-generic configuration interoperability, removal with `null`, and invalid generic type definitions.

### Phase G — Documentation and migration

- Update `.github/skills/json-serialization.md` with the finalized settings conventions.
- Update `CUSTOM_TYPE_READERS.md` and other serializer/deserializer documentation.
- Migrate all solution projects, tests, benchmarks, playgrounds, and examples.
- Add a concise breaking-change migration table.

## Explicitly deferred

- Redesigning deserializer strict mode. Keep it global and expose only `SetStrict` in this work.
- Redesigning the inheritance-based relationship between type and member settings.
- Resuming sibling-dependent member mapping.
- Making settings APIs fluent.

## Validation

For each phase:

- run focused settings tests;
- build `FeatureLoom.Core` for all target frameworks;
- migrate and compile all solution callers before proceeding.

Before completion:

- run the full `FeatureLoom.Tests` suite;
- build the full solution, except for any independently confirmed pre-existing playground break;
- verify public API shape with reflection tests;
- verify no new per-value settings lookups or diagnostic/test instrumentation entered hot paths.

## Completion criteria

- Global configuration uses methods consistently on serializer and deserializer.
- Scoped overrides can return to inherited behavior using nullable setters.
- Deserializer settings types have symmetric directional names.
- Constructor and dictionary-key APIs use consistent naming.
- Non-generic type configuration uses explicit runtime facades.
- Deserializer member configuration logic is centralized.
- No deprecated compatibility aliases remain.
- Documentation and repository callers use only the new API.
- Full applicable tests and multi-target builds pass.
