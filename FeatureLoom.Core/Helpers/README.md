# FeatureLoom.Helpers

A practical guide to the `FeatureLoom.Helpers` namespace in `FeatureLoom.Core`.

Helpers collects reusable building blocks for:
- argument parsing and conversion
- pooling and low-allocation memory reuse
- type/name reflection utilities
- thread-safe state replacement and lazy initialization
- UTF-8 conversions and text extraction
- robust try/catch wrappers and undo/redo workflows

## Contents

- [Quick Tour](#quick-tour)
- [Core Concepts](#core-concepts)
- [Feature Catalog](#feature-catalog)
- [Helper Reference](#helper-reference)
- [Common Patterns and Examples](#common-patterns-and-examples)
- [Performance and Concurrency Notes](#performance-and-concurrency-notes)
- [When to Choose Helpers](#when-to-choose-helpers)

## Quick Tour

`FeatureLoom.Helpers` is a toolbox, not a single workflow. Pick the helper that matches your problem:

- Parse CLI-style input:

```csharp
var args = new ArgsHelper("-name=Teddy -retries=3 verbose");
args.TryGetFirstByKey<int>("retries", out var retries); // 3
```

- Reuse objects to reduce allocations:

```csharp
var pool = new Pool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear());
var sb = pool.Take();
pool.Return(sb);
```

- Add simple undo/redo to in-memory actions:

```csharp
var undoRedo = new UndoRedo();
undoRedo.DoWithUndo(() => counter++, () => counter--, "Increment counter");
```

If you are unsure where to start, jump to the feature group that matches your use case in [Feature Catalog](#feature-catalog).

## Core Concepts

- Allocation-aware helpers:
  `Pool<T>`, `SharedPool<T>`, `SlicedBuffer<T>`, `ValueWrapper<T>`, `UsingHelper`.
- Reflection/type helpers:
  `CommonTypeFinder`, `CollectionCaster`, `TypeNameHelper`, `EnumHelper`.
- Safe execution and concurrency helpers:
  `TryHelper`, `ThreadSafeHelper`, `SwapHelper`, `ConsoleHelper`.
- Deep object utilities:
  `DeepCloner`, `DeepComparer`.
- Text and encoding helpers:
  `PatternExtractor`, `Utf8Converter`, `EncodableStringWriter`, `ReadOnlyMemoryStream`.
- Workflow/testing helpers:
  `UndoRedo`, `TestHelper`.

## Feature Catalog

### Argument and Value Wrappers

- `ArgsHelper`: Parses command-line style arguments with key/value and positional support.
- `Box<T> (IBox)`: Lightweight mutable value container with optional non-generic access through `IBox`.
- `ValueWrapper<T> (IValueWrapper)`: Pooled, single-use wrapper with optional non-generic metadata access.

### Type and Collection Utilities

- `CommonTypeFinder`: Finds the most specific common type/base type across values or types.
- `CollectionCaster + CollectionCasterExtension`: Runtime casting plus thread-safe extension wrappers for `IEnumerable`.
- `TypeNameHelper`: Converts `Type <-> simplified name` with caching and assembly probing.
- `EnumHelper<T> + EnumHelper`: Generic enum helpers plus non-generic forwarding API.

### Lazy Initialization and State Helpers

- `LazyFactoryValue<T> + LazyValue<T> + LazyUnsafeValue<T>`: Lazy wrappers for factory-based, thread-safe default-constructor, and minimal-overhead scenarios.
- `ThreadSafeHelper`: CAS-based object replacement (`ReplaceObject(...)`).
- `SwapHelper`: Generic `ref` swap utility.

### Allocation and Buffering Helpers

- `Pool<T>`: Optional thread-safe object pool with configurable reset and max size.
- `SharedPool<T>`: Thread-local batched pool plus global reservoir.
- `SlicedBuffer<T>`: Reusable sliced array allocator with resize/free semantics.
- `ReadOnlyMemoryStream`: Seekable read-only `Stream` over `ReadOnlyMemory<byte>`.
- `UsingHelper`: `IDisposable` scope helper for before/after actions.

### Deep Clone/Compare

- `DeepCloner + settings/policies`: Reflection-driven deep cloning with configurable behavior via `Settings`, `DelegateCloneHandling`, and `TaskCloneHandling`.
- `DeepComparer`: Recursive structural equality for objects/graphs.
- `DelegateEqualityComparer<T>`: Build comparers from delegates.

### Text, UTF-8, and Randomness

- `PatternExtractor`: Extracts typed values from `TextSegment` by static/dynamic pattern parts.
- `Utf8Converter`: UTF-8 encode/decode helpers for `ByteSegment`, `TextSegment`, chars, and strings.
- `EncodableStringWriter`: `StringWriter` with explicit encoding exposure.
- `RandomGenerator`: Thread-local pseudo-random plus optional crypto-random helpers.

### Workflow and Test Helpers

- `UndoRedo + UndoRedo.Transaction`: In-memory undo/redo with transaction grouping and async support.
- `TestHelper + nested helpers`: Test setup/context and port range pooling utilities.

## Helper Reference

### ArgsHelper

- Purpose: Parse command-line style input into key/value and positional entries.
- Benefit vs usual approach: Avoids repetitive manual splitting/parsing and keeps key lookup/casting logic centralized.
- Main options:
  `ArgsHelper(string[] args, char bullet = '-', char assignment = '=')`
  `ArgsHelper(string argsString, char bullet = '-', char assignment = '=')`
  `bullet` controls key prefix (`-x`, `/x`), `assignment` controls key/value separator (`=`, `:`).
- Key APIs: `TryGetFirstByKey<T>`, `GetAllByKey`, `GetAllAfterKey`, `TryGetByIndex<T>`, `HasKey`.

```csharp
var args = new ArgsHelper("/mode:fast /retry:3 input.txt", bullet: '/', assignment: ':');
args.TryGetFirstByKey<int>("retry", out var retryCount);
```

```csharp
var args = new ArgsHelper(new[] { "-name=Teddy", "-tags", "blue", "green" });
IEnumerable<string> tags = args.GetAllAfterKey("tags");
```

### Box<T> and IBox

- Purpose: `Box<T>` is a lightweight mutable value container; `IBox` provides non-generic access (`Clear` / `GetValue<T>` / `SetValue<T>`).
- Benefit vs usual approach: You can pass mutable state across APIs without creating one-off holder classes, and you can process mixed `Box<T>` instances through one non-generic interface.
- Benefit for GC: A `Box<T>` instance can be reused (and also pooled) instead of allocating transient holder objects repeatedly.
- Main options:
  Constructors: `Box()`, `Box(T value)`.
  Value field: `value`.
  Conversion: implicit `T -> Box<T>`.
  `IBox` contract: `Clear()`, `GetValue<T>()`, `SetValue<T>(...)`.
- Key APIs: `Clear`, `GetValue<T1>`, `SetValue<T1>`, equality operators.

```csharp
Box<string> box = "hello";
bool isHello = box == "hello";
box.Clear();
```

```csharp
IBox untyped = new Box<int>(42);
int value = untyped.GetValue<int>();
untyped.SetValue(7);
```

```csharp
// Reuse one instance across iterations to reduce allocations.
var reusable = new Box<byte[]>();
for (int i = 0; i < 1000; i++)
{
    reusable.value = new byte[256];
    reusable.Clear();
}

// Optional: pool Box<T> objects if many holders are needed concurrently.
var boxPool = new Pool<Box<byte[]>>(() => new Box<byte[]>(), b => b.Clear(), maxSize: 256);
var pooled = boxPool.Take();
pooled.value = new byte[512];
boxPool.Return(pooled);
```

### CollectionCaster and CollectionCasterExtension

- Purpose: `CollectionCaster` performs runtime conversion from untyped `IEnumerable` to typed arrays/lists; `CollectionCasterExtension` exposes thread-safe extension methods over the same capabilities.
- Benefit vs usual approach: Replaces repeated reflection + cast boilerplate with cached conversion paths and an ergonomic extension-style API.
- Performance emphasis: Caches generated cast delegates per target type and reuses them, reducing repeated reflection/compile overhead on hot paths.
- Main options:
  `TryCastAllElementsToArray<T>(..., bool skipCheck = false)`
  `TryCastAllElementsToArray(..., Type targetType, ..., bool skipCheck = false)`
  `TryCastAllElementsToList<T>(..., bool skipCheck = false)`
  `TryCastAllElementsToList(..., Type targetType, ..., bool skipCheck = false)`
  `skipCheck` skips upfront assignability check for speed.
- Key APIs: `CastToCommonTypeArray`, `CastToCommonTypeList`, and extension equivalents on `IEnumerable`.

```csharp
var caster = new CollectionCaster();
object[] values = { 1, 2, 3 };
caster.TryCastAllElementsToArray(values, typeof(int), out var typedArray);
```

```csharp
var caster = new CollectionCaster();
IEnumerable values = new object[] { 1, 2, 3 };
caster.TryCastAllElementsToList<int>(values, out var list, skipCheck: true);
```

```csharp
IEnumerable values = new object[] { "a", "b" };
values.TryCastAllElementsToList<string>(out var list);
```

### CommonTypeFinder

- Purpose: Find best common type/base type for mixed objects/types.
- Benefit vs usual approach: Handles tricky base/interface resolution consistently instead of ad-hoc type checks.
- Performance emphasis: Uses an internal cache for type-pair results and lock-aware access to avoid recomputing expensive reflection checks.
- Main options:
  `GetCommonType(IEnumerable objects)`
  `GetCommonBaseType(Type type1, Type type2)`.
- Behavior notes:
  For `GetCommonType`, if all elements share one concrete type, that exact type is returned.
  If types differ, the helper prefers the closest common base type or the most specific common interface.
  If no meaningful shared type exists, it falls back to `typeof(object)`.
  For `GetCommonBaseType`, the same logic is applied to exactly two `Type` values.

```csharp
IEnumerable values = new object[] { "x", "y" };
Type common = CommonTypeFinder.GetCommonType(values);
// common == typeof(string)
```

```csharp
IEnumerable values = new object[] { new ArgumentException(), new InvalidOperationException() };
Type common = CommonTypeFinder.GetCommonType(values);
// common == typeof(Exception)
```

```csharp
IEnumerable values = new object[] { new List<int>(), new HashSet<int>() };
Type common = CommonTypeFinder.GetCommonType(values);
// common is typically an interface like ICollection<int>
```

```csharp
Type common = CommonTypeFinder.GetCommonBaseType(typeof(List<int>), typeof(HashSet<int>));
// common is typically an interface like ICollection<int>
```

```csharp
IEnumerable values = new object[] { 1, "two", DateTime.UtcNow };
Type common = CommonTypeFinder.GetCommonType(values);
// common == typeof(object)
```

### ConsoleHelper

- Purpose: Synchronized console I/O, with optional color handling.
- Benefit vs usual approach: Prevents interleaved multi-thread output and wraps color reset/locking in one call.
- Main options:
  `UseLocked(...)`, `UseLockedAsync(...)`
  color overloads: `ConsoleColor? foreGroundColor, ConsoleColor? backGroundColor = null`.
- Key APIs: `WriteLine`, `WriteLineToError`, async variants, `ReadLineLocked`, `ClearConsole`, `CheckHasConsole(resetCachedResult = false)`.

```csharp
ConsoleHelper.WriteLine("Started", ConsoleColor.Green);
bool hasConsole = ConsoleHelper.CheckHasConsole();
```

```csharp
// Useful when multiple async tasks write to console: keeps this block atomic
// and ensures temporary foreground/background colors are applied consistently.
await ConsoleHelper.UseLockedAsync(
  async () =>
  {
    await Console.Out.WriteLineAsync("Starting async step...");
    await Console.Out.WriteLineAsync("Finished async step.");
  },
  foreGroundColor: ConsoleColor.Yellow,
  backGroundColor: ConsoleColor.DarkBlue);
```

### DeepCloner, Settings, and Clone Policies

- Purpose: `DeepCloner` performs deep cloning for arbitrary object graphs (classes, structs, arrays, collections, dictionaries, shared references, cyclic references); `DeepCloner.Settings` plus policy enums (`DelegateCloneHandling`, `TaskCloneHandling`) define behavior for edge cases.
- Benefit vs usual approach: Avoids hand-written clone code per model while keeping delegate/task handling explicit and configurable.
- Performance emphasis: Clone paths are built and cached per type, so repeated clones amortize reflection/setup cost.
- Main options:
  `TryClone<T>(T obj, out T clone)`
  `TryClone<T>(..., in Settings settings)`
  `TryClone<T>(..., Func<Settings, Settings> configureSettings)`
  `TryClone<T>(..., DelegateCloneHandling delegateHandling)`
  `TryClone<T>(..., DelegateCloneHandling delegateHandling, TaskCloneHandling taskHandling)`
  `Settings(DelegateCloneHandling delegateHandling, TaskCloneHandling taskHandling)`
  `WithDelegateHandling(...)`, `WithTaskHandling(...)`, `Settings.Default`.

```csharp
// Deeply nested graph with collections + shared references.
sealed class Address
{
  public string City { get; set; }
}

sealed class OrderLine
{
  public string Sku { get; set; }
  public int Quantity { get; set; }
}

sealed class Order
{
  public List<OrderLine> Lines { get; set; } = new();
  public Address ShippingAddress { get; set; }
}

sealed class Customer
{
  public string Name { get; set; }
  public Address PrimaryAddress { get; set; }
  public List<Order> Orders { get; set; } = new();
}

var sharedAddress = new Address { City = "Berlin" };
var source = new Customer
{
  Name = "Teddy",
  PrimaryAddress = sharedAddress,
  Orders =
  {
    new Order
    {
      ShippingAddress = sharedAddress,
      Lines = { new OrderLine { Sku = "ABC-1", Quantity = 2 } }
    }
  }
};

bool ok = DeepCloner.TryClone(source, out Customer clone);

// New root and nested objects are cloned...
bool deepCloned = !ReferenceEquals(source, clone)
    && !ReferenceEquals(source.Orders[0], clone.Orders[0]);

// ...and shared references inside the graph are preserved in the clone.
bool sharedReferencePreserved = ReferenceEquals(
    clone.PrimaryAddress,
    clone.Orders[0].ShippingAddress);
```

```csharp
// Meaningful non-default policy configuration.
var settings = DeepCloner.Settings.Default
  .WithDelegateHandling(DeepCloner.DelegateCloneHandling.CopyReference)
  .WithTaskHandling(DeepCloner.TaskCloneHandling.CloneCompletedResult);

bool ok = DeepCloner.TryClone(source, out Customer clone, in settings);
```

```csharp
// Direct overload for explicit policy selection.
bool ok = DeepCloner.TryClone(
  source,
  out Customer clone,
  delegateHandling: DeepCloner.DelegateCloneHandling.RebindKnownTargetsAfterClone,
  taskHandling: DeepCloner.TaskCloneHandling.CopyReference);
```

### DeepComparer

- Purpose: Deep structural equality check for object graphs.
- Benefit vs usual approach: Removes large amounts of custom equality code and catches nested differences automatically.
- Main options:
  `AreEqual<T>(T x, T y, bool strictTypeCheck = true)`.
  `strictTypeCheck` toggles strict type equality vs assignable shape comparisons.
- Key APIs: `EqualsDeep<T>(this T x, T y)` extension shortcut.

```csharp
bool same = DeepComparer.AreEqual(new[] { 1, 2 }, new[] { 1, 2 });
```

```csharp
sealed class Address
{
  public string City { get; set; }
}

sealed class Person
{
  public string Name { get; set; }
  public Address Address { get; set; }
}

var left = new Person
{
  Name = "Teddy",
  Address = new Address { City = "Berlin" }
};

var right = new Person
{
  Name = "Teddy",
  Address = new Address { City = "Berlin" }
};

bool same = DeepComparer.AreEqual(left, right);
```

### DelegateEqualityComparer<T>

- Purpose: Build `IEqualityComparer<T>` from delegates.
- Benefit vs usual approach: Enables custom set/dictionary equality without writing dedicated comparer classes.
- Main options:
  `DelegateEqualityComparer(Func<T,T,bool> equals, Func<T,int> getHashCode = null)`.

```csharp
var comparer = new DelegateEqualityComparer<string>((a, b) => a?.Length == b?.Length, s => s?.Length ?? 0);
```

### EncodableStringWriter

- Purpose: `StringWriter` with explicit `Encoding`.
- Benefit vs usual approach: Prevents ambiguous writer encoding assumptions in serializer or transport code.
- Main options:
  `EncodableStringWriter()` (UTF-8 default)
  `EncodableStringWriter(Encoding encoding)`
  `EncodableStringWriter(StringBuilder sb, Encoding encoding)`.

```csharp
using var writer = new EncodableStringWriter(Encoding.UTF8);
writer.Write("payload");
```

### EnumHelper\<T> and EnumHelper

- Purpose: `EnumHelper<T>` provides cached generic enum operations; `EnumHelper` adds forwarding overloads for call sites that prefer one static entry point.
- Benefit vs usual approach: Consolidates enum parsing/flag logic and avoids repeated conversion code.
- Performance emphasis: Generic static caches are initialized once per enum type, enabling fast repeated parse/convert operations.
- Main options:
  `EnumHelper<T>`: `ToName`, `ToInt`, `TryFromString`, `TryFromInt`, `Compare`, `IsFlagSet`.
  `EnumHelper`: `ToName<T>`, `ToInt<T>`, `TryFromString<T>`, `TryFromInt<T>`, `IsFlagSet<T>`.

```csharp
string name = EnumHelper<DayOfWeek>.ToName(DayOfWeek.Friday);
```

```csharp
bool ok = EnumHelper.TryFromString("Friday", out DayOfWeek value);
```

### LazyFactoryValue<T>, LazyValue<T>, and LazyUnsafeValue<T>

- Purpose: Three lazy wrappers for different needs: `LazyFactoryValue<T>` (custom factory), `LazyValue<T>` (thread-safe default constructor path), and `LazyUnsafeValue<T>` (no synchronization overhead).
- Benefit vs usual approach: Lets you choose exactly the lazy-init tradeoff needed (factory flexibility, safety, or minimal overhead) without repeatedly implementing init/cache/reset logic.
- Performance emphasis: Struct-based wrappers avoid extra object allocation; `LazyUnsafeValue<T>` specifically removes synchronization overhead for single-threaded access.
- Main options:
  `LazyFactoryValue(Func<T> factory, bool threadSafe = true, bool clearFactoryAfterConstruction = true)`
  `LazyValue(T obj)`
  `LazyUnsafeValue(T obj)`
  Shared key APIs: `Obj`, `ObjIfExists`, `Exists`, `RemoveObj`.
  Extra for `LazyFactoryValue<T>`: `SetObj`, `ThreadSafe`, `ClearFactoryAfterConstruction`.

```csharp
var lazy = new LazyFactoryValue<StringBuilder>(() => new StringBuilder(), threadSafe: true);
StringBuilder sb = lazy.Obj;
```

```csharp
var lazy = new LazyFactoryValue<StringBuilder>(
  () => new StringBuilder(),
  threadSafe: false,
  clearFactoryAfterConstruction: false);
```

```csharp
LazyUnsafeValue<List<int>> lazy = default;
lazy.Obj.Add(1);
```

```csharp
LazyValue<List<int>> lazy = default;
int count = lazy.Obj.Count;
```

### PatternExtractor

- Purpose: Extract typed values from `TextSegment` using placeholder/static-part patterns.
- Benefit vs usual approach: Replaces repetitive index/split parsing code with reusable typed extraction rules.
- Convenience note: Although the API is based on `TextSegment`, plain `string` values work naturally through implicit conversion.
- Main options:
  Constructors that optionally expose/remove first static pattern element.
  `TryExtract<T1...T8>(...)` overload family for 1 to 8 typed captures.

```csharp
// Both pattern and source are strings here; they are converted implicitly.
var extractor = new PatternExtractor("id={0};name={1}");
bool ok = extractor.TryExtract("id=7;name=Alex", out int id, out string name);
```

### Pool<T>

- Purpose: Simple object pool with optional locking and reset callback.
- Benefit vs usual approach: Reduces hot-path allocations and GC pressure without introducing a heavyweight pooling framework.
- Performance emphasis: Reuses objects aggressively and allows lock-free operation (`threadSafe: false`) in controlled single-thread scenarios.
- Main options:
  `Pool(Func<T> create, Action<T> reset = null, int maxSize = 1000, bool threadSafe = true)`.
  `maxSize` caps retained instances; `threadSafe` controls lock usage.
- Key APIs: `Take`, `Return`, `Count`, `Clear`.

```csharp
var pool = new Pool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear(), maxSize: 200);
var sb = pool.Take();
pool.Return(sb);
```

```csharp
var pool = new Pool<byte[]>(
  create: () => new byte[4096],
  reset: null,
  maxSize: 32,
  threadSafe: false);
```

### RandomGenerator

- Purpose: Thread-local random generation for primitives, bytes, GUIDs, and strings.
- Benefit vs usual approach: Unifies pseudo/crypto randomness behind one API and avoids sharing non-thread-safe `Random` instances.
- Performance emphasis: Uses `[ThreadStatic]` RNG instances and pooled temporary byte arrays to reduce contention and transient allocations.
- Main options:
  `crypto` flag on several APIs (`Int32`, `Int64`, `Double`, `GUID`, `Bytes`, `String`) for cryptographic RNG.
  Range overloads for integer/float/double methods.
- Key APIs: `Reset(seed)`, `Bool(probability)`, `Int32/Int64`, `Double/Float`, `GUID`, `Bytes`, `String`.

```csharp
RandomGenerator.Reset(1234);
int value = RandomGenerator.Int32(10, 20);
Guid id = RandomGenerator.GUID(crypto: true);
```

```csharp
double sample = RandomGenerator.Double();
byte[] bytes = RandomGenerator.Bytes(length: 32, crypto: true);
string token = RandomGenerator.String(length: 12, crypto: false, allowedChars: "ABC123");
```

### ReadOnlyMemoryStream

- Purpose: Read-only seekable stream over `ReadOnlyMemory<byte>`.
- Benefit vs usual approach: Exposes memory buffers as streams without copying into temporary `MemoryStream` instances.
- Performance emphasis: Zero-copy read path over existing memory significantly lowers allocation and copy costs.
- Main options: `ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)`.
- Key APIs: `Read`, `Seek`, `Position`, `Length`.

```csharp
var data = new byte[] { 1, 2, 3 };
using var stream = new ReadOnlyMemoryStream(data);
int first = stream.ReadByte();
```

### SharedPool<T>

- Purpose: High-throughput pooled allocation using thread-local stacks plus global spill/fetch.
- Benefit vs usual approach: Scales better than one global lock pool under multi-threaded contention.
- Performance emphasis: Thread-local fast path minimizes lock contention; batched fetch/spill reduces synchronization frequency.
- Main options:
  `TryInit(Func<T> onCreate, Action<T> onReset, Action<T> onDiscard = null, int globalCapacity = 1000, int localCapacity = 50, int fetchOnEmpty = 40, int keepOnFull = 10)`.
  `globalCapacity` limits global reservoir.
  `localCapacity` controls local stack soft limit.
  `fetchOnEmpty` batch-fetch size from global to local.
  `keepOnFull` local retention target when trimming.
- Key APIs: `Take`, `Return`, `ClearLocal`, `ClearGlobal`, `IsInitialized`, `GlobalCount`, `LocalCount`.

```csharp
SharedPool<StringBuilder>.TryInit(() => new StringBuilder(), sb => sb.Clear());
var sb = SharedPool<StringBuilder>.Take();
SharedPool<StringBuilder>.Return(sb);
```

```csharp
SharedPool<StringBuilder>.TryInit(
  onCreate: () => new StringBuilder(256),
  onReset: sb => sb.Clear(),
  onDiscard: null,
  globalCapacity: 5000,
  localCapacity: 100,
  fetchOnEmpty: 80,
  keepOnFull: 20);
```

### SlicedBuffer<T>

- Purpose: Reusable slice allocator over shared arrays to reduce GC pressure.
- Benefit vs usual approach: Avoids frequent short-lived array allocations in parsing/serialization pipelines.
- Performance emphasis: Designed for low-allocation slice reuse with optional growth strategy and minimal per-slice overhead.
- Main options:
  `SlicedBuffer()` default tuned settings.
  `SlicedBuffer(int capacity, int maxCapacity = 0, int minSlicesPerBuffer = 4, bool growSliceLimit = false, bool threadSafe = false)`.
  `maxCapacity` bounds growth; `minSlicesPerBuffer` controls slice-limit heuristic; `growSliceLimit` ties max slice to growth; `threadSafe` enables internal locking.
- Key APIs: `GetSlice`, `Reset`, `ExtendSlice`, `ResizeSlice`, `FreeSlice`, `Shared`.

```csharp
var buffer = new SlicedBuffer<byte>(capacity: 128, threadSafe: true);
var slice = buffer.GetSlice(16);
buffer.FreeSlice(ref slice);
```

```csharp
var buffer = new SlicedBuffer<char>(
  capacity: 1024,
  maxCapacity: 8192,
  minSlicesPerBuffer: 8,
  growSliceLimit: true,
  threadSafe: false);

var slice = buffer.GetSlice(64);
buffer.ResizeSlice(ref slice, 96);
```

### SwapHelper

- Purpose: Swap two `ref` values.
- Benefit vs usual approach: Removes repeated temporary-variable swap boilerplate.
- Main options: `Swap<T>(ref T obj1, ref T obj2)`.

```csharp
int a = 1;
int b = 2;
SwapHelper.Swap(ref a, ref b);
```

### TestHelper, TestContext, and PortPoolService

- Purpose: `TestHelper` sets up reusable test context behavior; `TestHelper.TestContext` owns per-test resources; `TestHelper.PortPoolService` manages sharable port ranges.
- Benefit vs usual approach: Standardizes test isolation setup and avoids copy-paste fixture plumbing plus flaky ad-hoc port selection.
- Main options:
  `PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true)`.
  `PortPoolService.Initialize(IEnumerable<(int start, int end)> portRanges)`.
  `TestContext`: `BorrowPort()`, `ReturnBorrowedPorts()`, `Dispose()`.
- Key APIs: `HasAnyLogError(...)`, `BorrowPort(...)`, `ReturnPort(...)`.

```csharp
using var ctx = TestHelper.PrepareTestContext(disconnectLoggers: true, useMemoryStorage: true);
bool hasError = TestHelper.HasAnyLogError(ctx);
```

```csharp
using var ctx = TestHelper.PrepareTestContext(disconnectLoggers: false, useMemoryStorage: false);
```

```csharp
using var ctx = TestHelper.PrepareTestContext();
int port = ctx.BorrowPort();
```

```csharp
TestHelper.PortPoolService.Initialize(new[] { (start: 30000, end: 30100) });
```

### ThreadSafeHelper

- Purpose: Atomic replacement loop for reference values.
- Benefit vs usual approach: Provides lock-free compare-and-swap replacement without rewriting CAS loops.
- Main options:
  `ReplaceObject<T>(ref T objRef, Func<T, T> provideNewObject) where T : class`.
  `provideNewObject` may run multiple times under contention.

```csharp
string state = "v1";
ThreadSafeHelper.ReplaceObject(ref state, old => old + ".next");
```

### TryHelper

- Purpose: Exception-to-result helpers for sync/async delegates.
- Benefit vs usual approach: Keeps happy-path call sites concise while retaining optional exception capture.
- Main options:
  `Try(...)` and `TryAsync(...)` families for `Action`, `Func<T>`, `Func<Task>`, `Func<Task<T>>`.
  Variants with exception outputs/tuples for diagnostics.

```csharp
bool ok = ((Func<int>)(() => int.Parse("42"))).Try(out int result);
```

```csharp
var (success, value, ex) = await (async () => await Task.FromResult(42)).TryAsyncWithException();
```

### TypeNameHelper

- Purpose: Convert `Type` to simplified display name and back.
- Benefit vs usual approach: Adds cached, assembly-aware type resolution beyond fragile raw `Type.GetType(...)` use.
- Performance emphasis: Bi-directional name/type caches avoid repeated assembly scans and string parsing work.
- Main options:
  `TypeNameHelper(bool threadSafe)`
  `TypeNameHelper()`
  `SupplementaryAssemblies` for extra assembly probing.
- Key APIs: `GetSimplifiedTypeName`, `GetTypeFromSimplifiedName`, static `TypeNameHelper.Shared`.

```csharp
var helper = TypeNameHelper.Shared;
string name = helper.GetSimplifiedTypeName(typeof(Dictionary<string, int>));
Type type = helper.GetTypeFromSimplifiedName(name);
```

```csharp
var helper = new TypeNameHelper(threadSafe: false);
helper.SupplementaryAssemblies.Add("plugins/MyPlugin.dll");
```

### UndoRedo and UndoRedo.Transaction

- Purpose: `UndoRedo` manages in-memory undo/redo stacks; `UndoRedo.Transaction` groups several actions into one logical undo entry.
- Benefit vs usual approach: Delivers practical undo/redo behavior and transaction grouping without building a full command framework.
- Main options:
  `UndoRedo(int historyLimit = 0)` where `historyLimit <= 0` means unlimited history.
  `Transaction` created via `StartTransaction(...)` or `StartTransactionAsync(...)`.
- Key APIs:
  `DoWithUndo`, `DoWithUndoAsync`, `AddUndo(Action|Func<Task>)`, `PerformUndo`, `PerformRedo`, async variants, `Clear`, `TryCombineLastUndos`, `StartTransaction`, `StartTransactionAsync`.

```csharp
var undoRedo = new UndoRedo(historyLimit: 100);
int counter = 0;
undoRedo.DoWithUndo(() => counter++, () => counter--, "Increment");
```

```csharp
var undoRedo = new UndoRedo();
int counter = 0;

undoRedo.DoWithUndo(() => counter += 1, () => counter -= 1, "Add one");
undoRedo.DoWithUndo(() => counter += 10, () => counter -= 10, "Add ten");

// Descriptions are ordered from newest to oldest.
IEnumerable<string> undoSteps = undoRedo.UndoDescriptions;
```

```csharp
var undoRedo = new UndoRedo(historyLimit: 0); // unlimited
await undoRedo.DoWithUndoAsync(
  doAction: async () => await Task.Delay(1),
  undoAction: async () => await Task.Delay(1),
  description: "Async step");
```

```csharp
var undoRedo = new UndoRedo();
int counter = 0;

undoRedo.DoWithUndo(() => counter += 1, () => counter -= 1, "Add one");
undoRedo.DoWithUndo(() => counter += 10, () => counter -= 10, "Add ten");
// counter == 11

undoRedo.PerformUndo();
// counter == 1
// undoRedo.RedoDescriptions now contains "Add ten"

undoRedo.PerformRedo();
// counter == 11 again
```

```csharp
using (undoRedo.StartTransaction("Batch edit"))
{
  undoRedo.AddUndo(() => Console.WriteLine("undo item 1"));
  undoRedo.AddUndo(() => Console.WriteLine("undo item 2"));
}
```

### UsingHelper

- Purpose: Lightweight scope helper for before/after actions.
- Benefit vs usual approach: Lets you define tiny scoped lifecycle hooks without custom disposable classes.
- Main options:
  `UsingHelper(Action before, Action after)`.
  Static convenience methods: `Do(Action before, Action after)`, `Do(Action after)`.

```csharp
using (UsingHelper.Do(before: () => Console.WriteLine("begin"), after: () => Console.WriteLine("end")))
{
}
```

### Utf8Converter

- Purpose: High-throughput UTF-8 encode/decode helpers around `ByteSegment` and pooled buffers.
- Benefit vs usual approach: Offers pooled-buffer UTF-8 conversion and escape handling with fewer temporary allocations.
- Performance emphasis: Uses shared pooled `SlicedBuffer<T>` and pooled `StringBuilder` instances to keep encoding/decoding allocation-light.
- Main options:
  Decode APIs: `DecodeUtf8ToStringBuilder`, `DecodeUtf8ToString`, `DecodeUtf8ToChars`, `DecodeUtf8ToSpanOfChars`.
  Encode APIs: `EncodeToUtf8(this string ...)`, `EncodeToUtf8(this TextSegment ...)`.
  Buffer return API: `ReturnBytesToPool(ref ByteSegment byteSegment)`.
- Optional args:
  `StringBuilder stringBuilder = null`, `SlicedBuffer<char|byte> slicedBuffer = null` for caller-controlled pooling.

```csharp
ByteSegment bytes = "hello".EncodeToUtf8();
string text = bytes.DecodeUtf8ToString();
Utf8Converter.ReturnBytesToPool(ref bytes);
```

```csharp
var charBuffer = new SlicedBuffer<char>(512, threadSafe: true);
ByteSegment bytes = "world".EncodeToUtf8();
ArraySegment<char> chars = bytes.DecodeUtf8ToChars(stringBuilder: null, slicedBuffer: charBuffer);
Utf8Converter.ReturnBytesToPool(ref bytes);
```

### ValueWrapper<T> and IValueWrapper

- Purpose: `ValueWrapper<T>` provides pooled single-use wrapping; `IValueWrapper` exposes non-generic metadata (`WrappedType`, `IsValid`).
- Benefit vs usual approach: Minimizes boxing/allocation overhead for transient wrapped values while still allowing non-generic handling paths.
- Performance emphasis: Backed by `SharedPool<ValueWrapper<T>>`, enabling very low-overhead reuse in high-frequency flows.
- Main options:
  Static `Wrap(T value)` to rent/populate wrapper.
  `UnwrapAndDispose()` to retrieve value and return wrapper to pool.
  `IValueWrapper`: `WrappedType`, `IsValid`.
- Key lifecycle rule: call `UnwrapAndDispose()` exactly once per wrapped instance.

```csharp
IValueWrapper wrapper = ValueWrapper<int>.Wrap(7);
Type wrappedType = wrapper.WrappedType;
```

```csharp
var wrapped = ValueWrapper<int>.Wrap(42);
int value = wrapped.UnwrapAndDispose();
```

## Common Patterns and Examples

### Pooling with `Pool<T>`

```csharp
using FeatureLoom.Helpers;
using System.Text;

var pool = new Pool<StringBuilder>(
    create: () => new StringBuilder(256),
    reset: sb => sb.Clear(),
    maxSize: 100);

var sb = pool.Take();
sb.Append("FeatureLoom");
Console.WriteLine(sb.ToString());

pool.Return(sb);

// Expected output:
// FeatureLoom
```

### Thread-local + global pooling with `SharedPool<T>`

```csharp
using FeatureLoom.Helpers;
using System.Text;

SharedPool<StringBuilder>.TryInit(
    onCreate: () => new StringBuilder(128),
    onReset: sb => sb.Clear(),
    globalCapacity: 1000,
    localCapacity: 20,
    fetchOnEmpty: 10,
    keepOnFull: 5);

var sb = SharedPool<StringBuilder>.Take();
sb.Append("shared pooled instance");
Console.WriteLine(sb.Length > 0);
SharedPool<StringBuilder>.Return(sb);

// Expected output:
// True
```

### Lazy initialization choices

```csharp
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;

LazyValue<List<int>> threadSafeLazy = default;
threadSafeLazy.Obj.Add(1);
threadSafeLazy.Obj.Add(2);
Console.WriteLine(threadSafeLazy.Obj.Count);

var factoryLazy = new LazyFactoryValue<string>(() => DateTime.UtcNow.ToString("O"), threadSafe: true);
Console.WriteLine(factoryLazy.Exists); // False before first access
Console.WriteLine(factoryLazy.Obj.Length > 0);
Console.WriteLine(factoryLazy.Exists); // True after first access

// Expected output:
// 2
// False
// True
// True
```

### Undo/Redo in-memory workflow

```csharp
using FeatureLoom.Helpers;

var service = new UndoRedo();
int counter = 0;

service.DoWithUndo(
    doAction: () => counter += 5,
    undoAction: () => counter -= 5,
    description: "Add five");

Console.WriteLine($"After do: {counter}");

service.PerformUndo();
Console.WriteLine($"After undo: {counter}");

service.PerformRedo();
Console.WriteLine($"After redo: {counter}");

// Expected output:
// After do: 5
// After undo: 0
// After redo: 5
```

### Type round-trip with `TypeNameHelper`

```csharp
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;

var helper = TypeNameHelper.Shared;

Type original = typeof(Dictionary<string, int[]>);
string simplified = helper.GetSimplifiedTypeName(original);
Type resolved = helper.GetTypeFromSimplifiedName(simplified);

Console.WriteLine(simplified);
Console.WriteLine(resolved == original);

// Expected output:
// System.Collections.Generic.Dictionary<System.String, System.Int32[]>
// True
```

### Safe execution wrappers with `TryHelper`

```csharp
using FeatureLoom.Helpers;

bool ok = ((Action)(() =>
{
    // Work that might throw
})).Try();

var parseOk = ((Func<int>)(() => int.Parse("123"))).Try(out int value);

Console.WriteLine($"Action={ok}, Parse={parseOk}, Value={value}");

// Expected output:
// Action=True, Parse=True, Value=123
```

### UTF-8 encode/decode with pooled bytes

```csharp
using FeatureLoom.Collections;
using FeatureLoom.Helpers;

ByteSegment bytes = "hello ünicode".EncodeToUtf8();
string text = bytes.DecodeUtf8ToString();
Utf8Converter.ReturnBytesToPool(ref bytes);

Console.WriteLine(text);

// Expected output:
// hello ünicode
```

### Scoped before/after actions with `UsingHelper`

```csharp
using FeatureLoom.Helpers;

bool entered = false;
bool left = false;

using (UsingHelper.Do(
    before: () => entered = true,
    after: () => left = true))
{
    Console.WriteLine($"Inside scope: {entered}");
}

Console.WriteLine($"After scope: {left}");

// Expected output:
// Inside scope: True
// After scope: True
```

## Performance and Concurrency Notes

- `Pool<T>` is simpler and often enough for single service instances; `SharedPool<T>` scales better under high multi-threaded contention.
- `SlicedBuffer<T>` is optimized for short-lived, mostly LIFO slice usage. Only the most recently allocated slices can be reclaimed efficiently.
- `LazyValue<T>` is thread-safe; `LazyUnsafeValue<T>` avoids synchronization cost when external synchronization already exists.
- `ValueWrapper<T>` is single-use by design. Always call `UnwrapAndDispose()` exactly once.
- `Utf8Converter` can use pooled buffers (`ByteSegment`, `SlicedBuffer<T>`) to reduce allocations in parser/serializer hot paths.
- `DeepCloner` and `DeepComparer` are reflection-heavy by nature; useful for correctness and tooling, but not meant for every hot loop.

## When to Choose Helpers

Use `FeatureLoom.Helpers` when you need:
- small composable utilities with low friction
- allocation-aware primitives for high-frequency paths
- type and reflection helpers without adding external dependencies
- practical in-memory undo/redo or safe-execution wrappers

Prefer domain-specific components when you need:
- durable/persistent undo history (command/event sourcing approach)
- distributed/shared cache or pooling across processes
- specialized serialization formats beyond in-memory UTF-8 helper usage
