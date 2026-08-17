---
applyTo: "FeatureLoom.Tests/**"
---
# Testing

- xUnit, project `FeatureLoom.Tests`, mirroring the `FeatureLoom.Core` folder structure.
- Naming: `<Type><Topic>Tests.cs`, e.g. `JsonDeserializerNumberConformanceTests`.

## Rules
- Every time a mishandled case is found, add a permanent test for it — even if the fix is trivial. The test documents that the behavior stays correct in the future.
- Prefer `[Theory]` + `[InlineData]` for value/format matrices (numbers, escapes, culture-ish formats, boundaries).
- Cover boundaries explicitly: min/max, zero, negative, subnormal doubles, precision limits, empty and overlong strings, malformed input.
- For malformed input assert the actual contract (`TryDeserialize` returning `false` vs. throwing); don't assume.
- Test observable behavior through the public API, not internals, so refactoring of fast paths doesn't break the suite.
- Never weaken or delete an existing test to make a change pass. If a test is genuinely wrong, say so and explain why before touching it.

## Definition of done
The full suite must pass before a change is considered complete. Report the pass/skip/total counts.
