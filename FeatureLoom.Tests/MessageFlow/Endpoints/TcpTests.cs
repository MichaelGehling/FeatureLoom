using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using Xunit;
using FeatureLoom.Helpers;
using System.Threading;

namespace FeatureLoom.TCP
{
    public class TcpTests
    {
        public static volatile int testPortCounter = 5_001;

        [Fact]
        public void CanTransferByteArray2()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int testPort = Interlocked.Increment(ref TcpTests.testPortCounter);
            var server = new TcpServerEndpoint(new TcpServerEndpoint.Settings()
            {
                port = testPort
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Settings()
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

            Assert.True(client.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(server.ConnectionWaitHandle.Wait(2.Seconds()));

            var testData1 = new byte[] { 42, 43, 99 };
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceiveAsync(2.Seconds()).WaitFor(out byte[] receivedData1));
            Assert.Equal(testData1, receivedData1);

            var testData2 = new byte[] { 23, 11, 0 };
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceiveAsync(2.Seconds()).WaitFor(out byte[] receivedData2));
            Assert.Equal(testData2, receivedData2);

            client.Dispose();
            server.Dispose();
        }

        [Fact]
        public void CanTransferString2()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int testPort = Interlocked.Increment(ref TcpTests.testPortCounter); var server = new TcpServerEndpoint(new TcpServerEndpoint.Settings()
            {
                port = testPort
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Settings()
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

            Assert.True(client.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(server.ConnectionWaitHandle.Wait(2.Seconds()));

            var testData1 = "Test Data 1";
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceiveAsync(2.Seconds()).WaitFor(out string receivedData1));
            Assert.Equal(testData1, receivedData1);

            var testData2 = "{ testData: 2 }";
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceiveAsync(2.Seconds()).WaitFor(out string receivedData2));
            Assert.Equal(testData2, receivedData2);

            client.Dispose();
            server.Dispose();
        }

        [Fact]
        public void CanTransferByteArray()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int testPort = Interlocked.Increment(ref TcpTests.testPortCounter);
            var server = new TcpServerEndpoint(new TcpServerEndpoint.Settings()
            {
                port = testPort
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Settings()
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

            Assert.True(client.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(server.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(client.IsConnected);
            Assert.Equal(1, server.CountConnectedClients);

            var testData1 = new byte[] { 42, 43, 99 };
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceiveAsync(1.Seconds()).WaitFor(out byte[] receivedData1));
            Assert.Equal(testData1, receivedData1);

            var testData2 = new byte[] { 23, 11, 0 };
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceiveAsync(1.Seconds()).WaitFor(out byte[] receivedData2));
            Assert.Equal(testData2, receivedData2);

            client.Stop();
        }

        [Fact]
        public void CanTransferString()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int testPort = Interlocked.Increment(ref TcpTests.testPortCounter);
            var server = new TcpServerEndpoint(new TcpServerEndpoint.Settings()
            {
                port = testPort
            });
            var client = new TcpClientEndpoint(new TcpClientEndpoint.Settings()
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

            Assert.True(client.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(server.ConnectionWaitHandle.Wait(2.Seconds()));
            Assert.True(client.IsConnected);
            Assert.Equal(1, server.CountConnectedClients);

            var testData1 = "Test Data 1";
            clientSender.Send(testData1);
            Assert.True(serverReceiver.TryReceiveAsync(1.Seconds()).WaitFor(out string receivedData1));
            Assert.Equal(testData1, receivedData1);

            var testData2 = "{ testData: 2 }";
            serverSender.Send(testData2);
            Assert.True(clientReceiver.TryReceiveAsync(1.Seconds()).WaitFor(out string receivedData2));
            Assert.Equal(testData2, receivedData2);

            client.Stop();
        }
    }
}