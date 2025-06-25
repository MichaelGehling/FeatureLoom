using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageConverterTests
    {
        [Fact]
        public void CanConvertMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int>(str => int.Parse(str));
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(converter).ConnectTo(sink);
            sender.Send("42");
            Assert.True(sink.HasMessage);
            Assert.Equal(42, sink.LatestMessageOrDefault);
        }
    }
}