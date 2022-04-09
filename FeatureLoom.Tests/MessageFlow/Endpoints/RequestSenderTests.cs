using FeatureLoom.Diagnostics;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class RequestSenderTests
    {
        public class TestRequest : IRequestMessage
        {
            public string data;

            public TestRequest(string data)
            {
                this.data = data;
            }

            public long RequestId { get; set; }
        }

        public class TestResponse : IResponseMessage
        {
            public string data;
            public long requestId;

            public TestResponse(string data, long requestId)
            {
                this.data = data;
                this.requestId = requestId;
            }

            public long RequestId => requestId;
        }

        [Fact]
        public void ResponsesCanBeReceived()
        {
            TestHelper.PrepareTestContext();

            var requestSender = new RequestSender<TestRequest, TestResponse>();            
            var requestSink = new SingleMessageTestSink<TestRequest>();
            var responseSender = new Sender<TestResponse>();
            requestSender.ConnectTo(requestSink);
            responseSender.ConnectTo(requestSender);
            var task = requestSender.SendRequestAsync(new TestRequest("ABC"));
            Assert.True(requestSink.received);
            Assert.False(task.IsCompleted);
            responseSender.Send(new TestResponse(requestSink.receivedMessage.data + "!", requestSink.receivedMessage.RequestId));
            Assert.True(task.IsCompleted);
            Assert.Equal("ABC!", task.Result.data);
        }

    }
}