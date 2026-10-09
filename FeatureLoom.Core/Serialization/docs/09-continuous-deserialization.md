# 09 Continuous deserialization

A `JsonDeserializer` instance can be **bound to a data source** and then read value after value
from it – NDJSON logs, concatenated JSON values on a socket, message streams, or large files
processed record by record. The buffer, readers and string cache stay warm between the values.

## API

| Member | Purpose |
|---|---|
| `SetDataSource(Stream)` | Bind a stream. Data is pulled in chunks into the internal buffer when needed. |
| `SetDataSource(string)` / `(byte[])` / `(byte[], offset, count)` / `(ByteSegment)` / `(JsonFragment, …)` | Bind in-memory JSON (UTF-16 string or UTF-8 bytes). |
| `TryDeserialize<T>(out T)` / `TryDeserialize(Type, out object)` | Read the **next** value from the bound source. Returns `false` if no value is left or reading failed. |
| `IsAnyDataLeft()` | Skips whitespace and tells whether another (non-whitespace) value follows. For streams it pulls more data if the buffer is exhausted. |
| `TryDeserializeAsync<T>()` / `TryDeserializeAsync<T>(Stream)` | Async variant of `TryDeserialize`; returns `(bool success, T item)`. See [Async reading](#async-reading). |
| `IsAnyDataLeftAsync()` | Async variant of `IsAnyDataLeft()`. Completes synchronously if a value is already buffered. |
| `SkipBufferUntil(delimiter, alsoSkipDelimiter, out found)` | Advances to the next occurrence of a UTF-8 delimiter (e.g. `"\n"`); optionally consumes the delimiter itself. Works across buffer/chunk boundaries. |
| `ShowBufferAroundCurrentPosition(before, after)` | Diagnostic snippet of the buffer around the current read position. |

The `TryDeserialize(json, out …)` / `TryDeserialize(stream, out …)` overloads are shortcuts
that call `SetDataSource` and read the first value. Further values can be read afterwards with the
source-less overloads.

## Reading a sequence of values

Values may be separated by any whitespace (or nothing at all for objects/arrays):

```csharp
var deserializer = new JsonDeserializer();
deserializer.SetDataSource(stream);

while (deserializer.IsAnyDataLeft())
{
	if (!deserializer.TryDeserialize(out Order order)) break;
	Process(order);
}
```

## NDJSON with error recovery

NDJSON (JSON Lines) is simply a sequence of values separated by `\n`, so the loop above already
reads it. Setting `inputFormat = InputFormat.JsonLines` adds automatic **resynchronization**: when
a line is malformed, `TryDeserialize` returns `false` and the next call continues with the next line
instead of the failure position.

```csharp
var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
{
	inputFormat = JsonDeserializer.Settings.InputFormat.JsonLines
});
deserializer.SetDataSource(stream);

while (deserializer.IsAnyDataLeft())
{
	if (deserializer.TryDeserialize(out LogEntry entry)) Process(entry);
	// else: broken record, already skipped
}
```

The matching output is produced with `JsonSerializer.Settings.formatting = JsonFormatting.JsonLines`
(compact JSON plus one `\n` after every root value, also for string results).

Manual resynchronization is possible with `SkipBufferUntil`, e.g. for other delimiters:

```csharp
deserializer.SetDataSource(stream);

while (deserializer.IsAnyDataLeft())
{
	if (deserializer.TryDeserialize(out LogEntry entry))
	{
		Process(entry);
		continue;
	}

	// Broken record: skip the rest of the line and resume with the next one.
	deserializer.SkipBufferUntil("\n", alsoSkipDelimiter: true, out bool found);
	if (!found) break;
}
```

The same approach works for any framing that uses a textual delimiter or marker, e.g. skipping a
non-JSON prefix:

```csharp
deserializer.SetDataSource("prefix::42");
deserializer.SkipBufferUntil("::", alsoSkipDelimiter: true, out bool found);
deserializer.TryDeserialize(out int value); // 42
```

With `alsoSkipDelimiter: false` the read position stays *on* the delimiter – useful if the
delimiter itself is the start of the next value (e.g. `"{"`).

## Async reading

```csharp
deserializer.SetDataSource(networkStream);

while (await deserializer.IsAnyDataLeftAsync())
{
	var (success, entry) = await deserializer.TryDeserializeAsync<LogEntry>();
	if (success) Process(entry);
}
```

- **Waiting for the next value is non-blocking:** the start of the next value is awaited without
  blocking a thread, e.g. while waiting for the next JSON Lines record.
- **Read-ahead in parallel to parsing:** the parsing itself runs on the calling thread without
  further awaits. In parallel, one background `ReadAsync` fills the free part of the buffer. Only if
  the parser catches up with that read does it wait (blocking) for it (same compromise as the
  double-buffered `SerializeAsync`).
- **Large values:** the buffer grows like in the synchronous API; a pending read is completed first.
- **Mixing:** sync and async calls can be mixed on the same instance; the order of values is kept.
- **Thread safety:** the instance stays locked for the whole async call. Do not call other members of
  the same instance concurrently from the awaiting code path.

## Skipping values

- **Unwanted records:** deserialize into `JsonFragment` – the value is consumed and kept as raw
  JSON without building an object. Inspect it cheaply or discard it.
- **Heterogeneous streams:** read into `object` (→ `Dictionary<string, object>` / `List<object>` /
  primitives) or into a type with [type mappings](05-type-mappings.md) so the concrete type is
  inferred per record.
- **Inside custom readers:** `ExtensionApi.SkipNextValue()` skips the next JSON value of any shape
  (see [page 04](04-custom-writers-readers.md)).

## Behavior notes

- **Buffer growth:** a single value larger than the buffer (`initialBufferSize`) causes the buffer to
  grow and the read to be retried transparently; the source is not re-read.
- **Blocking:** the synchronous API uses `Stream.Read`. On network streams `IsAnyDataLeft()`
  and `TryDeserialize` block until data arrives or the stream ends. Use the async API to avoid
  blocking while waiting for the next value.
- **End of data:** `IsAnyDataLeft()` returns `false` once only whitespace is left and the stream
  reports no further bytes.
- **Thread safety:** all members lock the instance; one deserializer serves one source at a time.
  Use separate instances for parallel sources.
- **Failed reads:** by default `TryDeserialize` returns `false` and logs; with
  `rethrowExceptions` the exception propagates. Use `SkipBufferUntil` to resynchronize.

## Comparison

| | FeatureLoom | System.Text.Json | Newtonsoft.Json |
|---|---|---|---|
| Multiple top-level values from one source | ✅ `IsAnyDataLeft` + `TryDeserialize` loop | ⚠ STJ 9+: `JsonReaderOptions.AllowMultipleValues` (low-level reader) or `DeserializeAsyncEnumerable<T>(stream, topLevelValues: true)`; before 9 only top-level arrays | ✅ `JsonTextReader.SupportMultipleContent` |
| NDJSON | ✅ out of the box | ⚠ STJ 9+ via `topLevelValues: true`; older versions need manual line splitting | ✅ with `SupportMultipleContent` |
| Resync after a broken record | ✅ automatic with `inputFormat = JsonLines`, or manual via `SkipBufferUntil(delimiter)` | ❌ reader state is invalid after an error | ❌ reader state is invalid after an error |
| Skip to arbitrary delimiter / prefix | ✅ | ❌ | ❌ |
| Async streaming | ⚠ async wait for values + background read-ahead; parser may block briefly if it catches up | ✅ | ⚠ partial |

> ⚠ **Competitor limitation:** after a `JsonException`, both STJ and Newtonsoft readers cannot
> continue on the same stream – one malformed NDJSON line ends the whole import unless the input is
> pre-split into lines (extra copying/allocations).
