using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageTriggerTests
    {
        [Fact]
        public void IsSetToTriggeredWhenReceivesAnyMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            MessageTrigger trigger = new MessageTrigger(MessageTrigger.Mode.Default);
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
    }
}