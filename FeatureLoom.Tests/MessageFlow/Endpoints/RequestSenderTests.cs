using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class RequestSenderTests
    {

        [Fact]
        public void ResponsesCanBeReceived()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var requestSender = new RequestSender<string, string>();            
            var receiver = new LatestMessageReceiver<IRequestMessage<string>>();
            var responseSender = new Sender<IResponseMessage<string>>();
            requestSender.ConnectTo(receiver);
            responseSender.ConnectTo(requestSender);

            var task = requestSender.SendRequestAsync("ABC");
            Assert.True(receiver.TryReceiveRequest(out string request, out long requestId));
            Assert.False(task.IsCompleted);

            responseSender.SendResponse(request + "!", requestId);
            Assert.True(task.IsCompleted);
            Assert.Equal("ABC!", task.Result);

            requestSender.ProcessMessage<IRequestMessage<string>>(req => responseSender.SendResponse(req.Content + "?", req.RequestId));
            Assert.Equal("XYZ?", requestSender.SendRequest("XYZ"));
        }

    }
}