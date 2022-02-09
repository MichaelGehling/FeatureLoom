using FeatureLoom.Diagnostics;
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
            TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(msg => processed = msg);
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        private readonly FeatureLock locker = new FeatureLock();

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
                using(locker.Lock())
                { 
                    Assert.True(processingTask.IsCompleted);
                    Assert.True(processed);                                    
                }                
            });
            Task.WhenAll(processingTask, assertionTask);
        }
    }
}