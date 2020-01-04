using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.DataFlows.TCP;
using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows.TCP
{
    public class TcpTests
    {
        public static volatile int testPortCounter = 50_001;

        [Fact]
        public void CanTransferByteArray()
        {
            int testPort = TcpTests.testPortCounter++;
            var server = new TcpServerEndpoint(new TcpServerEndpoint.Config()
            {
                port = testPort                
                
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Config()
            {
                port = testPort
            });
            var clientSender = new Sender();
            var serverSender = new Sender();
            var clientReceiver = new LatestMessageReceiver<byte[]>();
            var serverReceiver = new LatestMessageReceiver<byte[]>();
            clientSender.ConnectTo(client);
            client.ConnectTo(clientReceiver);
            serverSender.ConnectTo(server);
            server.ConnectTo(serverReceiver);

            client.ConnectionWaitHandle.Wait(1.Seconds());
            server.ConnectionWaitHandle.Wait(1.Seconds());
            Assert.True(client.IsConnectedToServer);
            Assert.Equal(1, server.CountConnectedClients);

            var testData1 = new byte[] { 42, 43, 99 };
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceive(out byte[] receivedData1, 1.Seconds()));
            Assert.Equal(testData1, receivedData1);

            var testData2 = new byte[] { 23, 11, 0 };
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceive(out byte[] receivedData2, 1.Seconds()));
            Assert.Equal(testData2, receivedData2);

            client.DisconnectFromTcpServer();
        }

        [Fact]
        public void CanTransferString()
        {
            int testPort = TcpTests.testPortCounter++;
            var server = new TcpServerEndpoint(new TcpServerEndpoint.Config()
            {
                port = testPort
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Config()
            {
                port = testPort
            });
            var clientSender = new Sender();
            var serverSender = new Sender();
            var clientReceiver = new LatestMessageReceiver<string>();
            var serverReceiver = new LatestMessageReceiver<string>();
            clientSender.ConnectTo(client);
            client.ConnectTo(clientReceiver);
            serverSender.ConnectTo(server);
            server.ConnectTo(serverReceiver);

            client.ConnectionWaitHandle.Wait(1.Seconds());
            server.ConnectionWaitHandle.Wait(1.Seconds());
            Assert.True(client.IsConnectedToServer);
            Assert.Equal(1, server.CountConnectedClients);

            var testData1 = "Test Data 1";
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceive(out string receivedData1, 1.Seconds()));
            Assert.Equal(testData1, receivedData1);

            var testData2 = "{ testData: 2 }";
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceive(out string receivedData2, 1.Seconds()));
            Assert.Equal(testData2, receivedData2);

            client.DisconnectFromTcpServer();
        }

        [Fact]
        public void ClientReconnectsAutomatically()
        {

        }
    }
}
