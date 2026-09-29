# Serialization docs rewrite — status

Docs live in `FeatureLoom.Core/Serialization/docs/`.

## Done
- README (overview, highlights, index), feature-matrix
- 03 type configuration, 04 custom writers & readers, 05 type mappings
- 06 polymorphism, 07 references, 08 performance (preliminary benchmark numbers)
- Migration from STJ, migration from Newtonsoft
- 09 continuous deserialization (+ feature-matrix section); resync after failed read confirmed by owner;
  STJ `AllowMultipleValues`/`topLevelValues` verified as .NET 9+ via reference-pack XML docs

## Open points
- [ ] **Benchmarks**: clean rerun on a quiet system; add Newtonsoft to ComplexObject/SimpleObject
	  (and ideally other) benchmarks; add SimpleObject serialize report; then update 08 + feature
	  matrix teaser + README highlight.
- [x] 01 Quickstart, 02 Settings model. Verify: layer priority of type-owned config vs. generic definition (02); STJ/Newtonsoft settings-lifecycle comparison row.
- [ ] Verify uncertain statements:
  - case-insensitive member matching, null/default-value omission (migration "gaps"); number-from-string resolved (non-strict mode)
  - `AlwaysReplaceByRef` faster than loop modes; refs must point backwards (07)
  - STJ `AllowOutOfOrderMetadataProperties` statement (08)
- [ ] Compile-check doc snippets (e.g. via a docs-snippet test file).
- [ ] Decide fate of old `CUSTOM_TYPE_READERS.md` / `CUSTOM_TYPE_WRITERS.md` (keep as deep dive or remove).
- [ ] Consider a skill file for serialization docs conventions (page structure, ⚠ competitor notes).
