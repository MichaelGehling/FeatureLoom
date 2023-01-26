using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using FeatureLoom.Services;
using FeatureLoom.Scheduling;
using FeatureLoom.Time;

namespace FeatureLoom.TCP
{
    public sealed class TcpConnection2 : IDisposable
    {
        private readonly TcpClient client;
        private Stream stream;
        IMessageStreamReader reader;
        IMessageStreamWriter writer;
        CancellationTokenSource cts = new CancellationTokenSource();
        Sender receivedMessageSender = new Sender();
        ProcessingEndpoint<object> messageWriter;
        AsyncManualResetEvent closingEvent = new AsyncManualResetEvent(false);
        bool useConnectionMetaDataForMessages;
        ObjectHandle handle;

        public const string META_DATA_CONNECTION_KEY = "TcpConnectionHandle";

        public IAsyncWaitHandle ClosingEvent => closingEvent;
        public bool IsConnected => client.Connected && !cts.Token.IsCancellationRequested;
        public IMessageSource ReceivedMessages => receivedMessageSender;
        public IMessageSink MessagesToSend => messageWriter;

        public TcpConnection2(TcpClient client, IMessageStreamReader reader, IMessageStreamWriter writer, bool useConnectionMetaDataForMessages, IStreamUpgrader streamUpgrader = null)
        {
            handle = this.GetHandle();

            this.client = client;
            this.reader = reader;
            this.writer = writer;
            this.useConnectionMetaDataForMessages = useConnectionMetaDataForMessages;

            try
            {
                stream = streamUpgrader?.Upgrade(client.GetStream()) ?? client.GetStream();
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Could not get stream from tcpClient or stream upgrade failed.", e.ToString());
            }

            messageWriter = new ProcessingEndpoint<object>(async msg => await WriteMessage(msg));
            Task.Run(StartReceiving);
        }

        public static bool TryGetConnectionHandleFromMessage(object message, out ObjectHandle connectionHandle)
        {
            return message.TryGetMetaData(META_DATA_CONNECTION_KEY, out connectionHandle);
        }

        public static void AssignConnectionHandleToMessage(object message, ObjectHandle connectionHandle)
        {
            message.SetMetaData(META_DATA_CONNECTION_KEY, connectionHandle);
        }

        public ObjectHandle GetConnectionHandle() => handle;

        async Task StartReceiving()
        {
            while(IsConnected)
            {
                try
                {
                    object message = await reader.ReadMessage(stream, cts.Token);
                    if (useConnectionMetaDataForMessages) message.SetMetaData(META_DATA_CONNECTION_KEY, handle);
                    receivedMessageSender.Send(message);
                }
                catch (Exception e)
                {
                    Log.ERROR("Receiving on TCP connection failed.", e.ToString());
                }
                if (!IsConnected) break;
            }

            Close();
        }

        async Task WriteMessage(object message)
        {
            if (useConnectionMetaDataForMessages &&
                message.TryGetMetaData(META_DATA_CONNECTION_KEY, out ObjectHandle connectionHandle) &&
                connectionHandle != handle) return;

            if (!IsConnected)
            {
                cts.Cancel();
                Log.WARNING("Message cannot be written, because connection is interrupted.");                
                return;
            }

            try
            {                
                await writer.WriteMessage(message, stream, cts.Token);
            }
            catch (Exception e)
            {
                Log.ERROR("Writing on TCP connection failed.", e.ToString());
            }
        }

        public void Close()
        {
            cts.Cancel();
            client.Close();
            closingEvent.Set();
        }

        public void Dispose()
        {
            if (IsConnected) Close();
            writer.Dispose();
            reader.Dispose();
            stream.Dispose();
            client.Dispose();
        }
    }
}