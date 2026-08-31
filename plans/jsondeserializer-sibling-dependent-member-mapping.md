# JsonDeserializer sibling-dependent member mapping

## Goal

Allow a JSON object member's concrete type to be selected from the value of another field in the same
object, while preserving arbitrary JSON field order and integrating selection into an existing parent
multi-option identification scan whenever the possible parent types are known during reader preparation.

Example:

```csharp
settings.ConfigureType<Envelope>(type =>
{
	type.ConfigureMember<IPayload>(nameof(Envelope.Payload), member =>
	{
		member.AddSiblingFieldTypeMappingOption<string, PayloadA>(
			nameof(Envelope.Kind), kind => kind == "a");
		member.AddSiblingFieldTypeMappingOption<string, PayloadB>(
			nameof(Envelope.Kind), kind => kind == "b");
	});
});
```

The generic parameter order is input first and mapped type second, matching whole-value mapping:
`AddSiblingFieldTypeMappingOption<TValue,TMap>`.

## Non-goals

- Do not introduce a general JSON DOM or buffer complete objects as intermediate object graphs.
- Do not require discriminator fields to appear before dependent members.
- Do not perform one identification scan per dependent member.
- Do not alter existing mapping behavior when no sibling-dependent mapping is configured.
- Do not silently ignore unresolved required member mappings.

## Proposed public API

Add sibling-dependent mapping to `MemberSettings<T>`:

```csharp
public void AddSiblingFieldTypeMappingOption<TValue, TMap>(
	string siblingFieldName,
	Func<TValue, bool> predicate,
	Action<TypeSettings<TMap>> configureInstanceTypeSettings = null)
	where TMap : T;
```

Potential direct-result overload, deferred until the reader-selection variant is stable:

```csharp
public void AddSiblingFieldValueMappingOption<TValue, TMap>(
	string siblingFieldName,
	TryMapValue<TValue, TMap> converter)
	where TMap : T;
```

The first implementation should include only the reader-selection overload. A converter consumes the
sibling value but produces a separate member value, which is useful only for specialized models and adds
lifetime and precedence questions that are not required for the core scenario.

## Core semantics

1. A sibling mapping is scoped to one configured parent member.
2. The configured sibling field belongs to the same JSON object as the dependent member.
3. The sibling field may appear before or after the dependent member.
4. The sibling value is read using the normal prepared reader for `TValue`.
5. A true predicate selects that mapped type immediately for the affected member.
6. False or unreadable values exclude only that mapping option.
7. Options are evaluated in registration order; the first true predicate wins.
8. If the sibling field is absent or no option matches, use an explicitly configured fallback when present.
9. Without a fallback, continue through the member's existing mapping/proposed-type behavior if it can
   produce a valid concrete type; otherwise deserialization fails through the normal exception policy.
10. A valid `$type` inside the dependent member remains authoritative when proposed types are enabled.
11. Mapped option settings are applied through the normal prepared mapped reader.
12. Configuration rejects null/empty sibling names, null predicates, and mapped types not assignable to the
	declared member type.
13. Settings compilation must clone all sibling-selection metadata so later settings mutations do not affect
	an existing deserializer.

## Scan model

### Known concrete parent

When a concrete parent has one or more sibling-dependent members, prepare one parent scan plan. The scan:

1. Starts at the parent object's opening brace under one undo-read handle.
2. Reads field names once.
3. Reads configured sibling discriminator values when encountered.
4. Records selected mapped readers in an object-local selection context.
5. Skips all unrelated values, including dependent member payloads.
6. Continues until every required member selection is resolved or the object ends.
7. Undoes once to the parent-object start.
8. Runs normal parent deserialization, consulting the completed selection context for dependent members.

### Statically configured multi-option parent

Build a union scan plan from every configured parent candidate. It contains:

- Existing parent field-name inference data.
- Existing parent field checkers.
- Sibling selectors required by each possible parent candidate.
- Candidate ownership for every dependent member selector.

A single scan updates parent ratings/checkers and candidate-local member selections. Once the parent is
resolved, selections belonging to other candidates are discarded.

The scan may terminate early only when:

- The parent candidate is decisive, and
- Every sibling-dependent member required by that candidate is resolved or permanently ruled out.

A parent checker match therefore does not necessarily end the scan immediately.

### Dynamically proposed parent type

A `$type` may identify a runtime parent whose sibling scan plan was not part of the prepared finite option
set. If fields needed by that runtime type were encountered before `$type`, the original scan cannot recover
them without buffering.

Initial contract:

- Prepared concrete and multi-option parent types use one unified identification scan.
- An arbitrary `$type`-selected parent may use a second identification scan after resolving the runtime type.
- This fallback must be explicit in code and covered by a scan-count regression test.
- Do not add general field-value buffering solely to avoid this uncommon second scan.

## Prepared architecture

Introduce isolated abstractions rather than extending the current multi-option loop directly.

### `ParentObjectScanPlan`

Immutable preparation-time data:

- Parent candidate descriptors.
- Field-name lookup for parent inference.
- Parent option field checkers.
- Sibling selector dispatch grouped by field name and input type.
- Required dependent-member counts per parent candidate.
- Early-termination rules.

### `ParentObjectSelectionContext`

Per-object runtime state, obtained from a pool and returned after the actual parent read:

- Selected parent candidate.
- Parent inference ratings and exclusion state.
- Selected reader index per dependent member.
- Resolved/excluded state per sibling mapping option.
- Remaining unresolved dependent members for each viable parent candidate.

The context must be stack/scoped so nested and recursive parent objects cannot overwrite each other's
selections. It must be returned in `finally` paths.

### Member reader integration

Prepared member strategies should support a variant that obtains the selected mapped reader from the active
parent selection context. Do not add a per-value settings lookup. Existing member strategies remain unchanged
for members without sibling-dependent mappings.

The selection context should be keyed by a preparation-time integer member slot, not by reflection metadata
or member-name dictionary lookup during actual deserialization.

## Identification-pass accounting

Add an internal test hook or narrowly scoped diagnostic counter that can verify identification scan counts
without exposing mutable production behavior. Prefer an internal counter visible through
`InternalsVisibleTo` or an existing diagnostics mechanism over public API expansion.

Expected counts:

| Scenario | Identification scans | Actual reads |
|---|---:|---:|
| Concrete parent, one or many dependent members | 1 | 1 |
| Prepared multi-option parent with dependent members | 1 | 1 |
| `$type` resolves to an unplanned runtime parent | At most 2 | 1 |
| No parent/member identification required | 0 | 1 |

## Precedence

Highest to lowest:

1. Security policy: forbidden types and whitelist enforcement.
2. Valid member-local `$type`, when enabled and allowed.
3. Successful sibling-dependent selection.
4. Existing member-local single or multi-option mapping.
5. Existing inferred/default member reader behavior.

This precedence must be validated against `ProposedTypeMode.Ignore`, `CheckWhereReasonable`, and
`CheckAlways`.

## Failure behavior

- Predicate exceptions follow `rethrowExceptions` and `logCatchedExceptions` like existing field checkers.
- Failure to deserialize a present sibling value as `TValue` behaves as a false predicate for that option.
- An absent sibling does not itself fail if another valid member resolution path exists.
- Multiple matching options select the first registered option.
- Duplicate sibling fields: the first true match wins; a false result permanently excludes that option for
  the current object.
- If no valid concrete reader can be resolved for a present dependent member, fail rather than silently
  returning `null` or a default value.

## Implementation phases

### Phase A — Settings model and concrete parent

- Add cloneable sibling-selector metadata to `MemberSettings<T>` / `BaseTypeSettings` member configuration.
- Add `AddSiblingFieldTypeMappingOption<TValue,TMap>` with XML documentation and validation.
- Prepare a scan plan for one known concrete parent and one dependent member.
- Add one context-aware member-reader strategy.
- Preserve all existing paths when no sibling selector is configured.

### Phase B — Multiple dependent members

- Support multiple dependent members in one parent scan.
- Group selectors by sibling field so one parsed sibling value can serve multiple dependent members.
- Support different `TValue` types for one sibling field through value-local undo handles.
- Pool per-object selection state.

### Phase C — Prepared multi-option parents

- Refactor existing parent multi-option identification into `ParentObjectScanPlan`.
- Merge parent inference, parent field checkers, and candidate-specific sibling selectors.
- Tighten early termination to include selected parent member-resolution state.
- Ensure selectors from excluded parent candidates no longer block termination.

### Phase D — Proposed runtime parent fallback

- Detect `$type` results whose scan plan was not included in the prepared parent candidate set.
- Perform the documented second identification scan for that runtime parent.
- Preserve security checks and proposed-type precedence.
- Verify at-most-two-scan behavior.

### Phase E — Hardening

- Nested and recursive objects.
- Reference resolution and populate-existing-member behavior.
- Recursive, member-local, element, exact, and open-generic settings interactions.
- Unknown-field policies.
- Snapshot isolation and concurrent deserializer instances.
- Multi-target build and full-suite validation.

## Test matrix

Create `FeatureLoom.Tests/Serialization/JsonDeserializerSiblingDependentMemberMappingTests.cs`.

### Configuration

- Null sibling field name.
- Empty/whitespace sibling field name.
- Null predicate.
- Incompatible mapped type prevented by generic constraint.
- Duplicate options preserve registration order.
- Compiled settings isolated from later configuration mutations.

### Concrete parent and field order

- Sibling before dependent member.
- Sibling after dependent member.
- Dependent member first, sibling last.
- Unrelated primitive, object, and array fields between sibling and member.
- Escaped sibling field names where supported by normal field matching.
- Empty parent object.

### Selection outcomes

- First checker succeeds.
- First checker fails and second succeeds.
- Sibling value unreadable as first `TValue`, readable as another `TValue`.
- Sibling absent with valid fallback.
- Sibling absent without a resolvable member type.
- Present sibling with no matching option.
- Duplicate sibling field after a successful selection.
- Predicate exception with rethrow enabled and disabled.

### Multiple dependent members

- Two members selected by the same sibling field.
- Two members selected by different sibling fields.
- One resolved and one unresolved member.
- Same sibling field with different input types.
- Payloads before all discriminators.
- Verify exactly one identification scan.

### Multi-option parent

- Parent selected by field-name inference; selected candidate has one dependent member.
- Parent selected by checker; scan continues until its dependent member is resolved.
- Parent discriminator before and after member discriminator.
- Parent candidate A and B use different sibling fields.
- A sibling result collected before parent selection is retained for the eventual candidate.
- Results for losing parent candidates are discarded.
- Excluded parent candidates no longer block early termination.
- Parent unresolved/ambiguous at end of object.
- Verify exactly one shared identification scan.

### Proposed types

- Member `$type` overrides sibling-selected reader when enabled.
- Member `$type` ignored when proposed types are disabled.
- Parent `$type` selects a prepared candidate without an extra scan.
- Parent `$type` selects an unplanned runtime type and uses the second-scan fallback.
- Forbidden and non-whitelisted proposed parent/member types remain rejected.
- Verify at most two identification scans for the unplanned runtime parent.

### Settings interactions

- Mapped-option member override names.
- Custom reader on selected member type.
- Recursive settings inherited by selected member type.
- Member-local settings override exact/open-generic/recursive settings.
- Unknown discriminator field with `UnknownFieldPolicy.Throw`.
- Reference handling in selected member objects.
- Populate an existing dependent member where supported.

### Nesting and recursion

- Nested parent objects with independent selection contexts.
- Recursive parent type with different selections at each depth.
- Arrays/lists of parents with different sibling values.
- Failure in a nested object does not leak context into the next item.

### Regression

- Existing parent multi-option inference without sibling mappings is unchanged.
- Existing parent field checker behavior is unchanged.
- Existing whole-value mappings and default string mappings are unchanged.
- Existing member single mapping and proposed-type behavior are unchanged.
- No identification scan is added when the feature is not configured.

## Performance constraints

This feature is behavior-driven, but its architecture must avoid unnecessary overhead:

- No new branch in ordinary member readers; use a prepared strategy variant.
- No settings or reflection lookup during value reading.
- One field-name scan shared by all prepared parent/member selection needs.
- Parse a sibling field once when all consumers use the same `TValue`.
- Pool mutable selection contexts and state arrays.
- Preserve the existing fast path for parents without scan requirements.

After correctness is complete, add or adapt a benchmark only if profiling shows meaningful overhead in the
configured or unconfigured paths.

## Completion criteria

- All phases implemented or explicitly deferred with the supported contract documented.
- Every applicable test matrix section covered through public behavior tests.
- Scan-count tests prove one shared scan for concrete/prepared multi-option parents.
- `$type` fallback tests prove no more than two identification scans.
- Existing deserializer tests remain unchanged and pass.
- Full multi-target solution build succeeds.
- Full `FeatureLoom.Tests` suite passes.
