﻿using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class BufferingForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new BufferingForwarder<object>(10);
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void BuffersForwardedDataAndSendsItOnConnection()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new BufferingForwarder<int>(10);
            var sink = new QueueReceiver<int>();
            sender.ConnectTo(forwarder);
            sender.Send(1);
            sender.Send(2);
            sender.Send(3);
            forwarder.ConnectTo(sink);
            Assert.Equal(3, sink.Count);
            sender.Send(4);
            Assert.Equal(4, sink.Count);
            Assert.Contains(1, sink.PeekAll());
            Assert.Contains(2, sink.PeekAll());
            Assert.Contains(3, sink.PeekAll());
            Assert.Contains(4, sink.PeekAll());

            var sink2 = new QueueReceiver<int>();
            forwarder.ConnectTo(sink2);
            Assert.Equal(4, sink2.Count);
        }
    }
}