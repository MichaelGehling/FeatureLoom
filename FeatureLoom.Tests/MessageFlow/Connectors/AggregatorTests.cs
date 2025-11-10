using FeatureLoom.Diagnostics;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class AggregatorTests
    {
        [Fact]
        public void CanSendAggregationMessageAfterTimeout_Inactivity()
        {
            using var testContext = TestHelper.PrepareTestContext();

            // State kept by the aggregator
            string firstName = null;
            string lastName = null;

            var sender = new Sender();
            var sink = new LatestMessageReceiver<string>();

            // Inactivity-based timeout (resets on each message)
            var aggregator = new Aggregator<(string key, string val)>(
                onMessage: (msg, s) =>
                {
                    if (msg.key == "firstName") firstName = msg.val;
                    else if (msg.key == "lastName") lastName = msg.val;

                    if (firstName != null && lastName != null)
                    {
                        s.Send($"{firstName} {lastName}");
                        firstName = null;
                        lastName = null;
                    }
                },
                onTimeout: s =>
                {
                    if (lastName != null)
                    {
                        s.Send($"Mr. or Mrs. {lastName}");
                        firstName = null;
                        lastName = null;
                    }
                },
                timeout: 100.Milliseconds(), resetTimeoutOnMessage: true);

            sender.ConnectTo(aggregator).ConnectTo(sink);

            // Only lastName arrives; timeout should emit courtesy title
            sender.Send(("lastName", "Doe"));
            Assert.False(sink.HasMessage);
            Assert.True(sink.WaitHandle.Wait(1.Seconds()));
            Assert.Equal("Mr. or Mrs. Doe", sink.LatestMessageOrDefault);

            // Next, both arrive; should emit full name immediately
            sender.Send(("lastName", "Doe"));
            sender.Send(("firstName", "Jane"));
            Assert.True(sink.HasMessage);
            Assert.Equal("Jane Doe", sink.LatestMessageOrDefault);

            // After clearing, no additional messages should appear without new input
            sink.Clear();
            Assert.False(sink.WaitHandle.Wait(200.Milliseconds()));
            Assert.Null(sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanAggregateComplementMessagesToASingleMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string firstName = null;
            string lastName = null;

            var sender = new Sender();
            var sink = new LatestMessageReceiver<string>();

            var aggregator = new Aggregator<(string key, string val)>(
                onMessage: (msg, s) =>
                {
                    if (msg.key == "firstName") firstName = msg.val;
                    else if (msg.key == "lastName") lastName = msg.val;

                    if (firstName != null && lastName != null)
                    {
                        s.Send($"{firstName} {lastName}");
                        firstName = null;
                        lastName = null;
                    }
                });

            sender.ConnectTo(aggregator).ConnectTo(sink);

            sender.Send(("bla", "bla"));
            Assert.False(sink.HasMessage);
            sender.Send(("firstName", "Jim"));
            Assert.False(sink.HasMessage);
            sender.Send(("firstName", "John"));
            Assert.False(sink.HasMessage);
            sender.Send(("lastName", "Doe"));
            Assert.True(sink.HasMessage);
            Assert.Equal("John Doe", sink.LatestMessageOrDefault);

            sink.Clear();

            sender.Send(("firstName", "Jane"));
            Assert.False(sink.HasMessage);
            sender.Send(("lastName", "Doe"));
            Assert.True(sink.HasMessage);
            Assert.Equal("Jane Doe", sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanAggregateComplementMessagesToMultipleMessages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string firstName = null;
            string lastName = null;

            var sender = new Sender();
            var sink = new QueueReceiver<string>();

            var aggregator = new Aggregator<(string key, string val)>(
                onMessage: (msg, s) =>
                {
                    if (msg.key == "firstName") firstName = msg.val;
                    else if (msg.key == "lastName") lastName = msg.val;

                    if (firstName != null && lastName != null)
                    {
                        s.Send($"{firstName} {lastName}");
                        s.Send($"{lastName}, {firstName}");
                        firstName = null;
                        lastName = null;
                    }
                });

            sender.ConnectTo(aggregator).ConnectTo(sink);

            sender.Send(("bla", "bla"));
            Assert.Equal(0, sink.Count);
            sender.Send(("firstName", "Jim"));
            Assert.Equal(0, sink.Count);
            sender.Send(("firstName", "John"));
            Assert.Equal(0, sink.Count);
            sender.Send(("lastName", "Doe"));

            // Expect two variants
            Assert.Equal(2, sink.Count);
            var results = sink.ReceiveAll().ToArray();
            Assert.Equal("John Doe", results[0]);
            Assert.Equal("Doe, John", results[1]);
        }

        [Fact]
        public void TimeoutCanBeFixedPeriod_WhenNotResetOnMessages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int ticks = 0;
            var sink = new QueueReceiver<string>();

            var aggregator = new Aggregator<string>(
                onMessage: (msg, s) =>
                {
                    // messages do not affect timer when resetTimeoutOnMessage == false
                    s.Send($"seen:{msg}");
                },
                onTimeout: s =>
                {
                    ticks++;
                    s.Send("tick");
                },
                timeout: 100.Milliseconds(),
                resetTimeoutOnMessage: false);

            aggregator.ConnectTo(sink);

            // Start timer with a message
            aggregator.Post("start");

            // Send more messages, but timer should NOT reset
            aggregator.Post("a");
            aggregator.Post("b");

            // Allow time for ~2 ticks
            AppTime.Wait(250.Milliseconds());

            // We should have at least 2 ticks; assert tick messages are present
            var all = sink.ReceiveAll().ToArray();
            Assert.True(ticks >= 2, $"Expected at least 2 ticks, got {ticks}");
            Assert.Contains("tick", all);
        }

        [Fact]
        public async Task AsyncConstructor_HandlerIsAwaitedByPostAsync()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool entered = false;
            bool completed = false;
            var sink = new LatestMessageReceiver<int>();

            var aggregator = new Aggregator<int>(
                onMessageAsync: async (msg, s) =>
                {
                    entered = true;
                    await Task.Delay(30);
                    s.Send(msg + 1);
                    completed = true;
                });

            aggregator.ConnectTo(sink);

            var task = aggregator.PostAsync(41);
            Assert.True(entered, "Handler should be entered immediately.");
            await task;
            Assert.True(completed, "Handler should be completed when PostAsync finishes.");
            Assert.True(sink.HasMessage);
            Assert.Equal(42, sink.LatestMessageOrDefault);
        }

    }
}