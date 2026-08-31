# Copilot Instructions

FeatureLoom — a multi-target .NET utility library (.NET Framework 4.8, netstandard2.0/2.1, .NET 8, .NET 10).
Solution: `FeatureProjects.sln`.

| Project | Role |
|---|---|
| `FeatureLoom.Core` | main library (serialization, collections, helpers) |
| `FeatureLoom.Tests` | xUnit tests |
| `FeatureLoom.PerformanceTests` | BenchmarkDotNet benchmarks |
| `FeatureLoom.Web`, `.Forms`, `.Deprecated` | peripheral |

## Communication
- Use short efficient language
- Keep explanations short until asked for details
- Report results and facts, not intentions. No restating of the plan before every step.
- State uncertainty explicitly instead of guessing; ask rather than assume when a decision has real consequences.
- Ask if instructions are unclear or if information is missing and cannot easily be found.

## Working rules
- For extensive/complex tasks prepare a plan first (create .md files in /plans folder) and update it as you go. Use the plan to communicate progress and decisions. Split bigger plans into an overview and smaller sub-plans.
- Read before editing; make minimal, targeted changes.
- Keep production hot paths free of diagnostic and test-only instrumentation. Verify behavior through public/configurable seams where possible; if instrumentation is unavoidable, compile it out of production builds.
- Never claim something was measured, tested or verified unless it actually was.
- A negative result (no gain, idea reverted) is a valid outcome — report it rather than forcing a change through.
- Prefer FeatureLoom's own tools over external libraries (e.g. `JsonSerializer` over `System.Text.Json`) unless there is a compelling reason to do otherwise.
- Always add/update tests for new/changed behavior. The test suite must pass before a change is considered complete.
- Add comments for non-obvious decisions. Trivial code does not need comments. Add XML comments for public APIs.
- Propose to add/adapt a skill file for any new domain area. Skills are the primary way to communicate domain knowledge and conventions.

## Token discipline
- Read the specific file range you need, not whole files; use search to locate first.
- Never re-read what is already in context.
- Batch independent tool calls.
- Prefer one targeted benchmark/test run over broad repeated runs.

## Topic instructions
Detailed rules live in `.github/skills/` and load automatically by file scope:
`csharp` · `json-serialization` · `optimization` · `testing`
- Update the instructions if you find them incomplete or unclear. Add a new topic if needed.
