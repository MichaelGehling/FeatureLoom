using System;
using Xunit;
using FeatureLoom.Time;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;

namespace FeatureLoom.Mappers
{
    public class ObjectMapperServiceTests
    {
        [Fact]
        public void CanConvertMessagesOfDifferentTypes()
        {
            using var testContext = TestHelper.PrepareTestContext();

            ObjectMapperService mapperService = new ObjectMapperService();
            mapperService.AddConversion((int i) => i.ToString());
            mapperService.AddConversion((string s) => s.ToLower());

            var sender = new Sender();
            var converter = mapperService.CreateMultiMappingConverter();
            var sink = new LatestMessageReceiver<string>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send(42);
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "42");

            sender.Send("HELLO");
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "hello");
        }

        [Fact]
        public void LaterAddingOrRemovingConversionIsAffective()
        {
            using var testContext = TestHelper.PrepareTestContext();

            ObjectMapperService mapperService = new ObjectMapperService();            

            var sender = new Sender();
            var converter = mapperService.CreateMultiMappingConverter();
            var sink = new LatestMessageReceiver<string>();
            sender.ConnectTo(converter).ConnectTo(sink);

            sender.Send("HELLO");
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "HELLO");

            mapperService.AddConversion((string s) => s.ToLower());

            sender.Send("HELLO");
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "hello");

            mapperService.RemoveConversion(typeof(string));

            sender.Send("HELLO");
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "HELLO");
        }

        [Fact]
        public void NotConvertedMessagesCanBeForwardedOrNot()
        {
            ObjectMapperService mapperService = new ObjectMapperService();

            var sender = new Sender();
            var forwardingConverter = mapperService.CreateMultiMappingConverter(true);
            var nonForwardingConverter = mapperService.CreateMultiMappingConverter(false);
            var forwardedSink = new LatestMessageReceiver<int>();
            var nonForwardedSink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwardingConverter).ConnectTo(forwardedSink);
            sender.ConnectTo(nonForwardingConverter).ConnectTo(nonForwardedSink);

            mapperService.AddConversion((string s) => s.ToLower());

            sender.Send(42);
            Assert.True(forwardedSink.HasMessage);
            Assert.True(forwardedSink.LatestMessageOrDefault == 42);
            Assert.False(nonForwardedSink.HasMessage);
        }

        [Fact]
        public void TypesToBeConvertedCanBeSelected()
        {
            ObjectMapperService mapperService = new ObjectMapperService();

            var sender = new Sender();
            var converter = mapperService.CreateMultiMappingConverter(true, typeof(int));
            var sink = new LatestMessageReceiver<string>();
            sender.ConnectTo(converter).ConnectTo(sink);

            mapperService.AddConversion((string s) => s.ToLower());
            mapperService.AddConversion((int i) => i.ToString());

            sender.Send(42);
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "42");

            sender.Send("HELLO");
            Assert.True(sink.HasMessage);
            Assert.True(sink.LatestMessageOrDefault == "HELLO");
        }
    }
}
