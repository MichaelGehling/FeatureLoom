using FeatureLoom.MessageFlow;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Send_Wrappers_Tests
{
    [Fact]
    public async Task Send_and_SendAsync_wrappers_invoke_sink_Post_variants()
    {
        var collected = new List<object>();
        var sink = new ProcessingEndpoint<object>(o =>
        {
            collected.Add(o);
            return true;
        });

        sink.Send("a");
        var one = 1;
        sink.Send(in one);
        await sink.SendAsync(2.5);

        Assert.Equal(3, collected.Count);
        Assert.Contains("a", collected);
        Assert.Contains(1, collected);
        Assert.Contains(2.5, collected);
    }
}