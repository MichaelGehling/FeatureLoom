﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using FeatureLoom.Workflows;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FeatureLoom.Extensions;
using System.Threading;
using FeatureLoom.Serialization;

namespace FeatureLoom.TCP
{

    public class TcpConnection : Workflow<TcpConnection.StateMachine>
    {
        public class StateMachine : StateMachine<TcpConnection>
        {
            protected override void Init()
            {
                var receivingAndDecoding = State("ReceivingAndDecoding");
                var closing = State("Closing");

                receivingAndDecoding.Build()
                    .Step("Await more data from stream if all data was processed or last message was incomplete")
                        .If(c => c.decodingResult == DecodingResult.Incomplete || c.bufferReadPosition == c.bufferFillState)
                            .Do(async c =>
                            {
                                var bytesRead = await c.stream.ReadAsync(c.buffer, c.bufferFillState, c.bufferSize - c.bufferFillState, c.CancellationToken).ConfigureAwait(false);
                                // Client may not recognize diconnection, so when stream is exhausted, connection is closed
                                if (bytesRead == 0) c.client.Close();
                                else c.bufferFillState += bytesRead;
                            })
                        .CatchAndGoto(closing)
                    .Step("On disconnection goto closing")
                        .If(c => !c.client.Connected)
                            .Goto(closing)
                    .Step("Try to decode message from received data")
                        .Do(c =>
                        {
                            var oldBufferReadPosition = c.bufferReadPosition;
                            c.decodingResult = c.decoder.Decode(c.buffer, c.bufferFillState, ref c.bufferReadPosition, out c.decodedMessage, ref c.decoderIntermediateData);
                        })
                        .CatchAndDo((c, e) =>
                        {
                            c.ResetBuffer(false);
                            OptLog.ERROR()?.Build($"Decoding data from TCP connection failed! Buffer was reset and all data omitted.", e);
                        })
                    .Step("If decoding was completed put message in routing wrapper and send it, else reset buffer, but preserve the unprocessed bytes if chance exists to fit more data into the buffer.")
                        .If(c => c.decodingResult == DecodingResult.Complete)
                            .Do(c => c.receivedMessageSender.Send(c.addRoutingWrapper ? new ConnectionRoutingWrapper(c.GetHandle().id, c.decodedMessage) : c.decodedMessage))
                        .Else()
                            .Do(c => c.ResetBuffer(!(c.bufferReadPosition == 0 && c.bufferFillState == c.bufferSize)))
                    .Step("Start over again")
                        .Loop();

                closing.Build()
                    .Step("Stop connection")
                        .Do(c =>
                        {
                            OptLog.INFO()?.Build($"Connection was closed!");
                            c.Stop(false);
                        })
                    .Step("Finish workflow")
                        .Finish();
            }
        }

        private readonly TcpClient client;
        private Stream stream;
        private bool addRoutingWrapper;
        private MessageConverter<object, object> routingFilter;
        private readonly ProcessingEndpoint<byte[]> tcpWriter;

        private Sender receivedMessageSender = new Sender();
        private ITcpMessageDecoder decoder;

        private byte[] buffer;
        private int bufferFillState = 0;
        private int bufferSize;
        private int bufferReadPosition = 0;
        private object decoderIntermediateData = null;
        private object decodedMessage = null;
        private DecodingResult decodingResult = DecodingResult.Invalid;
        private AsyncManualResetEvent disconnectionEvent = new AsyncManualResetEvent(false);

        public TcpConnection(TcpClient client, ITcpMessageDecoder decoder, int bufferSize, bool addRoutingWrapper, IStreamUpgrader streamUpgrader = null)
        {
            this.client = client;
            this.decoder = decoder;
            this.addRoutingWrapper = addRoutingWrapper;

            try
            {
                stream = streamUpgrader?.Upgrade(client.GetStream()) ?? client.GetStream();
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Could not get stream from tcpClient or stream upgrade failed.", e);
            }

            this.bufferSize = bufferSize;
            this.buffer = new byte[this.bufferSize];

            routingFilter = new MessageConverter<object, object>(msg => FilterOrUnwrapMessage(msg));
            tcpWriter = new ProcessingEndpoint<byte[]>(async buffer => await WriteToTcpStream(buffer).ConfigureAwait(false));
            routingFilter.ConnectTo(tcpWriter);

            this.Run();
        }

        private object FilterOrUnwrapMessage(object msg)
        {
            if (msg is ConnectionRoutingWrapper wrapper)
            {
                if ((wrapper.inverseConnectionFiltering && wrapper.connectionId != this.GetHandle().id) ||
                    (!wrapper.inverseConnectionFiltering && wrapper.connectionId == this.GetHandle().id) ||
                    wrapper.connectionId == default)
                {
                    return wrapper.Message;
                }
                else return null;
            }
            else return msg;
        }

        private async Task<bool> WriteToTcpStream(byte[] buffer)
        {
            if (Disconnected)
            {
                OptLog.WARNING()?.Build($"Message was not set over connection because of disconnection!");
                return false;
            }

            try
            {
                await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed when writing to stream, closing connection!", e);
                this.client.Close();
                return false;
            }
        }

        private void ResetBuffer(bool preserveUnreadBytes)
        {
            if (preserveUnreadBytes)
            {
                int unreadBytes = bufferFillState - bufferReadPosition;
                for (int i = 0; i < unreadBytes; i++)
                {
                    buffer[i] = buffer[bufferReadPosition + i];
                }
                bufferFillState = unreadBytes;
                bufferReadPosition = 0;
            }
            else
            {
                bufferFillState = 0;
                bufferReadPosition = 0;
            }
        }

        public void Stop(bool stopWorkflow = true)
        {
            client.Close();
            if (stopWorkflow) this.RequestPause(true);
            disconnectionEvent.Set();
            receivedMessageSender.DisconnectAll();
        }

        public IMessageSink SendingSink => routingFilter;

        public IMessageSource ReceivingSource => receivedMessageSender;

        public bool Disconnected => !client.Connected;
        public bool Connected => client.Connected;
        public IAsyncWaitHandle DisconnectionWaitHandle => disconnectionEvent;
    }

    public interface ITcpMessageEncoder
    {
        byte[] Encode(object payload);
    }

    public interface ITcpMessageDecoder
    {
        DecodingResult Decode(byte[] buffer, int bufferFillState, ref int bufferReadPosition, out object decodedMessage, ref object intermediateData);
    }

    

    public enum DecodingResult
    {
        Invalid,
        Complete,
        Incomplete
    }
}