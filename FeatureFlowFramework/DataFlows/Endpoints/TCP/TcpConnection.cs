using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Workflows;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.TCP
{
    public class TcpConnection : Workflow<TcpConnection.StateMachine>
    {
        public class StateMachine : StateMachine<TcpConnection>
        {
            protected override void Init()
            {
                logStateChanges = true;
                logStateChanges = true;
                logExeption = true;
                logStartWaiting = true;
                logFinishWaiting = true;

                var receivingAndDecoding = State("ReceivingAndDecoding");
                var closing = State("Closing");

                receivingAndDecoding.Build()
                    .Step("On disconnection goto closing")
                        .If(c => !c.client.Connected)
                            .Goto(closing)
                    .Step("Await more data from stream if all data was processed or last message was incomplete")
                        .If(c => c.decodingResult == DecodingResult.Incomplete || c.bufferReadPosition == c.bufferFillState)
                            .Do(async c =>
                            {
                                var bytesRead = await c.stream.ReadAsync(c.buffer, c.bufferFillState, c.bufferSize - c.bufferFillState, c.CancellationToken);
                                c.bufferFillState += bytesRead;
                            })
                        .CatchAndGoto(closing)
                    .Step("Try to decode message from received data")
                        .Do(c =>
                        {
                            var oldBufferReadPosition = c.bufferReadPosition;
                            c.decodingResult = c.decoder.Decode(c.buffer, c.bufferFillState, ref c.bufferReadPosition, out c.decodedMessage, ref c.decoderIntermediateData);
                        })
                        .CatchAndDo((c, e) =>
                        {
                            c.ResetBuffer(false);
                            Log.ERROR($"Decoding data from TCP connection {c.id.ToString()} failed! Buffer was reset and all data omitted. (id={c.id})", e.ToString());
                        })
                    .Step("If decoding was completed put message in routing wrapper and send it, else reset buffer, but preserve the unprocessed bytes if chance exists to fit more data into the buffer.")
                        .If(c => c.decodingResult == DecodingResult.Complete)
                            .Do(c => c.receivedMessageSender.Send(c.addRoutingWrapper ? new ConnectionRoutingWrapper(c.id, c.decodedMessage) : c.decodedMessage))
                        .Else()
                            .Do(c => c.ResetBuffer(!(c.bufferReadPosition == 0 && c.bufferFillState == c.bufferSize)))
                    .Step("Start over again")
                        .Loop();

                closing.Build()
                    .Step("Stop connection")
                        .Do(c =>
                        {
                            Log.INFO(c, $"Connection was closed! (id={c.id})");
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
                Log.ERROR($"Could not get stream from tcpClient or stream upgrade failed. (id={id})", e.ToString());
            }

            this.bufferSize = bufferSize;
            this.buffer = new byte[this.bufferSize];

            routingFilter = new MessageConverter<object, object>(msg => FilterOrUnwrapMessage(msg));
            tcpWriter = new ProcessingEndpoint<byte[]>(async buffer => await WriteToTcpStream(buffer));
            routingFilter.ConnectTo(tcpWriter);

            this.Run();
        }

        private object FilterOrUnwrapMessage(object msg)
        {
            if (msg is ConnectionRoutingWrapper wrapper)
            {
                if ((wrapper.inverseConnectionFiltering && wrapper.connectionId != id) ||
                    (!wrapper.inverseConnectionFiltering && wrapper.connectionId == id) ||
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
                Log.WARNING(this, $"Message was not set over connection because of disconnection! (id={id})");
                return false;
            }

            try
            {
                await stream.WriteAsync(buffer, 0, buffer.Length);
                return true;
            }
            catch (Exception e)
            {
                Log.ERROR(this, $"Failed when writing to stream, closing connection! (id={id})", e.ToString());
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

        public IDataFlowSink SendingSink => routingFilter;

        public IDataFlowSource ReceivingSource => receivedMessageSender;

        public bool Disconnected => !client.Connected;
        public bool Connected => client.Connected;
        public IAsyncWaitHandle DisconnectionWaitHandle => disconnectionEvent.AsyncWaitHandle;
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

    public interface IStreamUpgrader
    {
        Stream Upgrade(Stream stream);
    }

    public class ServerSslStreamUpgrader : IStreamUpgrader
    {
        private readonly X509Certificate2 serverCertificate;

        public ServerSslStreamUpgrader(X509Certificate2 serverCertificate)
        {
            this.serverCertificate = serverCertificate;
        }

        public Stream Upgrade(Stream stream)
        {
            var sslStream = new SslStream(stream, false);
            sslStream.AuthenticateAsServer(serverCertificate, false, System.Security.Authentication.SslProtocols.None, true);
            return sslStream;
        }
    }

    public class ClientSslStreamUpgrader : IStreamUpgrader
    {
        private readonly string serverName;
        private readonly bool ignoreFailedAuthentication;

        public ClientSslStreamUpgrader(string serverName, bool ignoreFailedAuthentication)
        {
            this.serverName = serverName;
            this.ignoreFailedAuthentication = ignoreFailedAuthentication;
        }

        public Stream Upgrade(Stream stream)
        {
            SslStream sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            sslStream.AuthenticateAsClient(serverName);
            return sslStream;
        }

        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (ignoreFailedAuthentication) return true;

            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            // refuse connection
            return false;
        }
    }
}