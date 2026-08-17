---
applyTo: "FeatureLoom.PerformanceTests/**"
---
# Performance Optimization

## Workflow (do not skip steps)
1. Find or create a benchmark that exercises the target code.
2. Measure the baseline.
3. Only then change production code.
4. Re-run the *same* benchmark and report Before / After / Delta including allocations.

You may propose an optimization from reading code alone, but ask for measurement first.

## Benchmarks
- `FeatureLoom.PerformanceTests`, BenchmarkDotNet, run via `BenchmarkSwitcher` in `Program.cs`.
- Check for an existing benchmark before creating one; the JSON ones live in `FeatureLoom.PerformanceTests/JsonSerializer/`.
- Always `[MemoryDiagnoser]`. Time and allocations must be read together — a variant can be the lowest allocator and still be slower.
- Compare against System.Text.Json, SpanJson and Newtonsoft where a comparison already exists.
- Never modify an existing benchmark's logic or setup while optimizing; that invalidates the comparison. New benchmarks may be iterated on until their first successful run, then treated as frozen.

## Benchmark design pitfalls (learned the hard way)
- Don't replay a small dataset many times: values that are unique in reality start repeating and flatter any cache.
- Build the dataset in `[GlobalSetup]`; keep the measured loop free of setup work.
- Prefer one realistic workload size over a parameter sweep. Sweeps mostly add noise and unreadable tables.
- Benchmarks run in a separate worker process, so `Console.WriteLine` from a benchmark lands in the middle of progress output and statics don't reach the host. Use `DeferredReport.Collect(...)`; `Program.Main` prints it after the summary.

## Reporting
- Present results as a markdown table.
- Explain the *mechanism*, not just the numbers (e.g. string-cache hit ratio explains why selective caching wins).
- Report regressions honestly, including allocation regressions.
- Revert changes that don't beat the noise floor. "No measurable gain" is a valid, final result.
