using FeatureLoom.MessageFlow;
using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Process_Filter_Convert_Split_Tests
{
    [Fact]
    public void ProcessMessage_with_elseSource_routes_declined_and_mismatched()
    {
        var source = new Sender();
        var elseRecv = new QueueReceiver<object>();
        var accepted = new List<int>();

        source.ProcessMessage<int>(i =>
        {
            accepted.Add(i);
            return i % 2 == 0; // accept even
        }, out var elseSource);

        elseSource.ConnectTo(elseRecv);

        source.Send(1);       // declined -> else
        source.Send("x");     // mismatched -> else
        source.Send(2);       // accepted

        Assert.Equal(new[] { 1, 2 }, accepted);
        Assert.True(elseRecv.TryReceive(out var a));
        Assert.True(elseRecv.TryReceive(out var b));
        Assert.True(elseRecv.IsEmpty);
        Assert.Contains(1, new[] { a, b });
        Assert.Contains("x", new[] { a, b });
    }

    [Fact]
    public void FilterMessage_passes_only_matching_and_drops_others_without_else()
    {
        var source = new Sender();
        var filtered = source.FilterMessage<int>(i => i > 0);

        var passRecv = new QueueReceiver<int>();
        filtered.ConnectTo(passRecv);

        source.Send(1);      // pass
        source.Send(-1);     // dropped (no else connected)
        source.Send("x");    // dropped (no else connected)

        Assert.True(passRecv.TryReceive(out var p));
        Assert.Equal(1, p);
        Assert.True(passRecv.IsEmpty);
    }

    [Fact]
    public void ConvertMessage_converts_matched_and_forwards_others()
    {
        var source = new Sender();
        var converted = source.ConvertMessage<int, string>(i => $"#{i}");

        var recv = new QueueReceiver<object>();
        converted.ConnectTo(recv);

        source.Send(5);      // -> "#5"
        source.Send("x");    // forwarded

        Assert.True(recv.TryReceive(out var a));
        Assert.True(recv.TryReceive(out var b));
        Assert.True(recv.IsEmpty);
        Assert.Equal("#5", a);
        Assert.Equal("x", b);
    }

    [Fact]
    public void SplitMessage_splits_matched_and_forwards_rest()
    {
        var source = new Sender();
        var split = source.SplitMessage<int, int>(i => new[] { i, i + 1 });

        var recv = new QueueReceiver<object>();
        split.ConnectTo(recv);

        source.Send(10);     // -> 10, 11
        source.Send("x");    // forwarded

        Assert.True(recv.TryReceive(out var a));
        Assert.True(recv.TryReceive(out var b));
        Assert.True(recv.TryReceive(out var c));
        Assert.True(recv.IsEmpty);

        Assert.Equal(10, a);
        Assert.Equal(11, b);
        Assert.Equal("x", c);
    }
}