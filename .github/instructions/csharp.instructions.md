---
applyTo: "**/*.cs"
---
# C# Code Style

- Follow the conventions of the file being edited; consistency with the surrounding code beats any general preference.
- Minimal diffs. Don't reformat, reorder or "tidy" code you weren't asked to change.
- Comments explain *why*, not *what*. Non-obvious decisions (a fast path, a workaround, a deliberate trade-off) deserve one; trivial code does not.
- No new dependencies and no library version bumps unless there is no reasonable alternative.
- Respect multi-targeting (.NET Framework 4.8, netstandard2.0/2.1, .NET 8, .NET 10). Guard newer BCL APIs with `#if` and provide a working fallback.
- Public API additions get XML doc comments.
- Verify with a build before claiming a change is done.
