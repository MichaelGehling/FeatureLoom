using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class CurrentContextForwarderTests
{
    [Fact]
    public async Task Executes_in_construction_context()
    {
        string message = "TestMessage";
        Sender sender = new Sender();
        AsyncManualResetEvent connected = new AsyncManualResetEvent(false);
        AsyncManualResetEvent processed = new AsyncManualResetEvent(false);
        SingleThreadSynchronizationContext testCtx = new SingleThreadSynchronizationContext();
        testCtx.Post(_ =>
        {
            var forwarder = new CurrentContextForwarder<string>();
            sender.ConnectTo(forwarder).ProcessMessage<string>(msg =>
            {
                Assert.Equal(Thread.CurrentThread, testCtx.Thread);
                Assert.Equal(message, msg);
                processed.Set();
            });
            connected.Set();
        }, null);

        await connected.WaitAsync(100.Milliseconds());
        Assert.Equal(1, sender.CountConnectedSinks);

        sender.Send(message);
        bool success = await processed.WaitAsync(100.Milliseconds());
        Assert.True(success);
    }

}