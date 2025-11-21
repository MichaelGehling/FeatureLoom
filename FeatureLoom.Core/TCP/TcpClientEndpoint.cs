using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Time;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System.Threading;

namespace FeatureLoom.TCP
{
    public sealed class TcpClientEndpoint : IMessageSink, IMessageSource, IRequester, IReplier, IDisposable
    {
        public class Settings : Configuration
        {
            public bool resolveByDns = true;
            public string hostAddress = "localhost";
            public int port = 3210;

            public TimeSpan connectionRetryTime = 2.Seconds();
            public bool useConnectionMetaDataForMessages = true;

            public string serverCertificateName = null;
            public bool ignoreFailedServerAuthentication = false;
        }

        Settings settings;
        TcpConnection connection;
        QueueForwarder writeForwarder = new QueueForwarder();
        QueueForwarder readForwarder = new QueueForwarder();
        CancellationTokenSource cts;
        public IMessageSink MessagesToSend => writeForwarder;
        public IMessageSink ReceivedMessages => readForwarder;

        public int CountConnectedSinks => readForwarder.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => readForwarder.NoConnectedSinks;

        public bool IsConnected => connection != null && connection.IsConnected;
        Func<IGeneralMessageStreamReader> createStreamReader = () => new VariantStreamReader(null, new TypedJsonMessageStreamReader());
        Func<IGeneralMessageStreamWriter> createStreamWriter = () => new VariantStreamWriter(null, new TypedJsonMessageStreamWriter());

        private AsyncManualResetEvent connectionWaitEvent = new AsyncManualResetEvent(false);
        public IAsyncWaitHandle ConnectionWaitHandle => connectionWaitEvent;

        public TcpClientEndpoint(Settings settings = null, bool autoStart = true, Func<IGeneralMessageStreamReader> createStreamReaderAction = null, Func<IGeneralMessageStreamWriter> createStreamWriterAction = null)
        {
            this.settings = settings;
            if (this.settings == null) this.settings = new Settings();
            this.settings.TryUpdateFromStorage(false);

            if (createStreamReaderAction != null) createStreamReader = createStreamReaderAction;
            if (createStreamWriterAction != null) createStreamWriter = createStreamWriterAction;

            if (autoStart) _ = Start();
        }

        public async Task Start()
        {
            cts = new CancellationTokenSource();
            while (!cts.IsCancellationRequested)
            {
                if (!IsConnected) await TryConnect();
                await AppTime.WaitAsync(settings.connectionRetryTime.Multiply(0.9), settings.connectionRetryTime, cts.Token);
            }
            Stop();
        }

        public void Stop()
        {
            cts?.Cancel();
            connectionWaitEvent.Reset();
            writeForwarder.DisconnectAll();
            connection.Close();
            connection = null;
        }

        private async Task<bool> TryConnect()
        {
            try
            {
                IPAddress ipAddress = await settings.hostAddress.ResolveToIpAddressAsync(settings.resolveByDns);

                if (connection != null)
                {
                    connectionWaitEvent.Reset();
                    writeForwarder.DisconnectAll();
                    connection.Close();
                    connection = null;
                }
                if (cts.IsCancellationRequested) return false;

                OptLog.INFO()?.Build($"Trying to connect to {ipAddress}:{settings.port.ToString()}");
                TcpClient newClient = new TcpClient(ipAddress.AddressFamily);
                await newClient.ConnectAsync(ipAddress, settings.port);
                if (newClient.Connected)
                {
                    var reader = createStreamReader();
                    var writer = createStreamWriter();
                    IStreamUpgrader sslUpgrader = null;
                    if (settings.serverCertificateName != null) sslUpgrader = new ClientSslStreamUpgrader(settings.serverCertificateName, settings.ignoreFailedServerAuthentication);

                    connection = new TcpConnection(newClient, reader, writer, settings.useConnectionMetaDataForMessages, sslUpgrader);
                    connection.ReceivedMessages.ConnectTo(readForwarder);
                    writeForwarder.ConnectTo(connection.MessagesToSend, true);
                }
            }
            catch (SocketException e)
            {
                OptLog.INFO()?.Build($"TcpConnection could not be established to target hostname {settings.hostAddress} and port {settings.port}! Connection will be retried!", e);
                connection?.Close();
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"TcpConnection failed with target hostname {settings.hostAddress} and port {settings.port}, due to a general problem!", e);
                connection?.Close();
            }

            if (IsConnected) connectionWaitEvent.Set();
            return IsConnected;
        }


        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public void Post<M>(in M message)
        {
            ((IMessageSink)writeForwarder).Post(message);
        }

        public void Post<M>(M message)
        {
            ((IMessageSink)writeForwarder).Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return ((IMessageSink)writeForwarder).PostAsync(message);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            readForwarder.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return readForwarder.ConnectTo(sink, weakReference);
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            readForwarder.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            readForwarder.DisconnectAll();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return readForwarder.GetConnectedSinks();
        }

        public void Dispose()
        {
            Stop();
        }

        bool IMessageSource.IsConnected(IMessageSink sink)
        {
            return readForwarder.IsConnected(sink);
        }
    }
}