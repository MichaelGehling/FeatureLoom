using System.Collections.Generic;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class ProcessingEndpointTests
    {
        [Fact]
        public void CanProcessMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(msg => processed = msg);
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        [Fact]
        public void CanProcessMessageAsync()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool processed = false;
            var sender = new Sender();
            var processor = new ProcessingEndpoint<bool>(async msg =>
            {
                await Task.Yield();
                processed = msg;
            });
            sender.ConnectTo(processor);
            sender.Send(true);
            Assert.True(processed);
        }

        [Fact]
        public void SyncCheckedFalseRoutesMessageToElse()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var endpoint = new ProcessingEndpoint<int>(_ => false);
            var sink = new RecordingSink();
            endpoint.Else.ConnectTo(sink);

            endpoint.Post(17);

            Assert.Equal(new object[] { 17 }, sink.Snapshot());
        }

        [Fact]
        public async Task AsyncCheckedFalseRoutesMessageToElseAsync()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var endpoint = new ProcessingEndpoint<int>(_ => tcs.Task);
            var sink = new RecordingSink();
            endpoint.Else.ConnectTo(sink);

            var postTask = endpoint.PostAsync(42);
            Assert.False(postTask.IsCompleted);

            tcs.SetResult(false);
            await postTask;

            Assert.Equal(new object[] { 42 }, sink.Snapshot());
        }

        [Fact]
        public async Task AsyncCheckedRejectWithoutElseCompletes()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool invoked = false;
            var endpoint = new ProcessingEndpoint<int>(value =>
            {
                invoked = true;
                return Task.FromResult(false);
            });

            await endpoint.PostAsync(7);

            Assert.True(invoked);
        }

        [Fact]
        public async Task AsyncActionPostAsyncReturnsUnderlyingTask()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool invoked = false;
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var endpoint = new ProcessingEndpoint<int>(value =>
            {
                invoked = true;
                return tcs.Task;
            });

            var postTask = endpoint.PostAsync(5);

            Assert.Same(tcs.Task, postTask);
            Assert.True(invoked);

            Assert.False(postTask.IsCompleted);
            tcs.SetResult(null);
            await postTask;
        }

        [Fact]
        public void NonMatchingMessageTypesAreForwardedToElse()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var endpoint = new ProcessingEndpoint<int>(_ => true);
            var sink = new RecordingSink();
            endpoint.Else.ConnectTo(sink);

            string text = "foreign";
            endpoint.Post(text);
            double other = 3.14;
            endpoint.Post(in other);

            Assert.Equal(new object[] { text, other }, sink.Snapshot());
        }

        private sealed class RecordingSink : IMessageSink
        {
            private readonly List<object> messages = new List<object>();

            public object[] Snapshot()
            {
                lock (messages)
                {
                    return messages.ToArray();
                }
            }

            public void Post<M>(in M message)
            {
                lock (messages)
                {
                    messages.Add(message);
                }
            }

            public void Post<M>(M message)
            {
                lock (messages)
                {
                    messages.Add(message);
                }
            }

            public Task PostAsync<M>(M message)
            {
                Post(message);
                return Task.CompletedTask;
            }
        }
    }
}