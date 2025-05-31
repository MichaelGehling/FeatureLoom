using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Text;
using FeatureLoom.Serialization;

namespace FeatureLoom.TCP
{

    public class TcpClientEndpoint : Workflow<TcpClientEndpoint.StateMachine>, IMessageSink, IMessageSource, IRequester, IReplier
    {
        public class StateMachine : StateMachine<TcpClientEndpoint>
        {
            protected override void Init()
            {
                var preparing = State("Preparing");
                var waitingForActivation = State("WaitingForActivation");
                var reconnecting = State("Reconnecting");
                var observingConnection = State("ObservingConnection");

                preparing.Build()
                    .Step("Check initially for config update and try connect")
                        .Do(async c =>
                        {
                            await c.UpdateConfig(c.initialConfigUpdate).ConfigureAwait(false);
                            c.initialConfigUpdate = false;
                        })
                    .Step("If set inactive goto waiting for activation")
                        .If(c => !c.config.active)
                            .Goto(waitingForActivation)
                    .Step("If connected go to observing connection, otherwise go to reconnecting")
                        .If(c => c.connection?.Connected ?? false)
                            .Goto(observingConnection)
                        .Else()
                            .Goto(reconnecting);

                reconnecting.Build()
                    .Step("Reset connection wait event")
                        .Do(c => c.connectionWaitEvent.Reset())
                    .Step("Wait for reconnection timer or a config change")
                        .WaitFor(c => c.config.SubscriptionWaitHandle, c => c.reconnectionCheckTimer.Remaining().ClampLow(TimeSpan.Zero))
                    .Step("If config change available, apply it, eventually try to reconnect")
                        .If(c => c.config.HasSubscriptionUpdate)
                            .Do(async c => await c.UpdateConfig(false).ConfigureAwait(false))
                    .Step("If set inactive goto waiting for activation")
                        .If(c => !c.config.active)
                            .Goto(waitingForActivation)
                    .Step("If disconnected and reconnection timer elapsed try to reconnect and reset timer")
                        .If(c => c.connection?.Disconnected ?? true && c.reconnectionCheckTimer.Elapsed())
                            .Do(async c => await c.TryConnect(false).ConfigureAwait(false))
                    .Step("If connected go to observing connection, otherwise continue reconnecting")
                        .If(c => c.connection?.Connected ?? false)
                            .Goto(observingConnection)
                        .Else()
                            .Loop();

                observingConnection.Build()
                    .Step("Wait for disconnection or a config change")
                        .WaitForAny(c => c.disconnectionAndConfigUpdateWaitHandles)
                    .Step("If config change available, apply it")
                        .If(c => c.config.HasSubscriptionUpdate)
                            .Do(async c => await c.UpdateConfig(false).ConfigureAwait(false))
                    .Step("If set inactive goto waiting for activation")
                        .If(c => !c.config.active)
                            .Goto(waitingForActivation)
                    .Step("If disconnected go to reconnection state, otherwise continue observing")
                        .If(c => c.connection?.Disconnected ?? true)
                            .Goto(reconnecting)
                        .Else()
                            .Loop();

                waitingForActivation.Build()
                    .Step("Reset connection wait event")
                        .Do(c => c.connectionWaitEvent.Reset())
                    .Step("Wait for a config change")
                        .WaitFor(c => c.config.SubscriptionWaitHandle)
                    .Step("Apply config change and try to connect if active")
                        .Do(async c => await c.UpdateConfig(false).ConfigureAwait(false))
                    .Step("If still inactive continue waiting")
                        .If(c => !c.config.active)
                            .Loop()
                    .Step("If connected go to observing connection, otherwise go to reconnecting")
                        .If(c => c.connection?.Connected ?? false)
                            .Goto(observingConnection)
                        .Else()
                            .Goto(reconnecting);
            }
        }

        public class Config : Configuration
        {
            public bool resolveByDns = true;
            public string hostAddress = "localhost";
            public int port = 3210;
            public bool active = true;

            public TimeSpan reconnectionCheckTime = 2.Seconds();

            public int receivingBufferSize = 10_000;
            public string serverCertificateName = null;
            public bool ignoreFailedServerAuthentication = false;
        }

        private Config config = new Config();

        private readonly ITcpMessageEncoder encoder;
        private readonly ITcpMessageDecoder decoder;

        private TimeFrame reconnectionCheckTimer = new TimeFrame(0.Seconds());
        private bool initialConfigUpdate = true;

        private QueueForwarder<object> sendingSink = new QueueForwarder<object>();
        private readonly Forwarder receivedMessageSource = new Forwarder();

        private MessageConverter<object, byte[]> messageEncoder;
        private TcpConnection connection;
        private IAsyncWaitHandle[] disconnectionAndConfigUpdateWaitHandles = new IAsyncWaitHandle[2];

        private AsyncManualResetEvent connectionWaitEvent = new AsyncManualResetEvent(false);
        public IAsyncWaitHandle ConnectionWaitHandle => connectionWaitEvent;

        public TcpClientEndpoint(Config config = null, ITcpMessageEncoder encoder = null, ITcpMessageDecoder decoder = null, bool autoRun = true)
        {
            if (config == null) config = new Config();
            this.config = config;
            if (encoder == null || decoder == null)
            {
                DefaultTcpMessageEncoderDecoder defaultEncoderDecoder = new DefaultTcpMessageEncoderDecoder();
                if (encoder == null) encoder = defaultEncoderDecoder;
                if (decoder == null) decoder = defaultEncoderDecoder;
                this.encoder = encoder;
                this.decoder = decoder;
            }

            messageEncoder = new MessageConverter<object, byte[]>(msg => encoder.Encode(msg));
            sendingSink.ConnectTo(messageEncoder);

            if (autoRun) this.Run();
        }

        public void DisconnectFromTcpServer()
        {
            if (IsConnectedToServer)
            {
                config.active = false;
                connection.Stop();
            }
        }

        private async Task<bool> UpdateConfig(bool initial)
        {
            bool result = false;
            var oldConfig = config;
            if (await config.TryUpdateFromStorageAsync(true).ConfigureAwait(false) || initial)
            {
                if (!initial) OptLog.INFO()?.Build("Loading updated configuration!");

                if (initial || oldConfig.reconnectionCheckTime != config.reconnectionCheckTime)
                {
                    DateTime now = AppTime.Now;
                    reconnectionCheckTimer = new TimeFrame(now, config.reconnectionCheckTime - reconnectionCheckTimer.TimeSinceStart(now));
                }

                if (initial || ConfigChanged(oldConfig))
                {
                    result = true;
                    if (config.active)
                    {
                        await TryConnect(initial).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!initial)
                        {
                            OptLog.INFO()?.Build($"TCP Client deactivated, connection will be disconnected.");
                            connection?.Stop();
                        }
                    }
                }
            }

            return result;
        }

        private bool ConfigChanged(Config oldConfig)
        {
            return oldConfig.port != config.port ||
                   oldConfig.hostAddress != config.hostAddress ||
                   oldConfig.resolveByDns != config.resolveByDns ||
                   oldConfig.active != config.active;
        }

        private async Task TryConnect(bool initial)
        {
            try
            {
                IPAddress ipAddress = await config.hostAddress.ResolveToIpAddressAsync(config.resolveByDns).ConfigureAwait(false);

                if (connection != null)
                {
                    messageEncoder.DisconnectFrom(connection.SendingSink);
                    connection.Stop();
                    connection = null;
                }

                OptLog.INFO()?.Build($"Trying to connect to {ipAddress}:{config.port.ToString()}");
                TcpClient newClient = new TcpClient(ipAddress.AddressFamily);
                await newClient.ConnectAsync(ipAddress, config.port).ConfigureAwait(false);
                if (newClient.Connected)
                {
                    IStreamUpgrader sslUpgrader = null;
                    if (config.serverCertificateName != null) sslUpgrader = new ClientSslStreamUpgrader(config.serverCertificateName, config.ignoreFailedServerAuthentication);
                    connection = new TcpConnection(newClient, decoder, config.receivingBufferSize, false, sslUpgrader);
                    connection.ReceivingSource.ConnectTo(receivedMessageSource);
                    messageEncoder.ConnectTo(connection.SendingSink, true);
                    connectionWaitEvent.Set();
                }
            }
            catch (SocketException e)
            {
                OptLog.INFO()?.Build($"TcpConnection could not be established to target hostname {config.hostAddress} and port {config.port}! Connection will be retried!", e);
                connection?.Stop();
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"TcpConnection failed with target hostname {config.hostAddress} and port {config.port}, due to a general problem!", e);
                connection?.Stop();
            }

            if (connection?.Connected ?? false)
            {
                disconnectionAndConfigUpdateWaitHandles[0] = config.SubscriptionWaitHandle;
                disconnectionAndConfigUpdateWaitHandles[1] = connection.DisconnectionWaitHandle;
            }
            else
            {
                disconnectionAndConfigUpdateWaitHandles[0] = null;
                disconnectionAndConfigUpdateWaitHandles[1] = null;
            }
            reconnectionCheckTimer = new TimeFrame(config.reconnectionCheckTime);
        }

        private IMessageSink SendingToTcpSink => sendingSink;
        private IMessageSource ReceivingFromTcpSource => receivedMessageSource;

        public bool IsConnectedToServer => connection?.Connected ?? false;

        public int CountConnectedSinks => ReceivingFromTcpSource.CountConnectedSinks;

        public void DisconnectFrom(IMessageSink sink)
        {
            ReceivingFromTcpSource.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ReceivingFromTcpSource.DisconnectAll();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return ReceivingFromTcpSource.GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            SendingToTcpSink.Post(in message);
        }

        public void Post<M>(M message)
        {
            SendingToTcpSink.Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return SendingToTcpSink.PostAsync(message);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            ReceivingFromTcpSource.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return ReceivingFromTcpSource.ConnectTo(sink, weakReference);
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public bool IsConnected(IMessageSink sink)
        {
            return messageEncoder.IsConnected(sink);
        }
    }
}