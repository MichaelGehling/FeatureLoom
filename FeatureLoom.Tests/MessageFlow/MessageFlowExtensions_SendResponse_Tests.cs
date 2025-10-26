using FeatureLoom.MessageFlow;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_SendResponse_Tests
{
    [Fact]
    public void SendResponse_untyped_sends_ResponseMessage()
    {
        var sender = new Sender();
        var recv = new QueueReceiver<object>();
        sender.ConnectTo(recv);

        sender.SendResponse("ok", requestId: 42);

        Assert.True(recv.TryReceive(out var obj));
        var msg = Assert.IsAssignableFrom<IResponseMessage<string>>(obj);
        Assert.Equal("ok", msg.Content);
        Assert.Equal(42, msg.RequestId);
    }

    [Fact]
    public async Task SendResponseAsync_untyped_and_typed_work()
    {
        var untyped = new Sender();
        var untypedRecv = new QueueReceiver<object>();
        untyped.ConnectTo(untypedRecv);

        var typed = new Sender<IResponseMessage<int>>();
        var typedRecv = new QueueReceiver<IResponseMessage<int>>();
        typed.ConnectTo(typedRecv);

        await untyped.SendResponseAsync(5, 101);
        await typed.SendResponseAsync(6, 202);

        Assert.True(untypedRecv.TryReceive(out var o));
        var r1 = Assert.IsAssignableFrom<IResponseMessage<int>>(o);
        Assert.Equal(5, r1.Content);
        Assert.Equal(101, r1.RequestId);

        Assert.True(typedRecv.TryReceive(out var r2));
        Assert.Equal(6, r2.Content);
        Assert.Equal(202, r2.RequestId);
    }
}