using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class ProcessingEndpointTests
    {
        [Fact]
        public void CanProcessMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(msg => processed = msg);
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        [Fact]
        public void CanProcessMessageAsync()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(async msg =>
            {
                await Task.Yield();
                processed = msg;
            });
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }
    }
}