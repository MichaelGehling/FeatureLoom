using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class SelectorTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var selector = new Selector<T>();
            var trueOption = selector.AddOption(msg => true);
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(selector);
            trueOption.ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void RoutesMessagesToMatchingOptions()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var selector = new Selector<int>(true);
            sender.ConnectTo(selector);
            var lessThan10Option = selector.AddOption(msg => msg < 10);
            var greaterThan5Option = selector.AddOption(msg => msg > 5);
            var lessThan10Counter = new MessageCounter();
            var greaterThan5Counter = new MessageCounter();
            var elseCounter = new MessageCounter();
            lessThan10Option.ConnectTo(lessThan10Counter);
            greaterThan5Option.ConnectTo(greaterThan5Counter);
            selector.Else.ConnectTo(elseCounter);

            sender.Send(7);
            Assert.Equal(1, lessThan10Counter.Counter);
            Assert.Equal(1, greaterThan5Counter.Counter);

            sender.Send(2);
            Assert.Equal(2, lessThan10Counter.Counter);
            Assert.Equal(1, greaterThan5Counter.Counter);

            sender.Send(99);
            Assert.Equal(2, lessThan10Counter.Counter);
            Assert.Equal(2, greaterThan5Counter.Counter);

            selector.MultiMatch = false;
            sender.Send(7);
            Assert.Equal(3, lessThan10Counter.Counter);
            Assert.Equal(2, greaterThan5Counter.Counter);

            sender.Send(AppTime.Now);
            Assert.Equal(3, lessThan10Counter.Counter);
            Assert.Equal(2, greaterThan5Counter.Counter);
            Assert.Equal(1, elseCounter.Counter);
        }
    }
}