# Normalize JSON serializer benchmarks

- [ ] Inventory benchmark classes with repeated single-value and array workloads.
- [ ] Identify valid logical groups and one baseline per group.
- [ ] Apply 25/100 iteration limits, `OperationsPerInvoke`, categories, and category grouping where appropriate.
- [ ] Remove only redundant `IterationSetup` methods whose stream resets already occur inside measured loops.
- [ ] Build the performance project and fix validation errors.
