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
            maxCollectionTime: TimeSpan.FromMilliseconds(120),
            tolerance: TimeSpan.FromMilliseconds(5),
            sendSingleMessagesAsArray: false);

        var recv = new QueueReceiver<object>();
        batched.ConnectTo(recv);

        source.Send(7);

        // Allow time-based flush
        await Task.Delay(200);

        Assert.True(recv.TryReceive(out var first));
        // With sendSingleMessagesAsArray=false and a 1-item batch, we expect a single T (int), not int[]
        var single = Assert.IsType<int>(first);
        Assert.Equal(7, single);
        Assert.True(recv.IsEmpty);
    }
}