using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Scheduling
{
    public class ActionSchedule: ISchedule 
    {
        private Func<DateTime, ScheduleStatus> triggerAction;
        string name;

        public string Name => name;

        public ActionSchedule(string name, Func<DateTime, ScheduleStatus> triggerAction)
        {
            this.triggerAction = triggerAction;
            this.name = name;
        }        

        public ScheduleStatus Trigger(DateTime now)
        {
            return triggerAction(now);            
        }
    }
}