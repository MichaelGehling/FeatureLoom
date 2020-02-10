using FeatureFlowFramework.Aspects;
using FeatureFlowFramework.Aspects.AppStructure;
using FeatureFlowFramework.DataStorage;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.TCP
{
    public class TcpServerEndpoint : Workflow<TcpServerEndpoint.StateMachine>, IDataFlowSink, IDataFlowSource
    {
        public class StateMachine : StateMachine<TcpServerEndpoint>
        {
            protected override void Init()
            {
                var preparing = State("Preparing");
                var running = State("Running");

                preparing.Build()
                    .Step("Check for config update and set up TCP server")
                        .Do(async c =>
                        {
                            await c.UpdateConfigAsync(c.initialConfigUpdate);
                            c.initialConfigUpdate = false;
                        })
                    .Step("If server was successfully started, begin waiting for connection requests, else wait for for a config change")
                        .If(c => c.listner != null)
                            .Goto(running)
                        .Else()
                            .WaitFor(c => c.config.SubscriptionWaitHandle)
                    .Step("Retry")
                        .Loop();

                running.Build()
                    .Step("Get awaited tasks from connectionRequest and config subscription")
                        .Do(c =>
                        {
                            c.awaited.Clear();
                            if (c.awaitedConnectionRequest?.IsCompleted ?? true)
                            {
                                c.awaitedConnectionRequest = c.listner.AcceptTcpClientAsync();
                                c.awaited.Add(AsyncWaitHandle.FromTask(c.awaitedConnectionRequest));
                            }
                            c.awaited.Add(c.config.SubscriptionWaitHandle);
                            foreach(var con in c.connections)
                            {
                                c.awaited.Add(con.DisconnectionWaitHandle);
                            }
                        })
                    .Step("Wait for connection request or other event")
                        .WaitForAny(c => c.awaited.ToArray())
                    .Step("If config update available apply it and if TCP server has to be restarted restart running state")
                        .If(async c => c.config.HasSubscriptionUpdate && await c.UpdateConfigAsync(false))
                            .Loop()
                    .Step("Prepare new connection if requested")
                        .If(c => c.awaitedConnectionRequest.IsCompleted)
                            .Do(c =>
                            {
                                IStreamUpgrader sslUpgrader = null;
                                if (c.serverCertificate != null) sslUpgrader = new ServerSslStreamUpgrader(c.serverCertificate);
                                var connection = new TcpConnection(c.awaitedConnectionRequest.Result, c.decoder, c.config.receivingBufferSize, c.config.addRoutingWrapper, sslUpgrader);
                                c.connectionForwarder.ConnectTo(connection.SendingSink);
                                connection.ReceivingSource.ConnectTo(c.receivedMessageSource);
                                c.connections.Add(connection);
                                c.connectionWaitEvent.Set();
                            })
                    .Step("Stop and remove disconnected clients if timer expired")
                        .If(c =>
                        {
                            foreach(var conn in c.connections)
                            {
                                if(conn.Disconnected) return true;
                            }
                            return false;
                        })
                            .Do(c =>
                            {
                                c.connections.RemoveAll(connection =>
                                {
                                    if (connection.Disconnected)
                                    {
                                        connection.Stop();
                                        c.connectionForwarder.DisconnectFrom(connection.SendingSink);
                                        return true;
                                    }
                                    return false;
                                });
                                if (c.connections.Count == 0) c.connectionWaitEvent.Reset();
                            })
                    .Step("Continue waiting for next events")
                        .Loop();
            }
        }

        public class Config : Configuration
        {
            public bool resolveByDns = true;
            public string hostAddress = "localhost";
            public int port = 3210;
            public bool active = true;

            public int receivingBufferSize = 10_000;
            public bool addRoutingWrapper = false;
            public string x509CertificateName = null;
        }

        private Config config = new Config();

        private readonly ITcpMessageEncoder encoder;
        private ITcpMessageDecoder decoder;

        private bool initialConfigUpdate = true;
        private TcpListener listner;
        private List<TcpConnection> connections = new List<TcpConnection>();

        private X509Certificate2 serverCertificate = null;

        private ActiveForwarder sendingSink = new ActiveForwarder();
        private readonly MessageConverter<object, object> messageEncoder;
        private AsyncForwarder connectionForwarder = new AsyncForwarder();

        private Forwarder receivedMessageSource = new Forwarder();

        private Task<TcpClient> awaitedConnectionRequest;
        private List<IAsyncWaitHandle> awaited = new List<IAsyncWaitHandle>();

        private AsyncManualResetEvent connectionWaitEvent = new AsyncManualResetEvent(false);
        public IAsyncWaitHandle ConnectionWaitHandle => connectionWaitEvent.AsyncWaitHandle;

        public TcpServerEndpoint(Config config = null, ITcpMessageEncoder encoder = null, ITcpMessageDecoder decoder = null)
        {
            if (config == null) config = new Config();
            this.config = config;
            if (encoder == null || decoder == null)
            {
                DefaultTcpMessageEncoderDecoder defaultEncoderDecoder = new DefaultTcpMessageEncoderDecoder();
                if (encoder == null) encoder = defaultEncoderDecoder;
                if (decoder == null) decoder = defaultEncoderDecoder;
            }
            this.encoder = encoder;
            this.decoder = decoder;

            messageEncoder = new MessageConverter<object, object>(msg => EncodeMessage(msg));
            sendingSink.ConnectTo(messageEncoder).ConnectTo(connectionForwarder);

            this.Run();
        }

        private object EncodeMessage(object msg)
        {
            if (msg is ConnectionRoutingWrapper wrapper)
            {
                wrapper.Message = encoder.Encode(wrapper.Message);
                if (wrapper.Message == null) return null;
                return wrapper;
            }
            else
            {
                return encoder.Encode(msg);
            }
        }

        private async Task<bool> UpdateConfigAsync(bool initial)
        {
            bool result = false;
            var oldConfig = config;
            if (config.TryUpdateFromStorage(true) || initial)
            {
                if (!initial) Log.INFO(this, "Loading updated configuration!");

                if (initial || ConfigChanged(oldConfig))
                {
                    result = true;
                    if (config.active) await RestartServer(initial);
                    else if (!initial) DeactivateServer();
                }
            }
            return result;
        }

        private void DeactivateServer()
        {
            Log.INFO(this, $"TCP Server deactivated. {connections.Count} connections will be disconnected!");
            foreach (var connection in connections) connection.Stop();
            connections.Clear();
            connectionForwarder.DisconnectAll();
            listner?.Stop();
            listner = null;
        }

        private async Task RestartServer(bool initial)
        {
            try
            {
                if (config.x509CertificateName != null) Storage.GetReader("certificate").TryRead(config.x509CertificateName, out this.serverCertificate);

                IPAddress ipAddress = await config.hostAddress.ResolveToIpAddressAsync(config.resolveByDns);

                if (!initial)
                {
                    Log.WARNING(this, $"TCP Server reset. {connections.Count} connections will be disconnected!");
                    foreach (var connection in connections) connection.Stop();
                    connections.Clear();
                    connectionForwarder.DisconnectAll();
                }
                listner?.Stop();
                listner = new TcpListener(ipAddress, config.port);
                listner.Start();
                Log.TRACE(this, $"TCP server started with hostname {config.hostAddress} and port {config.port}.");
            }
            catch (Exception e)
            {
                Log.ERROR($"TcpListner failed to start with hostname {config.hostAddress} and port {config.port}! {connections.Count} connections will be disconnected!", e.ToString());
                listner?.Stop();
                listner = null;
                foreach (var connection in connections) connection.Stop();
                connections.Clear();
                connectionForwarder.DisconnectAll();
            }
        }

        private bool ConfigChanged(Config oldConfig)
        {
            return oldConfig.port != config.port ||
                   oldConfig.hostAddress != config.hostAddress ||
                   oldConfig.resolveByDns != config.resolveByDns ||
                   oldConfig.active != config.active ||
                   oldConfig.x509CertificateName != config.x509CertificateName;
        }

        private IDataFlowSink SendingToTcpSink => sendingSink;
        private IDataFlowSource ReceivingFromTcpSource => receivedMessageSource;

        public int CountConnectedClients => connections.Count;

        public int CountConnectedSinks => ReceivingFromTcpSource.CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink)
        {
            ReceivingFromTcpSource.ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ReceivingFromTcpSource.ConnectTo(sink);
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ReceivingFromTcpSource.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ReceivingFromTcpSource.DisconnectAll();
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ReceivingFromTcpSource.GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            SendingToTcpSink.Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return SendingToTcpSink.PostAsync(message);
        }

        public override bool TryUpdateAppStructureAspects(TimeSpan timeout)
        {
            var childrenInterface = this.GetAspectInterface<IAcceptsChildren, AppStructureAddOn>();
            childrenInterface.AddChild(config, "TcpServerConfig");
            childrenInterface.AddChild(encoder, "Encoder");
            childrenInterface.AddChild(decoder, "Decoder");
            childrenInterface.AddChild(sendingSink, "SendingSink");
            childrenInterface.AddChild(receivedMessageSource, "ReceivedMessageSource");
            childrenInterface.AddChild(messageEncoder, "MessageEncoder");
            childrenInterface.AddChild(connectionForwarder, "ConnectionForwarder");
            foreach (var connection in connections) childrenInterface.AddChild(connection, "Connection " + connection.GetAspectHandle());

            base.TryUpdateAppStructureAspects(timeout);
            return true;
        }
    }
}