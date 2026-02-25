using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class LatestMessageReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var receiver = new LatestMessageReceiver<T>();
            sender.ConnectTo(receiver);

            sender.Send(message);

            Assert.True(receiver.TryReceive(out T received));
            Assert.Equal(message, received);
            Assert.True(receiver.IsEmpty);
            Assert.False(receiver.HasMessage);
        }

        [Fact]
        public void OnlyLatestMessageIsRetained()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            sender.Send(1);
            sender.Send(2);
            sender.Send(3);

            Assert.True(receiver.TryReceive(out int received));
            Assert.Equal(3, received);
            Assert.False(receiver.TryReceive(out _));
            Assert.True(receiver.IsEmpty);
        }

        [Fact]
        public void PeekDoesNotConsume()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            sender.Send(123);

            Assert.True(receiver.TryPeek(out int peeked));
            Assert.Equal(123, peeked);

            Assert.True(receiver.HasMessage);
            Assert.True(receiver.TryReceive(out int received));
            Assert.Equal(123, received);
            Assert.True(receiver.IsEmpty);
        }

        [Fact]
        public void ReceiveManyReturnsSingleLatest()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            sender.Send(10);
            sender.Send(20);

            var seg = receiver.ReceiveMany(10);
            Assert.True(seg.Array != null);
            Assert.Equal(1, seg.Count);
            Assert.Equal(20, seg[0]);

            Assert.True(receiver.IsEmpty);
            Assert.False(receiver.TryReceive(out _));
        }

        [Fact]
        public void SignalsAvailabilityViaWaitHandle()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            var waitHandle = receiver.WaitHandle;
            Assert.True(receiver.IsEmpty);
            Assert.False(waitHandle.WaitingTask.IsCompleted);

            sender.Send(1);
            Assert.True(waitHandle.WaitingTask.IsCompleted);

            // After receiving, event should be reset
            Assert.True(receiver.TryReceive(out _));
            Assert.True(receiver.IsEmpty);

            var newTask = receiver.WaitingTask; // new WaitingTask reference after reset
            Assert.False(newTask.IsCompleted);
        }

        [Fact]
        public void ClearResetsStateAndSignal()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new LatestMessageReceiver<string>();
            receiver.Post("abc");
            Assert.True(receiver.HasMessage);
            Assert.True(receiver.WaitingTask.IsCompleted);

            receiver.Clear();

            Assert.True(receiver.IsEmpty);
            Assert.False(receiver.WaitingTask.IsCompleted);
            Assert.False(receiver.TryReceive(out _));
        }

        [Fact]
        public async Task PostAsyncCompletesAndSignals()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new LatestMessageReceiver<int>();

            var postTask = receiver.PostAsync(77);
            Assert.True(postTask.IsCompleted);

            // Give a tiny timeslice for the signal to propagate
            await Task.Yield();
            Assert.True(receiver.WaitingTask.IsCompleted);
            Assert.True(receiver.HasMessage);

            Assert.True(receiver.TryReceive(out int value));
            Assert.Equal(77, value);
        }

        [Fact]
        public void LatestMessageOrDefaultIsConsistent()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new LatestMessageReceiver<int>();
            Assert.Equal(default, receiver.LatestMessageOrDefault);

            receiver.Post(5);
            Assert.Equal(5, receiver.LatestMessageOrDefault);

            Assert.True(receiver.TryReceive(out _));
            Assert.Equal(default, receiver.LatestMessageOrDefault);
        }

        [Fact]
        public void TryConvertToWaitHandleWorks()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new LatestMessageReceiver<int>();
            Assert.True(receiver.TryConvertToWaitHandle(out WaitHandle handle));

            var task = Task.Run(() => handle.WaitOne(1000));
            Assert.False(task.IsCompleted);

            receiver.Post(1);

            Assert.True(task.Wait(1000));
        }
    }
}