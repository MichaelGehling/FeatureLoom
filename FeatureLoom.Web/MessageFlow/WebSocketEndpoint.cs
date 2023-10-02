using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{

    public class WebSocketEndpoint : IMessageSource, IMessageSink, IDisposable
    {
        public readonly struct WebSocketControlMessage
        {
            public readonly Command command;
            public readonly ObjectHandle endpointHandle;

            public WebSocketControlMessage(Command command)
            {
                this.command = command;
                endpointHandle = 0;
            }

            public WebSocketControlMessage(Command command, ObjectHandle endpointId)
            {
                this.command = command;
                this.endpointHandle = endpointId;
            }

            public override string ToString()
            {
                return $"{endpointHandle}:{command.ToName()}";
            }
        }
        public enum Command
        {
            Close,
            Closed,
            Created
        }

        SourceHelper sourceHelper = new SourceHelper();
        WebSocket webSocket;
        MemoryStream writeBuffer = new MemoryStream();
        JsonTextWriter jsonWriter;
        FeatureLock writeLock = new FeatureLock();
        CancellationTokenSource cts = new CancellationTokenSource();
        bool closed;
        int readBufferSize = 4096;
        ObjectHandle endpointHandle;

        public const string META_DATA_CONNECTION_KEY = "WebSocketHandle";

        Type deserializationType;
        AsyncManualResetEvent closeEvent = new AsyncManualResetEvent(false);
        bool useConnectionMetaDataForMessages;

        public WebSocketEndpoint(WebSocket webSocket, Type deserializationType = null, bool useConnectionMetaDataForMessages = true, int readBufferSize = 4096) 
        { 
            endpointHandle = this.GetHandle();
            jsonWriter = new JsonTextWriter(new StreamWriter(writeBuffer));


            if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));
            this.deserializationType = deserializationType;
            this.readBufferSize = readBufferSize;
            this.webSocket = webSocket;
            this.useConnectionMetaDataForMessages = useConnectionMetaDataForMessages;

            StartListeningAsync();
        }

        public IAsyncWaitHandle CloseEventWaitHandle => closeEvent;

        public bool IsClosed => closed;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        async void StartListeningAsync()
        {
            await Task.Yield();
            sourceHelper.Forward(new WebSocketControlMessage(Command.Created, endpointHandle));

            var readBuffer = new ArraySegment<byte>(new byte[readBufferSize]);
            using (var ms = new MemoryStream())
            using (var streamReader = new StreamReader(ms))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                while (!closed)
                {
                
                    try
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            cts.Token.ThrowIfCancellationRequested();

                            result = await webSocket.ReceiveAsync(readBuffer, cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                throw new WebSocketException("WebSocket connection closed by the remote endpoint.");
                            }

                            ms.Write(readBuffer.Array, readBuffer.Offset, result.Count);

                        } while (!result.EndOfMessage);

                    }
                    catch (Exception ex)
                    {
                        if (!this.closed)
                        {
                            if (webSocket != null && webSocket.State > WebSocketState.Open && webSocket.State < WebSocketState.Aborted)
                            {
                                Log.INFO(this.GetHandle(), "Websocket was closed", ex.ToString());
                            }
                            else
                            {
                                Log.ERROR(this.GetHandle(), "Websocket connection failed while listenting", ex.ToString());
                            }
                            Dispose();
                        }
                        return;
                    }

                    try
                    {
                        ms.Position = 0;
                        object message;
                        message = streamReader.ReadToEnd();
                        if (deserializationType != null) message = Json.DeserializeFromJson(message.ToString(), deserializationType); //TODO Optimize, serializer should directly work on stream, but last try failed and the smae message was read again and again.

                        if (useConnectionMetaDataForMessages) message.SetMetaData(META_DATA_CONNECTION_KEY, this.endpointHandle);
                        _ = sourceHelper.ForwardAsync(message);

                        ms.Position = 0;
                        ms.SetLength(0);
                    }
                    catch (Exception ex)
                    {
                        Log.ERROR(this.GetHandle(), "Failed to deserialize or forwarding message", ex.ToString());
                    }
                }
            }
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            PostAsync(message).WaitFor();
        }

        public void Post<M>(M message)
        {
            PostAsync(message).WaitFor();
        }

        public async Task PostAsync<M>(M message)
        {
            if (message is WebSocketControlMessage controlMessage)
            {
                HandleControlMessage(controlMessage);
                return;
            }

            if (closed)
            {
                Log.WARNING(this.GetHandle(), "Tried to send message via closed WebSocketEndpoint");
                return;
            }
            if (webSocket.State > WebSocketState.Open)
            {
                Dispose();
                return;
            }            

            using (await writeLock.LockAsync())
            {
                Json.Default_Serializer.Serialize(jsonWriter, message);
                jsonWriter.Flush();
                await webSocket.SendAsync(new ArraySegment<byte>(writeBuffer.GetBuffer(), 0, (int)writeBuffer.Length), WebSocketMessageType.Text, true, cts.Token);
                writeBuffer.Position = 0;
                writeBuffer.SetLength(0);
            }
        }

        public void HandleControlMessage(WebSocketControlMessage controlMessage)
        {
            if (!controlMessage.endpointHandle.IsInvalid && controlMessage.endpointHandle != endpointHandle) return;

            if (controlMessage.command == Command.Close)
            {
                 Dispose();
            }
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true;
            sourceHelper.Forward(new WebSocketControlMessage(Command.Closed, endpointHandle));
            if (webSocket.State < WebSocketState.CloseSent)
            {
                Log.DEBUG(this.GetHandle(), "Sending close request on WebSocket");
                var closeTask = webSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "The endpoint was closed", cts.Token);
                if (!closeTask.Wait(20.Milliseconds()))
                {
                    Log.WARNING(this.GetHandle(), "Close request was not finished within 20 ms, so WebSocket will be disposed anyway");
                }
            }
            closeEvent.Set();
            cts.Cancel();
            cts.Dispose();
            webSocket.Dispose();
            writeBuffer.Dispose();
        }
    }
}
