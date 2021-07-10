# FeatureLoom
![FeatureLoom Image](https://raw.githubusercontent.com/MichaelGehling/FeatureLoom/master/Resources/FeatureLoom_256.png)

FeatureLoom is a C# development framework that focuses on a lean development and efficient code:
- FeatureLoom speeds-up development by simplification while offering opt-in for a high level of control
- FeatureLoom promotes modular and extensible code by introducing means to easily decouple components with asynchronous communication
- FeatureLoom strives for high performance and a small memory footprint
- FeatureLoom equally supports synchronous and asynchronous programming to allow the right approach at the right time

FeatureLoom comprises the following functional facets:
* *Communication:*
  * **MessageFlow**: A very lean local messaging concept consisting of senders, receivers, functional connectors and endpoints that allow synchronous and asyncronous messaging. *[FeatureLoom.Core]*
  * **RPC**: Allows remote procedure calls via MessageFlow connections. Beside normal calls with result response, it is also possible to "fire-and-forget" or even to address multiple targets including receival of multiple return values. *[FeatureLoom.Core]*
  * **TCP**: MessageFlow endpoints allowing messaging via TCP servers and clients. *[FeatureLoom.Core]*
  * **Web**: A Kestrel based webserver that can be used to define web endpoints. Also contains MessageFlow endpoints and REST interface for Storage Interface. *[FeatureLoom]*
  * **Diagnostics**: Helpers and MessageFlow elements to support testing and to allow runtime statistics of MessageFlow connections. *[FeatureLoom.Core]*

* *Logic:*
  * **Extensions**: A large number of various extension methods to simplify and speed-up implementations. *[FeatureLoom.Core]*
  * **Helpers**: A collection of small helpers (e.g. AsyncOut, LazyValue, UsingHelper, GenericComparer) up to powerful tools (e.g. UndoRedo, Factory, ServiceContext). *[FeatureLoom.Core]*
  * **Workflows**: Create highly efficient and performant executable state machines via a very powerful builder pattern. Can be executed in synchronous and asynchronous contexts and also allow for step-by-step execution. *[FeatureLoom.Core]*
  * **Supervision**: Service allowing to register supervision jobs that are cyclically executed with individual cycle times in a shared thread. *[FeatureLoom.Core]*
  * **Synchronization**: Several synchronization features, including extremly performant locks (FeatureLock, MicroValueLock), async features (e.g. AsyncWaitHandle, AsyncManualResetEvent and shared data helpers *[FeatureLoom.Core]*
  * **Time**: A time service (AppTime), extensions to nicely write time values (e.g. 42.Seconds()) and some time measurement helpers (TimeKeeper, TimerFrame) *[FeatureLoom.Core]*

* *Data:*  
  * **Storage**: A generic storage service interface that allows reading and writing objects (identified by URIs) and even allows for change subscriptions with URI filters. Also includes some concrete implementations (TextFileStorage, MemoryStorage, CertificateStorageReader) *[FeatureLoom.Core]*  
  * **MetaData**: A service interface that allows attaching any kind of meta data to any existing object without changing it. An object handle allows to identify registered objects from outside (e.g. for logging context) *[FeatureLoom.Core]*
  * **Logging**: A Logging service interface that can be extended via MessageFlow. Includes different loggers (FileLogger, ConsoleLogger, InMemoryLogger) *[FeatureLoom.Core]*
  * **Collections**: A few additional collections (CountingRingBuffer, InMemoryCache, PriorityQueue) *[FeatureLoom.Core]*
  * **Serialization**: Currently just JSON support (based on Newtonsoft.Json). This will be changed to a more generic serialization concept in the future. *[FeatureLoom.Core]*

* *UI:*
  * **Forms**: Several Helpers for Windows Forms and some Controls (Logging Window, Property Control) *[FeatureLoom.Forms]* 
