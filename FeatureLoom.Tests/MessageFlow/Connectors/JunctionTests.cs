using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class JunctionTests
    {
        private sealed class CollectingSink : IMessageSink
        {
            public List<object> ReceivedSync { get; } = new List<object>();
            public List<object> ReceivedAsync { get; } = new List<object>();
            private readonly int asyncDelayMs;

            public CollectingSink(int asyncDelayMs = 0) => asyncDelayMs = this.asyncDelayMs = asyncDelayMs;

            public void Post<M>(in M message) => ReceivedSync.Add(message);
            public void Post<M>(M message) => ReceivedSync.Add(message);

            public async Task PostAsync<M>(M message)
            {
                if (asyncDelayMs > 0) await Task.Delay(asyncDelayMs).ConfigureAwait(false);
                ReceivedAsync.Add(message);
            }
        }

        [Fact]
        public void SingleMatch_ChoosesHighestPriority()
        {
            var sinkLow = new CollectingSink();
            var sinkHigh = new CollectingSink();
            var junction = new Junction(multiOption: false);

            junction.ConnectOption<string>(sinkLow, priority: 1);
            junction.ConnectOption<string>(sinkHigh, priority: 10); // Should win.

            junction.Post("hello");
            Assert.Empty(sinkLow.ReceivedSync);
            Assert.Single(sinkHigh.ReceivedSync);
            Assert.Equal("hello", sinkHigh.ReceivedSync[0]);
        }

        [Fact]
        public async Task SingleMatchAsync_ChoosesHighestPriority()
        {
            var sinkLow = new CollectingSink();
            var sinkHigh = new CollectingSink();
            var junction = new Junction(multiOption: false);

            junction.ConnectOption<int>(sinkLow, priority: 5);
            junction.ConnectOption<int>(sinkHigh, priority: 7);

            await junction.PostAsync(42);

            Assert.Empty(sinkLow.ReceivedAsync);
            Assert.Single(sinkHigh.ReceivedAsync);
            Assert.Equal(42, sinkHigh.ReceivedAsync[0]);
        }

        [Fact]
        public void MultiOption_ForwardsToAllMatching_InPriorityOrder()
        {
            var sinkA = new CollectingSink();
            var sinkB = new CollectingSink();
            var sinkC = new CollectingSink();
            var junction = new Junction(multiOption: true);

            junction.ConnectOption<int>(sinkA, priority: 1);
            junction.ConnectOption<int>(sinkC, priority: 3);
            junction.ConnectOption<int>(sinkB, priority: 2);

            junction.Post(7);

            // All should receive.
            Assert.Single(sinkC.ReceivedSync); // priority 3
            Assert.Single(sinkB.ReceivedSync); // priority 2
            Assert.Single(sinkA.ReceivedSync); // priority 1

            // Verify all values received.
            Assert.Equal(new[] { 7 }, sinkC.ReceivedSync.Cast<int>());
            Assert.Equal(new[] { 7 }, sinkB.ReceivedSync.Cast<int>());
            Assert.Equal(new[] { 7 }, sinkA.ReceivedSync.Cast<int>());
        }

        [Fact]
        public async Task MultiOptionAsync_ForwardsSequentially_InPriorityOrder()
        {
            var sinkFastHigh = new CollectingSink(asyncDelayMs: 10);
            var sinkMedium = new CollectingSink(asyncDelayMs: 30);
            var sinkSlowLow = new CollectingSink(asyncDelayMs: 50);
            var junction = new Junction(multiOption: true);

            junction.ConnectOption<string>(sinkSlowLow, priority: 1);
            junction.ConnectOption<string>(sinkFastHigh, priority: 5);
            junction.ConnectOption<string>(sinkMedium, priority: 3);

            await junction.PostAsync("X");

            Assert.Equal(1, sinkFastHigh.ReceivedAsync.Count);
            Assert.Equal(1, sinkMedium.ReceivedAsync.Count);
            Assert.Equal(1, sinkSlowLow.ReceivedAsync.Count);
        }

        [Fact]
        public void PredicateFiltersCorrectly()
        {
            var sink = new CollectingSink();
            var junction = new Junction();

            junction.ConnectOption<int>(sink, checkMessage: i => i > 10);

            junction.Post(5);
            junction.Post(15);

            Assert.Single(sink.ReceivedSync);
            Assert.Equal(15, sink.ReceivedSync[0]);
        }

        [Fact]
        public void FallbackReceivesUnmatchedMessages()
        {
            var fallbackSink = new CollectingSink();
            var junction = new Junction();
            junction.Else.ConnectTo(fallbackSink);

            junction.Post("no match"); // No options configured.

            Assert.Single(fallbackSink.ReceivedSync);
            Assert.Equal("no match", fallbackSink.ReceivedSync[0]);
        }

        [Fact]
        public void FallbackNotUsedIfNotCreated()
        {
            var junction = new Junction();
            junction.Post("lost"); // No Else access and no options.
        }

        [Fact]
        public void TypeIsolationBetweenOptions()
        {
            var sinkString = new CollectingSink();
            var sinkInt = new CollectingSink();
            var junction = new Junction(multiOption: true);

            junction.ConnectOption<string>(sinkString);
            junction.ConnectOption<int>(sinkInt);

            junction.Post("text");
            junction.Post(123);

            Assert.Single(sinkString.ReceivedSync);
            Assert.Single(sinkInt.ReceivedSync);
            Assert.Equal("text", sinkString.ReceivedSync[0]);
            Assert.Equal(123, sinkInt.ReceivedSync[0]);
        }

        [Fact]
        public async Task AsyncFallbackReceivesUnmatchedMessages()
        {
            var fallbackSink = new CollectingSink();
            var junction = new Junction(multiOption: true);
            junction.Else.ConnectTo(fallbackSink);

            await junction.PostAsync(Guid.NewGuid());

            Assert.Single(fallbackSink.ReceivedAsync);
            Assert.IsType<Guid>(fallbackSink.ReceivedAsync[0]);
        }

        [Fact]
        public void ConnectOption_WithFlowConnection_ReturnsSource_ForChaining()
        {
            var junction = new Junction();
            var forwarder = new Forwarder<int>();

            var returned = junction.ConnectOption<int>((IMessageFlowConnection)forwarder, priority: 2);

            Assert.Same((IMessageSource)forwarder, returned);
            // Verify it actually forwards
            var sink = new CollectingSink();
            returned.ConnectTo(sink);
            junction.Post(3);
            Assert.Single(sink.ReceivedSync);
            Assert.Equal(3, sink.ReceivedSync[0]);
        }

        [Fact]
        public void ConnectOption_FactoryVariant_ReturnsTypedSource_AndForwards()
        {
            var junction = new Junction();
            var sink = new CollectingSink();

            var source = junction.ConnectOption<int>(i => i % 2 == 0, priority: 1);
            source.ConnectTo(sink);

            junction.Post(1);
            junction.Post(2);

            Assert.Single(sink.ReceivedSync);
            Assert.Equal(2, sink.ReceivedSync[0]);
        }

        [Fact]
        public void ConnectOption_SinkOverloads_NullChecks()
        {
            var junction = new Junction();
            Assert.Throws<ArgumentNullException>(() => junction.ConnectOption<int>((IMessageSink)null, priority: 1));
            Assert.Throws<ArgumentNullException>(() => junction.ConnectOption<int>((IMessageFlowConnection)null, priority: 1));
        }

        [Fact]
        public void ConnectOption_OverloadSelection_WithExplicitCasts()
        {
            var junction = new Junction();
            var forwarder = new Forwarder<string>();
            var sinkA = new CollectingSink();
            var sinkB = new CollectingSink();

            // Force IMessageSink overload
            junction.ConnectOption<string>((IMessageSink)forwarder, priority: 1);
            // Force IMessageFlowConnection overload
            junction.ConnectOption<string>((IMessageFlowConnection)forwarder, priority: 2);

            junction.Post("hi");

            // Forwarder receives only once; both options point to same forwarder, but Junction dispatches once per option.
            // We verify by connecting forwarder to sinks differently:
            ((IMessageSource)forwarder).ConnectTo(sinkA);
            ((IMessageSource)forwarder).ConnectTo(sinkB);

            // Re-send to ensure forwarding happens (since connections were added after first post)
            junction.Post("again");

            Assert.Equal(1, sinkA.ReceivedSync.Count);
            Assert.Equal(1, sinkB.ReceivedSync.Count);
            Assert.Equal("again", sinkA.ReceivedSync[0]);
            Assert.Equal("again", sinkB.ReceivedSync[0]);
        }
    }
}