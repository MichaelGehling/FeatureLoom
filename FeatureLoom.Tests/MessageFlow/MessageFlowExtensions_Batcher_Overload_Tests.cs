using FeatureLoom.MessageFlow;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Batcher_Overload_Tests
{
    [Fact]
    public async Task BatchMessages_single_item_not_array_when_configured()
    {
        var source = new Sender();
        var batched = source.BatchMessages<int>(
            maxBatchSize: 10,
            maxCollectionTime: TimeSpan.FromMilliseconds(100),
            sendSingleMessagesAsArray: false);

        var recv = new QueueReceiver<object>();
        batched.ConnectTo(recv);

        source.Send(7);

        // Allow time-based flush. Poll instead of a fixed delay to tolerate a loaded machine.
        object first = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!recv.TryReceive(out first) && DateTime.UtcNow < deadline) await Task.Delay(20);
        // The batcher's timeout schedule is only weakly referenced; keep the flow alive until here,
        // otherwise a GC triggered by parallel tests may collect it before the flush (seen on net48 Release).
        GC.KeepAlive(source);
        GC.KeepAlive(batched);

        Assert.NotNull(first);
        // With sendSingleMessagesAsArray=false and a 1-item batch, we expect a single T (int), not int[]
        var single = Assert.IsType<int>(first);
        Assert.Equal(7, single);
        Assert.True(recv.IsEmpty);
    }
}