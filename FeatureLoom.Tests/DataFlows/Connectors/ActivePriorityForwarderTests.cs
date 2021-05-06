using FeatureLoom.Diagnostics;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class ActivePriorityForwarderTest
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message) where T : IComparable
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new ActivePriorityForwarder<T>(Comparer<T>.Default);
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            sink.WaitHandle.Wait(2.Seconds());
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Theory(Skip = "Unstable when run with other tests.")]
        [InlineData(1, 10000, 1, 3, 30, 1, 90)]
        [InlineData(3, 10000, 1, 3, 30, 3, 30)]
        [InlineData(3, 10000, 3, 3, 30, 1, 90)]
        [InlineData(10, 10000, 1, 3, 30, 3, 30)]
        [InlineData(2, 10000, 1, 4, 30, 2, 90)]
        [InlineData(3, 0, 1, 3, 30, 0, 30)]
        public void CanUseMultipleThreads(int threadLimit, int maxIdleMilliseconds, int spawnThresholdFactor,
                                                                 int numMessages, int messageDelay,
                                                                 int expectedThreads, int expectedRuntime)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new ActivePriorityForwarder<int>(Comparer<int>.Default, threadLimit, maxIdleMilliseconds, spawnThresholdFactor);
            var delayer = new DelayingForwarder(messageDelay.Milliseconds());
            var sink = new CountingForwarder();
            sender.ConnectTo(forwarder).ConnectTo(delayer).ConnectTo(sink);

            for (int i = 0; i < numMessages; i++)
            {
                sender.Send(i);
            }
            var waitingTask = sink.WaitForAsync(numMessages);
            waitingTask.Wait(expectedRuntime.Milliseconds() * 5);

            Assert.Equal(numMessages, sink.Counter);
            Assert.Equal(expectedThreads, forwarder.CountThreads);
        }
    }
}