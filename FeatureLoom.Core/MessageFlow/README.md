# FeatureLoom.MessageFlow

A practical guide to the `FeatureLoom.MessageFlow` namespace in `FeatureLoom.Core`.

MessageFlow provides a composable in-process messaging model based on:
- sources (`IMessageSource`)
- sinks (`IMessageSink`)
- connectors (`IMessageFlowConnection`)
- endpoints (senders, receivers, triggers, probes)

## Contents

- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Feature Catalog](#feature-catalog)
- [Common Patterns and Examples](#common-patterns-and-examples)
- [Routing Patterns](#routing-patterns)
- [Concurrency and Thread-Safe N-to-M Connectability](#concurrency-and-thread-safe-n-to-m-connectability)
- [Advanced APIs and Helper Usage](#advanced-apis-and-helper-usage)
- [Why MessageFlow Over Alternatives](#why-messageflow-over-alternatives)

## Quick Start

```csharp
var sender = new Sender<string>();

sender.ProcessMessage<string>(msg => Console.WriteLine($"Audit: {msg}"));
sender.ProcessMessage<string>(msg => Console.WriteLine($"UI: {msg}"));

sender.Send("System started");

// Expected output:
// Audit: System started
// UI: System started
```

## Core Concepts

- `IMessageSink`: Consumes messages (`Post`, `Post by ref`, `PostAsync`).
- `IMessageSource`: Connects to sinks and forwards outgoing messages.
- `IMessageFlowConnection`: A component that is both sink and source.
- `IAlternativeMessageSource`: Optional `Else` branch for rejected/unmatched messages.
- Typed contracts (`IMessageSink<T>`, `IMessageSource<T>`) make pipelines safer and clearer.

## Feature Catalog

### Common Contracts and Types

- `IMessageFlow`: Marker interface for all MessageFlow elements.
- `IMessageSink`: Receives messages via `Post`, `Post by ref`, and `PostAsync`.
- `IMessageSource`: Manages downstream connections and forwards messages.
- `ITypedMessageSink`: Sink that exposes its consumed CLR type.
- `IMessageSink<T>`: Typed sink marker built on `ITypedMessageSink`.
- `ITypedMessageSource`: Source that exposes its emitted CLR type.
- `IMessageSource<T>`: Typed source marker built on `ITypedMessageSource`.
- `IMessageFlowConnection`: Combined sink and source role.
- `IMessageFlowConnection<T>`: Typed combined sink/source with same input and output type.
- `IMessageFlowConnection<I, O>`: Typed combined sink/source with different input/output types.
- `IAlternativeMessageSource`: Optional `Else` route for non-matching/rejected messages.
- `IRequester`: Request-side contract for request/reply interactions.
- `IReplier`: Reply-side contract for request/reply interactions.
- `IRequestMessage<T>`: Request envelope with `RequestId` and `Content`.
- `IResponseMessage<T>`: Response envelope with `RequestId` and `Content`.
- `RequestMessage<T>`: Concrete request envelope with generated or explicit request id.
- `ResponseMessage<T>`: Concrete response envelope correlated by request id.
- `IMessageWrapper`: Untyped wrapper contract with unwrap-and-send helpers.
- `IMessageWrapper<T>`: Typed wrapper contract exposing `TypedMessage`.
- `ITopicMessage`: Contract for messages carrying a `Topic`.
- `TopicMessageWrapper<T>`: Wrapper that combines typed payload with topic metadata.
- `ISender`: Untyped sender abstraction (sync/by-ref/async).
- `ISender<T>`: Typed sender abstraction.
- `IReceiver<T>`: Typed receiver abstraction with wait/peek/receive APIs.
- `ForwardingMethod`: Forwarding mode enum (`Synchronous`, `SynchronousByRef`, `Asynchronous`).

### Connectors

- `Forwarder`: Untyped pass-through connector.
- `Forwarder<T>`: Typed pass-through connector that ignores non-`T` messages.
- `Filter<T>`: Predicate/type-based filter with optional `Else` branch.
- `MessageConverter<I, O>`: Converts input type `I` to output type `O`.
- `Splitter<T, E>`: Splits one input message into multiple output messages.
- `Aggregator<T>`: Delegate-driven stateful connector for batching/coalescing/timeout flush.
- `AsyncForwarder`: Fire-and-forget asynchronous forwarding connector.
- `QueueForwarder`: Queue-backed forwarding connector with worker scaling.
- `CurrentContextForwarder<T>`: Forwards on captured synchronization context.
- `BufferingForwarder<T>`: Replays buffered recent messages to newly connected sinks.
- `DeactivatableForwarder`: Runtime gate that can enable/disable forwarding.
- `DelayingForwarder`: Applies fixed delay before forwarding.
- `DuplicateMessageSuppressor<T>`: Suppresses duplicates within a time window.
- `Hub`: Broadcast group container for interconnected sockets.
- `Hub.Socket`: Hub endpoint that broadcasts to all other sockets.
- `Junction`: Rule-based multi-route dispatcher with priorities and optional multi-cast mode.

### Endpoints

- `Sender`: Untyped broadcast sender endpoint.
- `Sender<T>`: Typed broadcast sender endpoint.
- `RequestSender<REQ, RESP>`: Correlated request/reply endpoint with timeout handling.
- `QueueReceiver<T>`: FIFO queue receiver with bounded capacity and configurable full-queue behavior.
- `PriorityQueueReceiver<T>`: Priority-based queue receiver with bounded capacity.
- `LatestMessageReceiver<T>`: Keeps only latest accepted message and exposes wait/peek/receive APIs.
- `PriorityMessageReceiver<T>`: Accepts only latest-highest-priority message, routes lower ones to `Else`.
- `ReceiverBuffer<T>`: Local prefetching wrapper around `IReceiver<T>` for lower overhead consumption.
- `ValueWrappingQueueReceiver`: Object queue receiver that wraps value types to reduce boxing allocations.
- `MessageTrigger`: Waitable trigger sink with `ManualReset`, `InstantReset`, and `Toggle` modes.
- `ConditionalTrigger<T, R>`: Triggers on `T` and optionally resets on `R` conditions.
- `MessageCounter`: Counts incoming messages and supports async threshold waiting.
- `MessageLog<T>`: Circular log sink with id-based reads and wait-for-id semantics.
- `MessageLogReader<T>`: Reads from log buffers and forwards entries in id order.
- `ProcessingEndpoint<T>`: Delegate-based processing sink (sync/async, optional reject route).
- `StatisticsMessageProbe<T1, T2>`: Probe endpoint for filtering, conversion, buffering, and time-slice statistics.

### Extension APIs

- `MessageFlowExtensions`
- `ReceiverExtensions`
- `SenderExtensions`

## Common Patterns and Examples

### Filter with Else Branch

```csharp
IMessageSource source = new Sender<object>();

var accepted = source.FilterMessage<int>(x => x >= 0, out var rejected);
accepted.ProcessMessage<int>(x => Console.WriteLine($"Accepted: {x}"));
rejected.ProcessMessage<object>(x => Console.WriteLine($"Rejected: {x}"));

source.Send(42);
source.Send(-2);
source.Send("not an int");

// Expected output:
// Accepted: 42
// Rejected: -2
// Rejected: not an int
```

### Convert Message

```csharp
IMessageSource source = new Sender<string>();

source
    .ConvertMessage<string, int>(text => int.Parse(text))
    .ProcessMessage<int>(number => Console.WriteLine(number * 2));

source.Send("21");

// Expected output:
// 42
```

### Split Message

```csharp
IMessageSource source = new Sender<string>();

source
    .SplitMessage<string, string>(line => line.Split(','))
    .ProcessMessage<string>(part => Console.WriteLine(part.Trim()));

source.Send("alpha,beta,gamma");

// Expected output:
// alpha
// beta
// gamma
```

### Request/Response

```csharp
var requester = new RequestSender<string, int>(timeout: TimeSpan.FromSeconds(2));

requester.Respond<string, int>(text => text.Length);

int length = await requester.SendRequestAsync("FeatureLoom");
Console.WriteLine(length);

// Expected output:
// 11
```

### Queue Backpressure

```csharp
var queue = new QueueReceiver<int>(
    maxQueueSize: 1000,
    maxWaitOnFullQueue: TimeSpan.FromMilliseconds(50));

var producer = new Sender<int>();
producer.ConnectTo(queue);

producer.Send(1);
producer.Send(2);

while (queue.TryReceive(out var item))
{
    Console.WriteLine($"Consumed {item}");
}

// Expected output:
// Consumed 1
// Consumed 2
```

### Latest Value Semantics

```csharp
var latest = new LatestMessageReceiver<double>();
var telemetry = new Sender<double>();

telemetry.ConnectTo(latest);

telemetry.Send(10.1);
telemetry.Send(10.2);
telemetry.Send(10.3);

if (latest.TryReceive(out var current))
{
    Console.WriteLine($"Latest value: {current}");
}

// Expected output:
// Latest value: 10.3
```

### Dedup plus Delay

```csharp
IMessageSource source = new Sender<string>();

source
    .SuppressDuplicateMessages<string>(TimeSpan.FromMilliseconds(500))
    .DelayMessage(TimeSpan.FromMilliseconds(100))
    .ProcessMessage<string>(msg => Console.WriteLine($"Forwarded: {msg}"));

source.Send("A");
source.Send("A");
source.Send("B");

// Expected output (with duplicate suppression active):
// Forwarded: A
// Forwarded: B
```

### Message Statistics Probe

```csharp
var probe = new StatisticsMessageProbe<string, string>(
    name: "orders",
    filter: msg => msg.StartsWith("order:"),
    converter: msg => msg,
    messageBufferSize: 200,
    timeSliceSize: TimeSpan.FromSeconds(1),
    maxTimeSlices: 60);

var source = new Sender<string>();
source.ConnectTo(probe);

source.Send("order:created");
source.Send("health:ok");

Console.WriteLine($"Matched messages: {probe.Counter}");

// Expected output:
// Matched messages: 1
```

## Routing Patterns

### Hub (broadcast to peer sockets)

```csharp
var hub = new Hub();

var socketA = hub.CreateSocket();
var socketB = hub.CreateSocket();
var socketC = hub.CreateSocket();

socketB.ProcessMessage<string>(msg => Console.WriteLine($"B received: {msg}"));
socketC.ProcessMessage<string>(msg => Console.WriteLine($"C received: {msg}"));

socketA.Post("hello from A");

// Expected output:
// B received: hello from A
// C received: hello from A
```

### Junction (rule-based routing)

```csharp
var junction = new Junction(multiOption: false);

junction.ConnectOption<int>(
    sink: new ProcessingEndpoint<int>(x => Console.WriteLine($"High-priority int: {x}")),
    checkMessage: x => x > 100,
    priority: 10);

junction.ConnectOption<int>(
    sink: new ProcessingEndpoint<int>(x => Console.WriteLine($"Normal int: {x}")),
    checkMessage: x => x >= 0,
    priority: 1);

junction.Else.ProcessMessage<object>(x => Console.WriteLine($"Else route: {x}"));

junction.Post(150);
junction.Post(20);
junction.Post(-5);
junction.Post("text");

// Expected output:
// High-priority int: 150
// Normal int: 20
// Else route: -5
// Else route: text
```

### Junction with multiOption true

```csharp
var junction = new Junction(multiOption: true);

junction.ConnectOption<int>(
    sink: new ProcessingEndpoint<int>(x => Console.WriteLine($"All non-negative ints: {x}")),
    checkMessage: x => x >= 0,
    priority: 1);

junction.ConnectOption<int>(
    sink: new ProcessingEndpoint<int>(x => Console.WriteLine($"Even ints: {x}")),
    checkMessage: x => x % 2 == 0,
    priority: 5);

junction.ConnectOption<int>(
    sink: new ProcessingEndpoint<int>(x => Console.WriteLine($"Large ints: {x}")),
    checkMessage: x => x > 100,
    priority: 10);

junction.Post(120);
junction.Post(3);

// Expected output:
// Large ints: 120
// Even ints: 120
// All non-negative ints: 120
// All non-negative ints: 3
```

## Concurrency and Thread-Safe N-to-M Connectability

MessageFlow supports:
- 1-to-n fan-out
- n-to-1 fan-in
- n-to-m graph composition

Most core connectors/endpoints are designed so posting, connecting, and disconnecting can be used safely in concurrent scenarios.

```csharp
var producerA = new Sender<string>();
var producerB = new Sender<string>();

var sharedQueue = new QueueReceiver<string>(maxQueueSize: 1000);
var auditSink = new ProcessingEndpoint<string>(msg => Console.WriteLine($"Audit: {msg}"));

producerA.ConnectTo(sharedQueue);
producerB.ConnectTo(sharedQueue);
producerA.ConnectTo(auditSink);
producerB.ConnectTo(auditSink);

producerA.Send("A-1");
producerB.Send("B-1");
producerA.Send("A-2");

while (sharedQueue.TryReceive(out var item))
{
    Console.WriteLine($"Worker consumed: {item}");
}

// Expected output (order may vary under concurrency):
// Audit: A-1
// Audit: B-1
// Audit: A-2
// Worker consumed: A-1
// Worker consumed: B-1
// Worker consumed: A-2
```

## Advanced APIs and Helper Usage

### DeactivatableForwarder with manual control

```csharp
var source = new Sender<string>();
var gate = new DeactivatableForwarder();

source.ConnectTo(gate);
gate.ProcessMessage<string>(msg => Console.WriteLine($"Passed: {msg}"));

gate.Active = true;
source.Send("one");

gate.Active = false;
source.Send("two");

gate.Active = true;
source.Send("three");

// Expected output:
// Passed: one
// Passed: three
```

### DeactivatableForwarder with auto-activation

```csharp
var autoSource = new Sender<string>();

string control = "";
var controlTap = new ProcessingEndpoint<string>(msg => control = msg);
autoSource.ConnectTo(controlTap);

var autoGate = new DeactivatableForwarder(
    autoActivationCondition: wasActive => control switch
    {
        "enable" => true,
        "disable" => false,
        _ => wasActive
    });

autoSource.ConnectTo(autoGate);
autoGate.ProcessMessage<string>(msg => Console.WriteLine($"Auto-passed: {msg}"));

autoSource.Send("hello");
autoSource.Send("enable");
autoSource.Send("work");
autoSource.Send("disable");
autoSource.Send("later");

// Expected output:
// Auto-passed: enable
// Auto-passed: work
```

### ConditionalTrigger is awaitable

```csharp
var trigger = new ConditionalTrigger<string, string>(
    triggerCondition: msg => msg == "start",
    resetCondition: msg => msg == "stop");

trigger.Post("noise");
Console.WriteLine($"Triggered after noise: {trigger.IsTriggered()}");

var waitForStart = trigger.WaitAsync(TimeSpan.FromMilliseconds(200));
trigger.Post("start");
Console.WriteLine($"Triggered after start: {trigger.IsTriggered()}");
Console.WriteLine($"Awaited start signal: {await waitForStart}");

trigger.Post("stop");
Console.WriteLine($"Triggered after stop: {trigger.IsTriggered()}");

// Expected output:
// Triggered after noise: False
// Triggered after start: True
// Awaited start signal: True
// Triggered after stop: False
```

### MessageCounter threshold waiting

```csharp
var source = new Sender<int>();
var counter = new MessageCounter();

source.ConnectTo(counter);

var waitTask = counter.WaitForCountAsync(3);

source.Send(1);
source.Send(2);
source.Send(3);

var reached = await waitTask;
Console.WriteLine($"Reached count: {reached}");

// Expected output:
// Reached count: 3
```

### Message wrappers and unwrapping

```csharp
var sender = new Sender();
sender.ProcessMessage<int>(x => Console.WriteLine($"Unwrapped payload: {x}"));

IMessageWrapper wrapped = new TopicMessageWrapper<int>(42, "numbers.answer");
wrapped.UnwrapAndSend(sender);

// Expected output:
// Unwrapped payload: 42
```

### Topic filtering and unwrapping

```csharp
IMessageSource source = new Sender<object>();

source
    .FilterByTopic("orders.*", unwrap: true)
    .ProcessMessage<string>(msg => Console.WriteLine($"Topic payload: {msg}"));

source.Send(new TopicMessageWrapper<string>("created", "orders.new"));
source.Send(new TopicMessageWrapper<string>("ignored", "health.ok"));

// Expected output:
// Topic payload: created
```

### BatchMessages

```csharp
IMessageSource source = new Sender<int>();

source
    .BatchMessages<int>(maxBatchSize: 3, maxCollectionTime: TimeSpan.FromMilliseconds(200))
    .ProcessMessage<int[]>(arr => Console.WriteLine($"Batch size: {arr.Length}"));

source.Send(1);
source.Send(2);
source.Send(3);

// Expected output:
// Batch size: 3
```

### SourceHelper and TypedSourceHelper in custom source components

```csharp
sealed class IntTicker : IMessageSource<int>
{
    private readonly TypedSourceHelper<int> source = new TypedSourceHelper<int>();

    public Type SentMessageType => source.SentMessageType;
    public int CountConnectedSinks => source.CountConnectedSinks;
    public bool NoConnectedSinks => source.NoConnectedSinks;

    public void Tick(int value) => source.Forward(value);

    public void ConnectTo(IMessageSink sink, bool weakReference = false) => source.ConnectTo(sink, weakReference);
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => source.ConnectTo(sink, weakReference);
    public void DisconnectFrom(IMessageSink sink) => source.DisconnectFrom(sink);
    public void DisconnectAll() => source.DisconnectAll();
    public IMessageSink[] GetConnectedSinks() => source.GetConnectedSinks();
    public bool IsConnected(IMessageSink sink) => source.IsConnected(sink);
}

var ticker = new IntTicker();
ticker.ProcessMessage<int>(x => Console.WriteLine($"Tick: {x}"));

ticker.Tick(1);
ticker.Tick(2);

// Expected output:
// Tick: 1
// Tick: 2
```

### SourceValueHelper and TypedSourceValueHelper guidance

```csharp
// Usage guideline (not typical app code):
// Keep SourceValueHelper/TypedSourceValueHelper<T> as fields on reference types,
// and avoid copying them (they are mutable structs with internal locking semantics).

var sender = new Sender<int>();
sender.ProcessMessage<int>(x => Console.WriteLine($"Fast path payload: {x}"));
sender.Send(7);

// Expected output:
// Fast path payload: 7
```

### Weak references via ConnectTo(..., weakReference: true)

```csharp
var source = new Sender<string>();
var sink = new ProcessingEndpoint<string>(msg => Console.WriteLine($"Weak sink got: {msg}"));

source.ConnectTo(sink, weakReference: true);
source.Send("hello");

// Expected output:
// Weak sink got: hello
```

### SenderExtensions and ReceiverExtensions

```csharp
var sender = new Sender();
sender.ProcessMessage<IResponseMessage<string>>(r =>
    Console.WriteLine($"Response {r.RequestId}: {r.Content}"));

sender.SendResponse("ok", requestId: 123);

// Expected output:
// Response 123: ok
```

```csharp
var receiver = new QueueReceiver<int>();

_ = Task.Run(async () =>
{
    await Task.Delay(50);
    receiver.Post(99);
});

var (ok, value) = await receiver.TryReceiveAsync(TimeSpan.FromMilliseconds(200));
Console.WriteLine($"Received: {ok}, Value: {value}");

// Expected output:
// Received: True, Value: 99
```

## Why MessageFlow Over Alternatives

### Advantages

1. Unified abstraction for pipeline construction.
2. Everything can be a message: no framework-specific base class or special message type is required.
3. Strong composition with explicit routing semantics.
4. Built-in operational patterns (queueing, delay, dedup, triggering, probing).
5. Performance-oriented forwarding paths and low-allocation helper patterns.
6. First-class sync plus async/await support in the same flow model.
7. Runtime-flexible graph rewiring, including weak links.
8. Testability and diagnosability through composable endpoints.
9. Thread-safe n-to-m connectability patterns.

### Performance Highlights

- High-throughput in-process forwarding:
    MessageFlow is optimized for fast, in-memory dispatch without network or broker overhead.
- Sync and async/await without model switching:
    You can combine direct synchronous forwarding with `Task`-based async stages in one topology, enabling low-latency fast paths and non-blocking awaits where needed.
- Low-allocation design in hot paths:
    Forwarding helpers and typed APIs are built to minimize runtime overhead in common flow operations.
- Efficient fan-out/fan-in composition:
    Complex n-to-m topologies can be composed while keeping execution local and lightweight.
- Operational controls without external infrastructure:
    Queueing, delay, deduplication, and routing are implemented as composable in-process nodes rather than expensive distributed hops.
- Performance and clarity together:
    You keep explicit topology modeling (`Hub`, `Junction`, connectors) without sacrificing runtime efficiency.

### Most Outstanding Features in Practice

1. First-class routing with explicit topology components:
    `Hub` and `Junction` let you model broadcast, prioritized routing, and multi-match routing directly in code instead of scattering logic across handlers.
2. Everything can be a message:
    You can send plain domain objects, primitives, DTOs, or wrappers without inheriting from framework-specific message base types.
3. Rich operational connectors out of the box:
    Components like `QueueForwarder`, `DelayingForwarder`, `DuplicateMessageSuppressor<T>`, `BatchMessages`, and `DeactivatableForwarder` cover many production patterns without extra frameworks.
4. Unified sync and async/await execution model:
    MessageFlow supports synchronous posting and asynchronous awaiting (`PostAsync`, `WaitAsync`, `TryReceiveAsync`, async request/reply) without forcing separate frameworks or duplicated pipelines.
5. Unified request/reply and fire-and-forget model:
    `RequestSender<REQ, RESP>`, `IRequestMessage<T>`, and `IResponseMessage<T>` allow correlated request/response inside the same composable graph used for normal event flow.
6. Concurrency-friendly dynamic wiring:
    MessageFlow is designed for thread-safe posting and n-to-m graph composition, while still allowing runtime connect/disconnect for evolving system state.
7. Built-in observability primitives:
    `MessageCounter`, `MessageLog<T>`, `ConditionalTrigger<T, R>`, and `StatisticsMessageProbe<T1, T2>` make it easier to measure and test behavior without intrusive instrumentation.
8. Low-friction custom extensions:
    `SourceHelper`/`TypedSourceHelper<T>` and extension APIs (`MessageFlowExtensions`, `SenderExtensions`, `ReceiverExtensions`) reduce boilerplate when building custom components.
9. Topic-aware and wrapper-aware message handling:
    `TopicMessageWrapper<T>`, `FilterByTopic(...)`, and unwrap helpers support metadata-rich messaging while preserving strongly typed payload pipelines.

### Comparison with common alternatives

- Versus C# events/delegates:
    Events are lightweight for simple pub/sub, but become hard to manage for complex flows. MessageFlow adds explicit routing (`Hub`, `Junction`), typed transformations, queueing/backpressure, and operational connectors while still maintaining high in-process performance.

- Versus `System.Threading.Channels` only:
    Channels are excellent transport primitives for producer/consumer pipelines. MessageFlow preserves in-process performance characteristics while adding graph-level composition, dynamic fan-out/fan-in wiring, conditional routing, request/reply correlation, and seamless sync plus async/await usage in one model.

- Versus Reactive Extensions (Rx):
    Rx is powerful for declarative stream queries. MessageFlow is often simpler when your architecture is endpoint-and-connector oriented and you need explicit operational nodes (queues, delay gates, dedup, triggers, probes), runtime rewiring, and predictable low-overhead in-process execution.

- Versus MediatR-style dispatch:
    MediatR focuses on request/notification dispatch to handlers. MessageFlow focuses on composable topologies where transformation, routing, throttling, buffering, and observation are first-class pipeline elements, with efficient forwarding for high-frequency internal message traffic.

- Versus broker-based messaging systems (e.g., RabbitMQ/Kafka) for in-process scenarios:
    External brokers provide durability and inter-process distribution, but add operational complexity and network boundaries. MessageFlow is ideal when you need very fast, in-memory, in-process composition with rich control flow, low latency, and minimal infrastructure.

### When to choose MessageFlow

Use MessageFlow when you need:
- explicit in-process message pipelines with dynamic wiring
- high-performance, low-latency in-process messaging on hot paths
- mixed synchronous and async/await stages with bounded queues
- request/reply and broadcast in one architecture
- operational connectors without external infrastructure
- explicit routing semantics via `Hub`/`Junction` rather than ad-hoc branching
- built-in diagnostics and flow control as composable nodes

Use alternatives when you need:
- durable cross-process/event-stream guarantees (choose broker/event-stream platforms)
- pure async queue transport without rich graph/routing semantics (channels may be enough)
- highly query-driven stream transformations as the central paradigm (Rx may fit better)
