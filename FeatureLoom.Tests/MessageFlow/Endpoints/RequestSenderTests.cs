using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Runtime.CompilerServices;
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
        public async Task Times_out_even_if_sender_is_unreferenced_and_GC_runs()
        {
            using var testContext = TestHelper.PrepareTestContext();

            // The sender is only reachable through its own pending request, so a forced GC must not collect the timeout handling.
            var task = SendWithUnreferencedSender();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(20);
            }

            var completed = await Task.WhenAny(task, Task.Delay(5.Seconds()));
            Assert.Same(task, completed);
            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Task<int> SendWithUnreferencedSender() => new RequestSender<int, int>(timeout: 200.Milliseconds()).SendRequestAsync(1);

        [Fact]
        public async Task Sender_becomes_collectable_after_pending_requests_timed_out()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var weakSender = await SendAndAwaitTimeout();
            for (int i = 0; i < 3 && weakSender.IsAlive; i++)
            {
                await Task.Delay(50);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weakSender.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task<WeakReference> SendAndAwaitTimeout()
        {
            var sender = new RequestSender<int, int>(timeout: 30.Milliseconds());
            await Assert.ThrowsAsync<TaskCanceledException>(() => sender.SendRequestAsync(1));
            return new WeakReference(sender);
        }

        [Fact]
        public async Task Staggered_requests_time_out_individually()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var requester = new RequestSender<int, int>(timeout: 150.Milliseconds());
            var first = requester.SendRequestAsync(1);
            await Task.Delay(100);
            var second = requester.SendRequestAsync(2);

            await Assert.ThrowsAsync<TaskCanceledException>(() => first);
            Assert.False(second.IsCompleted);
            await Assert.ThrowsAsync<TaskCanceledException>(() => second);

            // A new request after the loop ended must restart the timeout handling.
            await Assert.ThrowsAsync<TaskCanceledException>(() => requester.SendRequestAsync(3));
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