# Custom Type Handler API — Redesign (Overview)

Redesign of the `JsonSerializer` / `JsonDeserializer` custom handler API, optimizing for
usability first and performance second. A full break with the old API was accepted.
Supersedes the discarded alignment plan.

**Status: writer side complete, reader side pending.**

## Sub-plans

| # | Topic | Status |
|---|---|---|
| 1 | [Design rationale](custom-typehandler-api/01-design-rationale.md) — goals, what was kept, resolved decisions | settled |
| 2 | [API shape](custom-typehandler-api/02-api-shape.md) — builders, registration, reader specification | writer done, reader spec |
| 3 | [Phase 1 — Writer side](custom-typehandler-api/03-writer-implementation.md) | **complete** |
| 4 | [Phase 2 — Reader side](custom-typehandler-api/04-reader-implementation.md) | **not started** |
| 5 | [Performance](custom-typehandler-api/05-performance.md) — baseline, re-measurement | target partly missed |

User-facing documentation: `FeatureLoom.Core/Serialization/CUSTOM_TYPE_WRITERS.md`.

## The idea in short

A custom handler is registered per type and built in **two phases**:

- **Phase 1** runs once per type. Field names get UTF-8 encoded, nested type handlers get
  resolved, delegates get built.
- **Phase 2** is the returned delegate, invoked per value. It does nothing but read/write.

The phase-1 builder is chosen by output **shape** (value / object / array / raw), which is what
lets the serializer keep control of delimiters, type info and reference tracking while the user
only describes content.

```csharp
var settings = new JsonSerializer.Settings();
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<Money>((value, m) => value.WriteString($"{m.Amount} {m.Currency}"))));

settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
```

## Where things stand

Done:

- Writer side implemented for closed **and** open generic types.
- Single registration entry point (`ConfigureType<T>` / `ConfigureGenericType`); the old
  creator-interface surface is gone.
- `IWriter` removed; the low-level writer is internal-only.
- Old type-handler plumbing deleted as dead code (`ExtensionApi`, `GenericTypeHandlerCreator`,
  `ICachedTypeHandler`, `JsonDataTypeCategory`, `TryCreateItemHandlerDelegate<T>`).
- Benchmarked against the recorded baseline.

Open:

1. **Reader side** — the whole of [phase 2](custom-typehandler-api/04-reader-implementation.md).
   `JsonDeserializer.ExtensionApi` is still live and will likely fold away the same way the
   serializer's did.
2. **Object-builder performance gap** — custom object writing is still ~4–13% slower than the
   generated handler, and the last run was too noisy to resolve it. Decide whether to chase it
   or accept it as the price of the declarative form. See
   [performance](custom-typehandler-api/05-performance.md).

## Acceptance set

`FeatureLoom.Tests/Serialization/JsonSerializerCustomTypeWriterTests.cs` and
`JsonSerializerPrimitiveTests.cs` double as the usability test: if a scenario becomes awkward
to express, the API is wrong, not the test. (`Playground/JsonTest.cs` was dropped rather than
ported — it was the last consumer of the legacy creator API.)
