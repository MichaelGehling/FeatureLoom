using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Scheduling
{
    public class ActionSchedule: ISchedule 
    {
        private Func<DateTime, (bool, TimeSpan)> triggerAction;

        public ActionSchedule(Func<DateTime, (bool, TimeSpan)> triggerAction)
        {
            this.triggerAction = triggerAction;
        }            

        public bool Trigger(DateTime now, out TimeSpan maxDelay)
        {
            (bool active, TimeSpan delay) = triggerAction(now);
            maxDelay = delay;
            return active;
        }
    }
}