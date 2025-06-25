﻿using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class SplitterTests
    {
        [Fact]
        public void CanSplitMessageIntoMultiple()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string>(str => str.ToCharArray());
            var receiver = new QueueReceiver<char>();
            sender.ConnectTo(splitter).ConnectTo(receiver);

            sender.Send("HELLO");
            Assert.Equal(5, receiver.Count);
            Assert.Equal('H', receiver.TryReceive(out char msg1) ? msg1 : ' ');
            Assert.Equal('E', receiver.TryReceive(out char msg2) ? msg2 : ' ');
            Assert.Equal('L', receiver.TryReceive(out char msg3) ? msg3 : ' ');
            Assert.Equal('L', receiver.TryReceive(out char msg4) ? msg4 : ' ');
            Assert.Equal('O', receiver.TryReceive(out char msg5) ? msg5 : ' ');
        }
    }
}