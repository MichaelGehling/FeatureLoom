using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Diagnostics
{
    public class MessageCounterTests
    {

        [Fact]
        public void CountsTheMessages()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new MessageCounter();            
            sender.ConnectTo(forwarder);
            sender.Send(41);
            Assert.Equal(1, forwarder.Counter);
            sender.Send(42);
            Assert.Equal(2, forwarder.Counter);
            sender.Send(43);
            Assert.Equal(3, forwarder.Counter);
        }

        [Fact]
        public void CanWaitForANumberOfMessages()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new MessageCounter();
            sender.ConnectTo(forwarder);

            Task waitFor1 = forwarder.WaitForCountAsync(1);
            Task waitFor3 = forwarder.WaitForCountAsync(3);
            Assert.False(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(41);
            Assert.True(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(42);
            Assert.True(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(43);
            Assert.True(waitFor1.IsCompleted);
            Assert.True(waitFor3.IsCompleted);
        }
    }
}