﻿using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Web;
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
        FeatureLock writeLock = new FeatureLock();
        CancellationTokenSource cts = new CancellationTokenSource();
        bool closed;
        int readBufferSize = 4096;
        ObjectHandle endpointHandle;

        public const string META_DATA_CONNECTION_KEY = "WebSocketHandle";

        Type deserializationType;
        AsyncManualResetEvent closeEvent = new AsyncManualResetEvent(false);
        bool useConnectionMetaDataForMessages;

        
        static FeatureJsonDeserializer.Settings defaultDeserializerSettings = new()
        {
            enableProposedTypes = false,
            enableReferenceResolution = false,
            dataAccess = FeatureJsonDeserializer.DataAccess.PublicAndPrivateFields,
            rethrowExceptions = false,
            strict = false,
            tryCastArraysOfUnknownValues = true            
        };

        static FeatureJsonSerializer.Settings defaultSerializerSettings = new()
        {
            typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            dataSelection = FeatureJsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields,
            enumAsString = true,            
        };

        static FeatureJsonDeserializer defaultDeserializer = new(defaultDeserializerSettings);
        static FeatureJsonSerializer defaultSerializer = new(defaultSerializerSettings);
        FeatureJsonDeserializer deserializer;
        FeatureJsonSerializer serializer;

        public WebSocketEndpoint(WebSocket webSocket, Type deserializationType = null, bool useConnectionMetaDataForMessages = true, int readBufferSize = 4096, FeatureJsonSerializer serializer = null, FeatureJsonDeserializer deserializer = null) 
        { 
            endpointHandle = this.GetHandle();

            if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));
            this.deserializationType = deserializationType;
            this.readBufferSize = readBufferSize;
            this.webSocket = webSocket;
            this.useConnectionMetaDataForMessages = useConnectionMetaDataForMessages;

            this.serializer = serializer ?? defaultSerializer;
            this.deserializer = deserializer ?? defaultDeserializer;

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
                                OptLog.INFO()?.Build("Websocket was closed", ex);
                            }
                            else
                            {
                                OptLog.ERROR()?.Build("Websocket connection failed while listenting", ex);
                            }
                            Dispose();
                        }
                        return;
                    }

                    try
                    {
                        ms.Position = 0;
                        object message;
                        if (deserializationType != null)
                        {
                            while(deserializer.TryDeserialize(ms, deserializationType, out message))
                            {
                                if (useConnectionMetaDataForMessages) message.SetMetaData(META_DATA_CONNECTION_KEY, this.endpointHandle);
                                _ = sourceHelper.ForwardAsync(message);
                            }

                            if (ms.Position < ms.Length)
                            {
                                OptLog.ERROR()?.Build($"Failed deserializing message to type {deserializationType.Name}, skipping {ms.Length - ms.Position} bytes");
                                continue;
                            }
                        }
                        else
                        {
                            message = ms.ToArray();
                            if (useConnectionMetaDataForMessages) message.SetMetaData(META_DATA_CONNECTION_KEY, this.endpointHandle);
                            _ = sourceHelper.ForwardAsync(message);
                        }

                        ms.Position = 0;
                        ms.SetLength(0);
                    }
                    catch (Exception ex)
                    {
                        OptLog.ERROR()?.Build("Failed to deserialize or forwarding message", ex);
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
                OptLog.WARNING()?.Build("Tried to send message via closed WebSocketEndpoint");
                return;
            }
            if (webSocket.State > WebSocketState.Open)
            {
                Dispose();
                return;
            }            

            using (await writeLock.LockAsync())
            {                
                serializer.Serialize(writeBuffer, message);
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
                OptLog.DEBUG()?.Build("Sending close request on WebSocket");
                var closeTask = webSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "The endpoint was closed", cts.Token);
                if (!closeTask.Wait(20.Milliseconds()))
                {
                    OptLog.WARNING()?.Build("Close request was not finished within 20 ms, so WebSocket will be disposed anyway");
                }
            }
            closeEvent.Set();
            cts.Cancel();
            cts.Dispose();
            webSocket.Dispose();
            writeBuffer.Dispose();
        }

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}
