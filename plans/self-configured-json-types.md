# Self-configured JSON types

## Goal
Let a type configure its own JSON (de)serialization through an annotated (possibly private) static method.
Settings-based configuration takes priority. You can turn self-configuration off globally.

## API sketch
```csharp
[JsonTypeConfiguration] // new attribute in JsonAnnotations.cs
static void ConfigureJsonWriting(JsonSerializer.Settings.TypeWriteSettings<MyType> s) { ... }

[JsonTypeConfiguration]
static void ConfigureJsonReading(JsonDeserializer.Settings.TypeSettings<MyType> s) { ... }
```
- Signature: `static void M(TypeWriteSettings<T>)` / `static void M(TypeSettings<T>)`, where T is the declaring type.
  (A parameter is simpler than `Action<Action<...>>`. If you prefer the `Action<...>`-returning form, it is a small change.)
- The parameter type decides which side (writer/reader) a method belongs to. Allow at most one method per side, otherwise throw.
- Generic types: the method is resolved on the closed type, so `TypeSettings<Foo<int>>` works naturally.
- New settings flag on both Settings classes: `bool useTypeSelfConfiguration = true` (name to be decided).

## Merge semantics
`effective = settingsConfigured.MergeOnto(selfConfigured)`, so values from settings win. Precedence, from highest to lowest:
1. Local/member override (existing)
2. Settings exact type
3. Settings generic definition
4. Self-configuration of the type
5. Recursive/ambient (existing)

## Steps
1. Add the attribute plus the `useTypeSelfConfiguration` flag to both Settings classes (with cloning/copying).
2. Add a helper `TryGetSelfTypeSettings(Type)`: reflection (NonPublic|Public|Static, DeclaredOnly), invoke on a fresh settings instance, cache per type (null results cached too).
3. Deserializer: add a central `TryGetConfiguredTypeSettings(Type, out BaseTypeSettings)` (exact -> generic -> merged onto self) and replace the direct `typeSettingsDict` lookups in `CreateCachedTypeReader`.
4. Serializer: do the same in `CreateCachedTypeWriter`.
5. Check the settings pre-processing in the `ExtensiveSettings` ctor (merge of generics / `allTypeSettings`): self settings must go through the same preparation (e.g. member name resolution) or be prepared lazily.
6. Tests: self-config only, settings override a single property, settings unrelated property + self property both apply, flag off, private method, generic type, struct, invalid signature -> exception, round trip.
7. Update `.github/skills/json-serialization.md`.

## Decisions
- No inheritance: settings lookup is exact type -> generic definition only; self-config mirrors that (DeclaredOnly).
- Method form: `static void M(TypeSettings<T>)` == method group convertible to `Action<TypeSettings<T>>`; bind via `Delegate.CreateDelegate` and feed into the same code path as `ConfigureType<T>(Action<...>)`.
- Self-config behaves exactly like a settings entry (incl. recursive settings).

## Verified lookup behavior (must be mirrored)
- Serializer `ExtensiveSettings.TryGetTypeSettings`: exact -> Nullable underlying -> generic definition. Constructed entries pre-merged onto generic definition. No base class / interface lookup.
- Deserializer `CreateCachedTypeReader`: exact -> generic definition (pre-merged in ctor). No Nullable fallback, no base class / interface lookup.
- Deviation between both sides (Nullable) is pre-existing; self-config follows each side's lookup. Fixing the Nullable asymmetry is out of scope unless requested.
- Self-config merge: `settingsEntry.MergeOnto(selfConfig)`; for constructed generic types: exact settings > generic-definition settings > self-config of closed type.

## Risk: deserializer global flags

### Step 0 (first, separate change): Nullable fallback in deserializer — DONE
Finding: deserializer could not read `Nullable<customStruct>` at all (even without settings). Fixed via `CreateNullableStructReader<T>` delegating to the underlying type's reader, so settings of T apply to T?. Tests: `JsonDeserializerNullableTypeSettingsTests` (4 passing).
Original idea: add a central `CompiledSettings.TryGetTypeSettings(Type, out BaseTypeSettings)` mirroring the serializer (exact -> Nullable underlying -> generic definition), use it in `CreateCachedTypeReader`, add tests (ConfigureType<MyStruct> applies to MyStruct? members/values).

### Generics
Self-config is resolved on the closed runtime type; no separate generic-definition mechanism needed.

### Flag analysis (B = late update)
- `referenceResolutionMode` DisabledByDefault -> ForceDisabled (ctor) and `refResolutionEnabled`: readers built before discovery already skip ref-path tracking; flipping later gives inconsistent reader graph -> unsafe. Proposal: when self-config is on, do not downgrade to ForceDisabled (per-type check stays; perf impact to be measured).
- `anyUsesStringCache`: late lazy creation of `stringCache` is safe (null-checked usage). Proposal: create lazily on discovery.
- `anyAllowsProposedTypes` / custom type names: self-configured custom type names are only known after the type was touched; cannot be proposed before. Proposal: register on discovery, document limitation.

### Decision: 3-way mode (replaces bool flag)
`TypeSelfConfigurationMode` on both Settings classes (names tentative):
1. `Ignore` - self-config never read.
2. `Enabled` - self-config applied; ref-tracking not downgraded to ForceDisabled upfront. XML doc: performance cost when no type needs refs.
3. `EnabledKeepRefTrackingOff` - self-config applied, but if the upfront scan forced ref resolution off, a self-config `enableReferenceResolution` is ignored. XML doc: inconsistent behavior.
- String cache: created lazily on discovery (verify null-safety).
- Proposed types / custom type names: XML doc limitation; solution = new `Settings.ApplyTypeSelfConfiguration<T>()` / `(Type)` that copies self-config into `typeSettingsDict` upfront (existing settings entries still win).
- Serializer: check for a similar upfront downgrade; otherwise modes 2/3 behave identically there.
- Default mode: `IgnoreButWarn`. Final 4-way enum:
  1. `Ignore` - never read, no reflection, no warning (for intentionally ignoring type-own config).
  2. `IgnoreButWarn` (default) - not applied; on first reader/writer creation per type, detect an annotated method and log a warning once. Cost (one reflection check per type, cold path) to be measured.
  3. `Enabled` - applied; no upfront ref-tracking downgrade (perf cost documented).
  4. `EnabledKeepRefTrackingOff` - applied; upfront ref-tracking downgrade kept, self-config ref enabling ignored (inconsistency documented).
Deserializer `ExtensiveSettings` ctor precomputes `anyTypeHasReferenceResolutionEnabled`, `anyUsesStringCache`, `anyAllowsProposedTypes` from all type settings. Lazily discovered self-config would not affect these. Options: (a) update flags when self-config is resolved (only safe before first read uses them), (b) conservatively treat those flags as enabled when self-config is on, (c) scan on first discovery and rebuild. Decision needed.
