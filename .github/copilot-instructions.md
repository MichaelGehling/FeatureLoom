# Copilot Instructions

## Project Guidelines
- For xUnit `[InlineData]` with nullable decimal tests, use `double?` inputs and cast to `decimal?` inside the test to avoid binding issues.
- When adding new tests, ensure all referenced helper types (e.g., Node/NodeList) are defined in the same test class or moved to an existing test file where those types already exist.
- When adding negative deserialization tests for abstract types, ensure the helper type is truly declared `abstract` so the test validates the intended behavior.
- Keep extension classes lightweight and move complex logic into dedicated helper classes.
- For cloning/deserialization in this codebase, readonly instance fields should be written (do not skip IsInitOnly fields), while static fields should still be ignored. Note that init-only properties report `PropertyInfo.CanWrite == true` in this codebase/runtime context.
- Avoid redundant hash precomputation in `ByteSegment` usage: `GetHashCode()` already computes/caches hash, and `Equals(ByteSegment)` utilizes the cached hash for a fast path.
- Prioritize runtime performance in all implementations, especially in `ReadStringBytes`, avoiding optimizations that could sacrifice speed.
- Do not apply `map_IsFieldEnd` ref-based lookup optimization, as call sites are single non-loop lookups and likely not beneficial.
- For coverage work, prefer tests-only changes (no production code changes) and prioritize low-hanging-fruit coverage first across related files. Stop `ArraySegmentBuilder` topics and continue with `FeatureJsonDeserializer` tests only.
- When validating reference-resolution tests, treat assignment to `object` as type-compatible; use a truly incompatible target type for negative compatibility cases.
- In this codebase, `Deserialize_*_FromArrayOfKeyValuePairs_PublicFieldsAndProperties` should not be expected to pass because `KeyValuePair<TKey,TValue>` properties are not writable in `PublicFieldsAndProperties` mode.
- For `Settings_AddCustomTypeReader_UsesTryReadNullValue`, use a struct (`CustomNullReadType`) so `api.TryReadNullValue()` can be asserted meaningfully; a class result may be `null` before custom-reader state is observable.

## Parser Guidelines
- When proposing parser fast-path substitutions, only mark call sites as safe if they preserve whitespace tolerance (e.g., account for pretty-printed JSON whitespace after '{', ',', or ':').

## General Guidelines
- Avoid using phrases like "take a deep breath" in responses.
- When evaluating parser buffer helpers, explicitly distinguish normal state from EOF rollback state (e.g., BufferReadTillEnd) to avoid contradictory guidance.
- Prefer direct in-file code edits in responses instead of git patch format when asking to change uncommitted code.
- Use properly separated code blocks and ensure all markdown code fences are correctly closed.
- Ensure markdown code blocks are clearly separated from surrounding text, with correct fence closure and no trailing text attached.