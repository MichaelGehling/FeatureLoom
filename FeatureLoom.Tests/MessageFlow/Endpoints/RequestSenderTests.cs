using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class RequestSenderTests
    {
        [Fact]
        public async Task Async_forward_is_awaited_before_response_is_returned()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var requester = new RequestSender<int, string>();
            var responder = new AsyncResponder(requester);
            requester.ConnectTo(responder);

            var sendTask = requester.SendRequestAsync(7);

            Assert.False(sendTask.IsCompleted); // awaiting responder.PostAsync

            responder.CompletePending(); // finishes PostAsync

            var result = await sendTask;
            Assert.Equal("R7", result);
        }

        [Fact]
        public async Task Times_out_when_no_response_arrives()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var requester = new RequestSender<int, int>(timeout: 50.Milliseconds());

            var task = requester.SendRequestAsync(1);

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => task);
            Assert.IsType<TaskCanceledException>(ex);
        }

        [Fact]
        public async Task Correlates_multiple_inflight_requests_out_of_order()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var requester = new RequestSender<int, string>();
            var receiver = new QueueReceiver<IRequestMessage<int>>();
            var responseSender = new Sender<IResponseMessage<string>>();

            requester.ConnectTo(receiver);
            responseSender.ConnectTo(requester);

            var t1 = requester.SendRequestAsync(1);
            var t2 = requester.SendRequestAsync(2);

            Assert.True(receiver.TryReceive(out var first));
            Assert.True(receiver.TryReceive(out var second));

            // Respond out of order
            responseSender.SendResponse("B", second.RequestId);

            Assert.Equal("B", await t2);
            Assert.False(t1.IsCompleted);

            responseSender.SendResponse("A", first.RequestId);

            Assert.Equal("A", await t1);
        }

        private sealed class AsyncResponder : IMessageSink
        {
            private readonly RequestSender<int, string> requester;
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private long pendingRequestId;

            public AsyncResponder(RequestSender<int, string> requester)
            {
                this.requester = requester;
            }

            public void Post<M>(in M message)
            {
                Post(message);
            }

            public void Post<M>(M message)
            {
                if (message is IRequestMessage<int> req)
                {
                    pendingRequestId = req.RequestId;
                    requester.Post(new ResponseMessage<string>($"R{req.Content}", req.RequestId));
                }
            }

            public Task PostAsync<M>(M message)
            {
                Post(message);
                return completion.Task;
            }

            public void CompletePending()
            {
                completion.TrySetResult(true);
            }
        }
    }
}