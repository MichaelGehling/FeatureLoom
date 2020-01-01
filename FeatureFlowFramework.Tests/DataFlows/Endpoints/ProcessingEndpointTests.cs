using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class ProcessingEndpointTests
    {
        [Fact]
        public void CanProcessMessage()
        {
            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(msg => processed = msg);
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        volatile object locker = new object();
        [Fact]
        public void CanLockObjectBeforeProcessing()
        {            
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
