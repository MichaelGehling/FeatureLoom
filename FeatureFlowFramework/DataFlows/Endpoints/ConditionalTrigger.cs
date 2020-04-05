using System;

namespace FeatureFlowFramework.DataFlows
{
    public class ConditionalTrigger<T, R> : MessageTrigger
    {
        private readonly Predicate<T> triggerCondition;
        private readonly Predicate<R> resetCondition;

        public ConditionalTrigger(Predicate<T> triggerCondition, Predicate<R> resetCondition = null)
        {
            this.triggerCondition = triggerCondition;
            this.resetCondition = resetCondition;
        }

        protected override void HandleMessage<M>(in M message)
        {
            if(message is T trigger && triggerCondition(trigger)) Trigger();
            if(message is R reset && resetCondition != null && resetCondition(reset)) Reset();
        }
    }
}