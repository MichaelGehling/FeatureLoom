﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using FeatureLoom.Time;

namespace FeatureLoom.TCP
{
    public sealed class TcpServerEndpoint : IMessageSink, IMessageSource, IRequester, IReplier, IDisposable
    {
        public class Settings : Configuration
        {
            public bool resolveByDns = true;
            public string hostAddress = "localhost";
            public int port = 3210;
            
            public TimeSpan checkDisconnectionCycleTime = 1.Seconds();
            public bool useConnectionMetaDataForMessages = true;
            public string x509CertificateName = null;
        }
        Settings settings;
        private TcpListener listner;
        private X509Certificate2 serverCertificate = null;
        List<TcpConnection> connections = new List<TcpConnection>();
        FeatureLock connectionsLock = new FeatureLock();
        Func<IGeneralMessageStreamReader> createStreamReader = () => new VariantStreamReader(null, new TypedJsonMessageStreamReader());
        Func<IGeneralMessageStreamWriter> createStreamWriter = () => new VariantStreamWriter(null, new TypedJsonMessageStreamWriter());
        QueueForwarder<object> writeForwarder = new QueueForwarder<object>();
        QueueForwarder<object> readForwarder = new QueueForwarder<object>();
        CancellationTokenSource cts;
        private AsyncManualResetEvent connectionWaitEvent = new AsyncManualResetEvent(false);
        public IAsyncWaitHandle ConnectionWaitHandle => connectionWaitEvent;
        public int CountConnectedClients => connections.Count;
        public int CountConnectedSinks => readForwarder.CountConnectedSinks;

        public TcpServerEndpoint(Settings settings = null, bool autoStart = true, Func<IGeneralMessageStreamReader> createStreamReaderAction = null, Func<IGeneralMessageStreamWriter> createStreamWriterAction = null)
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
            if (listner != null) return;
            await StartServer().ConfiguredAwait();
            await Task.WhenAll(HandleConnectionRequests(), HandleDisconnections()).ConfiguredAwait();
            Stop();
        }

        public void Stop()
        {
            cts?.Cancel();
            DisconnectAllClients();
            listner.Stop();
            listner = null;            
        }

        public bool TryCloseConnection(TcpConnection connection)
        {
            using (connectionsLock.Lock())
            {
                if (!connections.Contains(connection)) return false;

                writeForwarder.DisconnectFrom(connection.MessagesToSend);
                connection.Close();
                connection.Dispose();            
                connections.Remove(connection);
                return true;
            }
        }

        public bool TryCloseConnection(ObjectHandle connectionHandle)
        {
            if (!connectionHandle.TryGetObject(out TcpConnection connection)) return false;
            return TryCloseConnection(connection);
        }

        private async Task StartServer()
        {
            try
            {
                if (settings.x509CertificateName != null) (await Storage.GetReader("certificate").TryReadAsync<X509Certificate2>(settings.x509CertificateName).ConfiguredAwait()).TryOut(out this.serverCertificate);

                IPAddress ipAddress = await settings.hostAddress.ResolveToIpAddressAsync(settings.resolveByDns).ConfiguredAwait();

                if (connections.Count > 0) DisconnectAllClients();

                listner?.Stop();
                listner = new TcpListener(ipAddress, settings.port);
                listner.Start();
                OptLog.TRACE()?.Build($"TCP server started with hostname {settings.hostAddress} and port {settings.port.ToString()}.");
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"TcpListner failed to start with hostname {settings.hostAddress} and port {settings.port.ToString()}!", e.ToString());
                listner?.Stop();
                listner = null;
                DisconnectAllClients();                
            }
        }

        private async Task HandleConnectionRequests()
        {
            while(!cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listner.AcceptTcpClientAsync().ConfiguredAwait();
                    ConnectClient(client);
                }
                catch (InvalidOperationException)
                {
                    // listner was stopped
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build("Error occurred while waiting for client connections", e);
                }
            }
        }

        private async Task HandleDisconnections()
        {
            while (!cts.IsCancellationRequested)
            {
                using (connectionsLock.Lock())
                {
                    for(int i=connections.Count-1; i>=0; i--)
                    {
                        if (!connections[i].IsConnected) connections.RemoveAt(i);
                    }
                    if (connections.Count > 0) connectionWaitEvent.Set();
                    else connectionWaitEvent.Reset();
                }

                await AppTime.WaitAsync(settings.checkDisconnectionCycleTime.Multiply(0.9), settings.checkDisconnectionCycleTime, cts.Token).ConfiguredAwait();
            }
        }

        private void ConnectClient(TcpClient client)
        {
            IStreamUpgrader sslUpgrader = null;
            if (serverCertificate != null) sslUpgrader = new ServerSslStreamUpgrader(serverCertificate);
            TcpConnection connection = new TcpConnection(client, createStreamReader(), createStreamWriter(), settings.useConnectionMetaDataForMessages, sslUpgrader);
            connection.ReceivedMessages.ConnectTo(readForwarder);
            writeForwarder.ConnectTo(connection.MessagesToSend);
            using(connectionsLock.Lock())
            {
                connections.Add(connection);
            }
            connectionWaitEvent.Set();
        }


        private void DisconnectAllClients()
        {
            using (connectionsLock.Lock())
            {
                connectionWaitEvent.Reset();
                writeForwarder.DisconnectAll();
                foreach (var connection in connections)
                {
                    connection.Close();
                    connection.Dispose();
                }
                connections.Clear();                
            }
        }

        public void Dispose()
        {
            Stop();
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

        void IMessageSource.DisconnectAll()
        {
            readForwarder.DisconnectAll();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return readForwarder.GetConnectedSinks();
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public bool IsConnected(IMessageSink sink)
        {
            return readForwarder.IsConnected(sink);
        }
    }
}