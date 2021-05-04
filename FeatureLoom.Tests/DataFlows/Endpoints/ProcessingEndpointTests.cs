using FeatureLoom.Helpers.Time;
using FeatureLoom.Helpers.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class ProcessingEndpointTests
    {
        [Fact]
        public void CanProcessMessage()
        {
            TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(msg => processed = msg);
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        private readonly object locker = new object();

        [Fact]
        public void CanLockObjectBeforeProcessing()
        {
            TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(async msg =>
            {
                await Task.Delay(50.Milliseconds());
                processed = msg;
            }, locker);
            sender.ConnectTo(processor);

            var processingTask = sender.SendAsync(true);
            var assertionTask = Task.Run(() =>
            {
                try
                {
                    Monitor.Enter(locker);
                    Assert.True(processingTask.IsCompleted);
                    Assert.True(processed);
                }
                finally
                {
                    Monitor.Exit(locker);
                }
            });
            Task.WhenAll(processingTask, assertionTask);
        }
    }
}