using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Scheduling
{
    public class ActionSchedule: ISchedule 
    {
        private Func<DateTime, (bool, TimeSpan)> triggerAction;
        string name;

        public string Name => name;

        public ActionSchedule(string name, Func<DateTime, (bool, TimeSpan)> triggerAction)
        {
            this.triggerAction = triggerAction;
            this.name = name;
        }        

        public bool Trigger(DateTime now, out TimeSpan maxDelay)
        {
            (bool active, TimeSpan delay) = triggerAction(now);
            maxDelay = delay;
            return active;
        }
    }
}