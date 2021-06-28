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
  * **RPC**: Allows remote procedure calls via MessageFlow connections. It is possible to to "fire-and-forget" or even to address multiple targets including receival of mutliple return values. *[FeatureLoom.Core]*
  * **TCP**: MessageFlow endpoints allowing messaging via TCP servers and clients. *[FeatureLoom.Core]*
  * **Web**: A Kestrel based webserver that can be used to define web endpoints. Also contains MessageFlow endpoints and REST interface for Storage Interface. *[FeatureLoom]*
  * **Diagnostics**: *[FeatureLoom.Core]*

* *Logic:*
  * **Extensions** *[FeatureLoom.Core]*
  * **Helpers** *[FeatureLoom.Core]*
  * **Workflows** *[FeatureLoom.Core]*
  * **Supervision** *[FeatureLoom.Core]*
  * **Synchronization** *[FeatureLoom.Core]*
  * **Time** *[FeatureLoom.Core]*

* *Data:*
  * **Serialization** *[FeatureLoom.Core]*
  * **Storage** *[FeatureLoom.Core]*
  * **Collections** *[FeatureLoom.Core]*
  * **MetaData** *[FeatureLoom.Core]*
  * **Logging** Logging service interface that can be extended via MessageFlow. Includes different loggers (FileLogger, ConsoleLogger, InMemoryLogger) *[FeatureLoom.Core]*

* *UI:*
  * **Forms**: Several Helpers for Windows Forms and some Controls (Logging Window, Property Control) *[FeatureLoom.Forms]* 
