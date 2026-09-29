# Self-configured JSON types

Status: **implemented** (serializer + deserializer). Open points listed at the end.

## Goal
A type can configure its own JSON (de)serialization through a static method marked with an attribute. The method may be private.
Settings-based configuration takes priority. Settings decide whether the type's own configuration is used.

## API
```csharp
[JsonTypeConfiguration]
static void ConfigureWriting(JsonSerializer.TypeWriteSettings<MyType> s) { ... }

[JsonTypeConfiguration]
static void ConfigureReading(JsonDeserializer.TypeSettings<MyType> s) { ... }
```
- Signature: `static void M(TypeWriteSettings<T>)` / `static void M(TypeSettings<T>)`, where T is the declaring type. The parameter type decides the side. At most one method per side; an invalid signature or a duplicate throws.
- `TypeSelfConfigurationMode typeSelfConfigurationMode` exists on both `Settings` classes. Default: `IgnoreButWarn`.
- `Settings.ApplyTypeSelfConfiguration<T>()` / `(Type)` exists on both sides. It copies the type's own configuration into `typeSettingsDict` upfront, whatever the mode. Existing entries still win.
- Only takes effect when enabled via the mode or `ApplyTypeSelfConfiguration`. This is documented on the attribute.

## Modes
1. `Ignore`: never read, no reflection, no warning.
2. `IgnoreButWarn` (default): not applied. Logs a warning once per type that has a method (one reflection check per type, cold path).
3. `Enabled`: applied.
   - Deserializer: reference resolution is not downgraded to ForceDisabled upfront. The string cache is always created.
4. `EnabledKeepRefTrackingOff`: like `Enabled`, but the upfront ref-tracking downgrade stays. Type-own enabling of reference resolution is then ignored.

## Semantics
- Precedence, highest to lowest:
  1. Local/member override
  2. Settings exact type
  3. Settings generic definition
  4. The type's own configuration
  5. Recursive/ambient settings
- Merge: `settingsEntry.MergeOnto(selfConfig, ignoreMergedFlag: true)`, resolved once per type and cached in `CompiledSettings`.
- No inheritance (`DeclaredOnly`), same as settings entries.
- Generics: resolved on the closed type.
- Nullable: `T?` uses the configuration of `T` on both sides.
- Limitation: custom type names, proposed types and custom writers selected by type predicate are prepared upfront. When they are needed, use `ApplyTypeSelfConfiguration<T>()`.

## Implementation
- `JsonAnnotations.cs`: `JsonTypeConfigurationAttribute`, `TypeSelfConfigurationMode`.
- `TypeSelfConfigurationHelper.cs`: reflection discovery and validation, cached process-wide per type.
- `JsonSerializer.Settings.cs`: mode, `ApplyTypeSelfConfiguration`, `CompiledSettings.TryGetTypeSettings` with self-config, warning and cache.
- `JsonDeserializer.Settings.cs`: mode, `ApplyTypeSelfConfiguration`, `CompiledSettings.TryGetTypeSettings(type, out settings, out fromGenericDefinition)`, and flag handling for reference resolution and the string cache.
- `JsonDeserializer.TypeReaderCreation.cs`:
  - `CreateCachedTypeReader` uses the central lookup.
  - `CreateNullableStructReader<T>` (prerequisite fix: `Nullable<customStruct>` could not be read at all before).

## Tests (all passing)
- `JsonSerializerTypeSelfConfigurationTests` (12)
- `JsonDeserializerTypeSelfConfigurationTests` (9)
- `JsonDeserializerNullableTypeSettingsTests` (4)
- Full suite: 2487 tests, 2482 passed, 0 failed.

## Open points
- [x] Benchmark `TypeSelfConfigurationModeTest` (.NET 10, in-process, single run). Results (Ignore / IgnoreButWarn / Enabled / EnabledKeepRefTrackingOff):
  - Steady_Serialize: 223.6 / 228.7 / 240.9 / 244.4 us
  - Steady_Deserialize: 533.4 / 536.2 / 551.9 / 540.8 us
  - Cold_Serialize: 7320 / 7319 / 7218 / 7320 us
  - Cold_Deserialize: 1936 / 1878 / 1887 / 1866 us
  - Default mode: no relevant cost. Enabled modes: ~+8% steady serialize. Not expected; investigate whether `TryGetTypeSettings` is hit per value on the serializer hot path.
`EnabledKeepRefTrackingOff` was the fastest mode this time, while in the two earlier runs both enabled modes were the slowest. Ignore's StdDev (27.8 us) shows this run was disturbed. Conclusion: most likely noise, no mode-dependent cost; not proven, since the writer comparison test was not written.
- [x] Update `.github/skills/json-serialization.md` with the feature.
- [x] Benchmarks closed: no further benchmarking for this topic (decided by user).
logged exactly once per type (serializer and deserializer, via
