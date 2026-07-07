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

- `IMessageFlow`, `IMessageSink`, `IMessageSource`
- `ITypedMessageSink`, `IMessageSink<T>`, `ITypedMessageSource`, `IMessageSource<T>`
- `IMessageFlowConnection`, `IMessageFlowConnection<T>`, `IMessageFlowConnection<I, O>`
- `IAlternativeMessageSource`, `IRequester`, `IReplier`
- `IRequestMessage<T>`, `IResponseMessage<T>`, `RequestMessage<T>`, `ResponseMessage<T>`
- `IMessageWrapper`, `IMessageWrapper<T>`, `TopicMessageWrapper<T>`, `ITopicMessage`
- `ISender`, `ISender<T>`, `IReceiver<T>`
- `ForwardingMethod`

### Connectors

- `Forwarder`, `Forwarder<T>`
- `Filter<T>`, `MessageConverter<I, O>`, `Splitter<T, E>`
- `Aggregator<T>`
- `AsyncForwarder`, `QueueForwarder`, `CurrentContextForwarder<T>`
- `BufferingForwarder<T>`
- `DeactivatableForwarder`
- `DelayingForwarder`, `DuplicateMessageSuppressor<T>`
- `Hub`, `Hub.Socket`, `Junction`

### Endpoints

- `Sender`, `Sender<T>`, `RequestSender<REQ, RESP>`
- `QueueReceiver<T>`, `PriorityQueueReceiver<T>`, `LatestMessageReceiver<T>`, `PriorityMessageReceiver<T>`
- `ReceiverBuffer<T>`, `ValueWrappingQueueReceiver`
- `MessageTrigger`, `ConditionalTrigger<T, R>`, `MessageCounter`
- `MessageLog<T>`, `MessageLogReader<T>`
- `ProcessingEndpoint<T>`, `StatisticsMessageProbe<T1, T2>`

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

### Convert and Split

```csharp
IMessageSource source = new Sender<string>();

source
    .ConvertMessage<string, string[]>(line => line.Split(','))
    .SplitMessage<string[], string>(parts => parts)
    .ProcessMessage<string>(part => Console.WriteLine(part.Trim()));

source.Send("alpha,beta,gamma");

// Expected output:
// alpha
// beta
// gamma
```

### Request/Response with Correlation

```csharp
var requester = new RequestSender<string, int>(timeout: TimeSpan.FromSeconds(2));

requester.ProcessMessage<IRequestMessage<string>>(request =>
{
    var response = new ResponseMessage<int>(request.Content.Length, request.RequestId);
    requester.Post(response);
});

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
2. Strong composition with explicit routing semantics.
3. Built-in operational patterns (queueing, delay, dedup, triggering, probing).
4. Performance-oriented forwarding paths and low-allocation helper patterns.
5. Runtime-flexible graph rewiring, including weak links.
6. Testability and diagnosability through composable endpoints.
7. Thread-safe n-to-m connectability patterns.

### Comparison with common alternatives

- Versus C# events/delegates:
  MessageFlow adds richer routing, transformations, queueing, and operational control.

- Versus `System.Threading.Channels` only:
  Channels are excellent transport primitives; MessageFlow layers graph composition and routing connectors.

- Versus Reactive Extensions (Rx):
  Rx is query-centric; MessageFlow is graph-and-endpoint-centric with explicit operational primitives.

- Versus MediatR-style dispatch:
  MediatR focuses on handler dispatch; MessageFlow focuses on composable flow topologies.

### When to choose MessageFlow

Use MessageFlow when you need:
- explicit in-process message pipelines with dynamic wiring
- mixed sync and async stages with bounded queues
- request/reply and broadcast in one architecture
- operational connectors without external infrastructure
