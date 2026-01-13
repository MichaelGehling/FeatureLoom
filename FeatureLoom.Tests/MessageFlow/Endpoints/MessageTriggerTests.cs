using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageTriggerTests
    {
        [Fact]
        public void IsSetToTriggeredWhenReceivesAnyMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            MessageTrigger trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);
            Assert.False(trigger.IsTriggered());
            trigger.Post(42);
            Assert.True(trigger.IsTriggered());
            Assert.True(trigger.IsTriggered(true));
            Assert.False(trigger.IsTriggered());
            trigger.Post(43);
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void CanToggleStatusWhenReceivingMultipleMessages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            MessageTrigger trigger = new MessageTrigger(MessageTrigger.Mode.Toggle);
            Assert.False(trigger.IsTriggered());
            trigger.Post(42);
            Assert.True(trigger.IsTriggered());
            trigger.Post(43);
            Assert.False(trigger.IsTriggered());
            trigger.Post(44);
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void CanAutoResetTriggerStatus()
        {
            using var testContext = TestHelper.PrepareTestContext();

            MessageTrigger trigger = new MessageTrigger(MessageTrigger.Mode.InstantReset);
            Assert.False(trigger.IsTriggered());
            var task = trigger.WaitAsync();
            Assert.False(task.IsCompleted);
            trigger.Post(42);
            Assert.False(trigger.IsTriggered());
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task WaitingTask_Completes_WhenTriggered()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);
            var waitingTask = trigger.WaitingTask;
            Assert.False(waitingTask.IsCompleted);
            trigger.Post(1);
            await waitingTask;
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void Wait_ReturnsTrue_WhenTriggered_AndFalse_OnTimeout()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);

            // Timeout should return false when not set
            Assert.False(trigger.Wait(TimeSpan.FromMilliseconds(10)));

            // After triggering, Wait should return true
            trigger.Post(1);
            Assert.True(trigger.Wait());
        }

        [Fact]
        public async Task WaitAsync_SupportsTimeout_AndCancellation()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);

            // Timeout path
            var timedResult = await trigger.WaitAsync(TimeSpan.FromMilliseconds(10));
            Assert.False(timedResult);

            // Cancellation path
            using var cts = new CancellationTokenSource();
            var waitTask = trigger.WaitAsync(cts.Token);
            Assert.False(waitTask.IsCompleted);
            cts.Cancel();
            var cancelledResult = await waitTask;
            Assert.False(cancelledResult);

            // Positive path
            var tcs = trigger.WaitAsync();
            trigger.Post(1);
            Assert.True(await tcs);
        }

        [Fact]
        public async Task InstantReset_Pulses_Current_Waiters_And_DoesNotLeave_Set()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.InstantReset);

            var waiter1 = trigger.WaitAsync();
            var waiter2 = trigger.WaitAsync();
            Assert.False(waiter1.IsCompleted);
            Assert.False(waiter2.IsCompleted);

            // PulseAll should complete current waiters only
            trigger.Post(1);

            Assert.True(await waiter1);
            Assert.True(await waiter2);

            // It should not remain set
            Assert.False(trigger.IsTriggered());
            Assert.True(trigger.WouldWait());
        }

        [Fact]
        public void Toggle_Does_Not_Remain_Set_When_Toggled_Twice()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.Toggle);

            Assert.True(trigger.WouldWait());
            trigger.Post(1);
            Assert.False(trigger.WouldWait());
            trigger.Post(2);
            Assert.True(trigger.WouldWait());
        }

        [Fact]
        public async Task PostAsync_Trigers_Asynchronously()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);
            var waitTask = trigger.WaitAsync();
            Assert.False(waitTask.IsCompleted);

            await trigger.PostAsync(123);
            Assert.True(await waitTask);
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void Post_In_Overload_Works_And_DoesNotRequire_Copy()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);

            var value = 42;
            trigger.Post(in value);
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void TryConvertToWaitHandle_Allows_Synchronous_Wait()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);
            Assert.True(trigger.TryConvertToWaitHandle(out var wh));

            using (wh)
            {
                // Initially not signaled; WaitOne with short timeout should return false
                Assert.False(wh.WaitOne(TimeSpan.FromMilliseconds(10)));

                // After triggering, WaitOne should return true
                trigger.Trigger();
                Assert.True(wh.WaitOne(TimeSpan.FromMilliseconds(50)));
            }
        }

        [Fact]
        public void IsTriggered_Can_Reset_When_Requested()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);
            trigger.Trigger();

            Assert.True(trigger.IsTriggered(reset: true));
            Assert.False(trigger.IsTriggered());
        }

        [Fact]
        public void WouldWait_Reflects_Current_State()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var trigger = new MessageTrigger(MessageTrigger.Mode.ManualReset);

            Assert.True(trigger.WouldWait());
            trigger.Trigger();
            Assert.False(trigger.WouldWait());
            trigger.Reset();
            Assert.True(trigger.WouldWait());
        }
    }
}