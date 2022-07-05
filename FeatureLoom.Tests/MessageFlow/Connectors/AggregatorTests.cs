using FeatureLoom.Diagnostics;
using System;
using Xunit;
using FeatureLoom.Time;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.MessageFlow
{
    public class AggregatorTests
    {
        private class FullNameAggregationData : Aggregator<(string key, string val), string>.IAggregationData
        {
            public string firstName;
            public string lastName;
            bool sendVariants = false;

            int variantNumber = 0;

            public FullNameAggregationData(bool sendVariants)
            {
                this.sendVariants = sendVariants;
            }

            public bool ForwardByRef => false;

            public bool AddMessage((string key, string val) message)
            {
                if (message.key == "firstName") firstName = message.val;
                else if (message.key == "lastName") lastName = message.val;
                else return false;

                return true;
            }

            public bool TryGetAggregatedMessage(bool timeoutCall, out string aggregatedMessage)
            {
                if (timeoutCall && lastName != null)
                {
                    aggregatedMessage = "Mr. or Mrs. " + lastName;
                    firstName = null;
                    lastName = null;
                    return true;
                }

                aggregatedMessage = null;
                if (firstName != null && lastName != null)
                {
                    if (!sendVariants)
                    {
                        aggregatedMessage = firstName + " " + lastName;
                        firstName = null;
                        lastName = null;
                        return true;
                    }
                    else
                    {
                        if (variantNumber == 0)
                        {
                            aggregatedMessage = firstName + " " + lastName;
                            variantNumber++;
                            return true;
                        }
                        else 
                        {
                            aggregatedMessage = lastName + ", " + firstName;
                            variantNumber = 0;
                            firstName = null;
                            lastName = null;
                            return true;
                        }
                    }
                }
                else return false;
            }

            public bool TryGetTimeout(out TimeSpan timeout)
            {
                timeout = 100.Milliseconds();
                return !lastName.EmptyOrNull();
            }
        }
        
        [Fact]
        public void CanSendAggregationMessageAfterTimeout()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), string>(new FullNameAggregationData(false));
            var sink = new LatestMessageReceiver<string>();
            sender.ConnectTo(aggregator).ConnectTo(sink);

            sender.Send(("lastName", "Doe"));            
            Assert.False(sink.HasMessage);            
            sink.WaitHandle.Wait(1.Seconds());
            Assert.Equal("Mr. or Mrs. Doe", sink.LatestMessageOrDefault);

            sender.Send(("lastName", "Doe"));
            sender.Send(("firstName", "Jane"));
            Assert.True(sink.HasMessage);
            Assert.Equal("Jane Doe", sink.LatestMessageOrDefault);

            sink.Clear();
            Assert.False(sink.WaitHandle.Wait(200.Milliseconds()));
            Assert.Null(sink.LatestMessageOrDefault);
        }
        

        [Fact]
        public void CanAggregateComplementMessagesToASingleMessage()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), string>(new FullNameAggregationData(false));
            var sink = new LatestMessageReceiver<string>();            
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
        public void ForwardsUnusedMessagesToAlternativeOutput()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), string>(new FullNameAggregationData(false));
            var sink = new LatestMessageReceiver<(string key, string val)>();
            sender.ConnectTo(aggregator);
            aggregator.Else.ConnectTo(sink);

            sender.Send(("bla1", "bla2"));
            Assert.True(sink.HasMessage);
            Assert.Equal("bla1", sink.LatestMessageOrDefault.key);
            Assert.Equal("bla2", sink.LatestMessageOrDefault.val);
        }

        [Fact]
        public void CanAggregateComplementMessagesToAMultipleMessage()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), string>(new FullNameAggregationData(true));
            var sink = new QueueReceiver<string>();
            sender.ConnectTo(aggregator).ConnectTo(sink);

            sender.Send(("bla", "bla"));
            Assert.Equal(0, sink.Count);
            sender.Send(("firstName", "Jim"));
            Assert.Equal(0, sink.Count);
            sender.Send(("firstName", "John"));
            Assert.Equal(0, sink.Count);
            sender.Send(("lastName", "Doe"));
            Assert.Equal(2, sink.Count);
            var results = sink.ReceiveAll();
            Assert.Equal("John Doe", results[0]);
            Assert.Equal("Doe, John", results[1]);
        }

    }
}