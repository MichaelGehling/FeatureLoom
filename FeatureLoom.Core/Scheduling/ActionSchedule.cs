using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Scheduling
{
    public class ActionSchedule: ISchedule 
    {
        private Func<DateTime, TimeFrame> triggerAction;
        string name;

        public string Name => name;

        public ActionSchedule(string name, Func<DateTime, TimeFrame> triggerAction)
        {
            this.triggerAction = triggerAction;
            this.name = name;
        }        

        public TimeFrame Trigger(DateTime now)
        {
            return triggerAction(now);            
        }
    }
}