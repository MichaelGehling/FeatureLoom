﻿using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class ForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new Forwarder();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void TypedForwarderFailsWhenConnectedToWrongType()
        {
            using var testContext = TestHelper.PrepareTestContext();

            Forwarder<int> intForwarder = new Forwarder<int>();
            Forwarder<int> intForwarder2 = new Forwarder<int>();
            Forwarder<string> stringForwarder = new Forwarder<string>();
            Forwarder<object> objectForwarder = new Forwarder<object>();

            Assert.ThrowsAny<Exception>(() => intForwarder.ConnectTo(stringForwarder));

            objectForwarder.ConnectTo(stringForwarder);
            Assert.True(objectForwarder.CountConnectedSinks == 1);

            intForwarder.ConnectTo(intForwarder2);
            Assert.True(intForwarder.CountConnectedSinks == 1);

            stringForwarder.ConnectTo(objectForwarder);
            Assert.True(stringForwarder.CountConnectedSinks == 1);

            intForwarder2.ConnectTo(objectForwarder);
            Assert.True(intForwarder2.CountConnectedSinks == 1);
        }
    }
}