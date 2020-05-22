using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class MessageTriggerTests
    {
        [Fact]
        public void IsSetToTriggeredWhenReceivesAnyMessage()
        {
            TestHelper.PrepareTestContext();

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
            TestHelper.PrepareTestContext();

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
            TestHelper.PrepareTestContext();

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
