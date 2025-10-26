using FeatureLoom.MessageFlow;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Batch_DefaultOverload_Tests
{
    [Fact]
    public async Task BatchMessages_default_overload_emits_arrays_and_flushes_by_time_and_size()
    {
        var source = new Sender();
        var batched = source.BatchMessages<int>(maxBatchSize: 3, maxCollectionTime: TimeSpan.FromMilliseconds(120));

        var recv = new QueueReceiver<object>();
        batched.ConnectTo(recv);

        // Time-based flush: 1 item -> int[] length 1 (default sendSingleMessagesAsArray=true)
        source.Send(7);
        await Task.Delay(220);

        Assert.True(recv.TryReceive(out var first));
        var arr1 = Assert.IsType<int[]>(first);
        Assert.Equal(new[] { 7 }, arr1);

        // Size-based flush: emit immediately when size reached
        source.Send(1);
        source.Send(2);
        source.Send(3);

        SpinWait.SpinUntil(() => !recv.IsEmpty, 500);

        Assert.True(recv.TryReceive(out var second));
        var arr2 = Assert.IsType<int[]>(second);
        Assert.Equal(new[] { 1, 2, 3 }, arr2);

        Assert.True(recv.IsEmpty);
    }
}