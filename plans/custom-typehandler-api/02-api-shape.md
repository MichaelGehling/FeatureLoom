# Custom Type Handler API — Intended API Shape

Part of the [Custom Type Handler API redesign](../custom-typehandler-api-redesign.md).

The shape both sides are built against. The writer side has since been implemented and
deviates in places — where it does, [writer implementation](03-writer-implementation.md) is
authoritative and this file records the original intent. The reader side is **not yet
implemented**, so its section below is still the specification.

## Two phases, builders chosen by output shape

Phase 1 is a builder chosen by output shape. Each builder returns the phase-2 delegate.
Same vocabulary on both sides — only `Read`/`Write` and the direction differ.

```csharp
// Writer
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
	prep.PrepareValueWriter<Money>((w, money) => w.WriteString(money.ToString()))));

// Reader — mirror image
settings.ConfigureType<Money>(ts => ts.SetCustomTypeReader(prep =>
	prep.PrepareValueReader<Money>(r => Money.Parse(r.ReadString()))));
```

| Builder | Output shape | Wrapper it implies |
|---|---|---|
| `PrepareValueWriter` / `PrepareValueReader` | single JSON value | primitive |
| `PrepareObjectWriter` / `PrepareObjectReader` | `{ ... }` | object |
| `PrepareArrayWriter` / `PrepareArrayReader` | `[ ... ]` | array |
| `PrepareRawWriter` / `PrepareRawReader` | anything | none (user writes delimiters) |

The `Prepare*` prefix is deliberate: these methods do not read or write anything themselves,
they **build** the handler that is used later on every value. The longer name keeps that
two-phase nature visible at the call site.

## Writer side (as originally specified)

> Implemented, with changes: `Field`/`FieldIf`/`Raw` became `AddField`/`AddRawField`, and
> `AddObject`/`AddArray` were added. See [writer implementation](03-writer-implementation.md).

```csharp
// on TypeWriteSettings<T>
public void SetCustomTypeWriter(Func<WriterPreparationApi, Action<T>> prepare);

public sealed class WriterPreparationApi
{
	public Action<T> PrepareValueWriter<T>(Action<ValueWriteApi, T> write);
	public Action<T> PrepareObjectWriter<T>(Action<ObjectWriterBuilder<T>> build);
	public Action<T> PrepareArrayWriter<T, TItem>(Func<T, IEnumerable<TItem>> getItems);
	public Action<T> PrepareRawWriter<T>(Action<RawWriteApi, T> write);

	// nested handler for another type, resolved once
	public Action<TOther> PrepareTypeWriter<TOther>();
}

public sealed class ObjectWriterBuilder<T>
{
	/// <summary>
	/// Declaring fields in the order they appear in the JSON is faster.
	/// </summary>
	public ObjectWriterBuilder<T> Field<TField>(string name, Func<T, TField> getValue);
	public ObjectWriterBuilder<T> FieldIf<TField>(string name, Func<T, TField> getValue, Func<T, bool> condition);
	public ObjectWriterBuilder<T> Raw(Action<RawWriteApi, T> write);
}
```

`FieldIf` cannot use the merged-comma trick (the preceding field may be absent), so it falls
back to an explicit comma. Documented, and a reason to prefer `Field` where possible.

## Reader side (specification — not implemented)

```csharp
// on TypeSettings<T> — replaces the three implicit SetCustomTypeReader overloads
public void SetCustomTypeReader(Func<ReaderPreparationApi, Func<ReadApi, T, T>> prepare);

public sealed class ReaderPreparationApi
{
	public Func<ReadApi, T, T> PrepareValueReader<T>(Func<ValueReadApi, T> read);
	public Func<ReadApi, T, T> PrepareObjectReader<T>(Action<ObjectReaderBuilder<T>> build);
	public Func<ReadApi, T, T> PrepareArrayReader<T, TItem>(Func<IEnumerable<TItem>, T> create);
	public Func<ReadApi, T, T> PrepareRawReader<T>(Func<ReadApi, T, T> read);

	public Func<TOther, TOther> PrepareTypeReader<TOther>();   // already exists
}

public sealed class ObjectReaderBuilder<T>
{
	/// <summary>
	/// Declaring fields in the order they appear in the JSON is faster: the reader probes the
	/// next expected field first and only falls back to a name lookup on a miss.
	/// </summary>
	public ObjectReaderBuilder<T> Field<TField>(string name, Action<T, TField> setValue);
	public ObjectReaderBuilder<T> Construct(Func<T> create);
	public ObjectReaderBuilder<T> Populatable();
	public ObjectReaderBuilder<T> OnComplete(Action<T> postProcess);
}
```

Populate support, made explicit (resolved decision 2):

```csharp
// Cannot populate: always constructs a new instance.
prep.PrepareValueReader<Money>(r => Money.Parse(r.ReadString()));

// Can populate: reuses the passed-in instance when the deserializer offers one.
prep.PrepareObjectReader<Person>(o => o
	.Field("name", (p, string v) => p.Name = v)
	.Populatable());          // opt-in, visible at the call site
```

Ref-path/no-ref derivation is internal: the object builders know their declared field types,
so they compute `allFieldsNoRefs` exactly like `CreateTypedComplexItemHandler` does today. The
value builders force no-ref, matching today's `ForceNoRefTypes()` for primitives. `Raw*` must
assume refs are possible, since nothing is declared.

## Registration: one concept, two matching strategies

All custom handlers are registered through `ConfigureType<T>` / `ConfigureGenericType`.
`AddCustomTypeHandlerCreator` disappears as a public concept — including for predicates.

Predicate matching is **kept**, not dropped. An earlier draft proposed dropping it on the
grounds that a dictionary cannot express a predicate. That reasoning was wrong in the part
that mattered: the cost. `CreateCachedTypeWriter` already scans `settings.itemHandlerCreators`
linearly **once per type**, and the resulting writer is cached. So a predicate scan is not new
overhead — it is the overhead that exists today, already off the hot path.

Resolution order when creating a handler for a type:

1. Custom writer set without a predicate — stored in the type settings, found by direct
   lookup, so it wins regardless of registration order.
2. Custom writers set with a `supportsType` predicate, in registration order — linear scan.
3. Built-in handler.

Specific beats general, and declaration order decides only among predicate matches. Precedence
is structural: it falls out of the lookup order in `JsonSerializer.CreateCachedTypeWriter`, so
no marker interface or two-pass scan is needed.

Assignability is not a separate mode: the predicate `type => typeof(T).IsAssignableFrom(type)`
expresses it. Optimizations that must not bypass a custom writer ask
`CompiledSettings.HasCustomWriterFor(type)`, which consults both stores.

```csharp
// exact type: the normal case
settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => ...));

// convention-based: one rule, many types
settings.ConfigureType<object>(ts => ts.SetCustomTypeWriter(
	prep => prep.PrepareValueWriter<object>((w, v) => w.WriteString(v.ToString())),
	type => type.IsDefined(typeof(JsonAsStringAttribute), true)));
```

`T` names the type the prepared writer accepts; with a predicate it acts as the upper bound
every matched type must be assignable to. Supplying a predicate *widens* the registration:
`T` itself is always covered by the direct lookup, the predicate only adds further types. A
predicate is therefore never a way to exclude `T`.
