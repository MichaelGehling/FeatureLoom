using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FeatureFlowFramework.Services
{
    public static class Supervision
    {
        static FastSpinLock myLock = new FastSpinLock();
        static List<ISupervisor> newSupervisors = new List<ISupervisor>();
        static List<ISupervisor> activeSupervisors = new List<ISupervisor>();
        static List<ISupervisor> handledSupervisors = new List<ISupervisor>();
        
        static Thread supervisionThread = null;
        static ManualResetEventSlim mre = new ManualResetEventSlim(true);

        public static void AddSupervisor(ISupervisor supervisor)
        {
            supervisor.IsActive = true;
            using (myLock.Lock())
            {                
                if (supervisionThread == null)
                {
                    activeSupervisors.Add(supervisor);
                    supervisionThread = new Thread(StartSupervision);
                    supervisionThread.Start();
                }
                else
                {
                    newSupervisors.Add(supervisor);
                    mre.Set();
                }
            }
        }

        public static void Supervise(Func<(bool valid, TimeSpan maxDelay)> supervisionAction)
        {
            AddSupervisor(new Supervisor(supervisionAction));
        }

        static void StartSupervision()
        {
            while (true)
            {
                if (activeSupervisors.Count == 0 && newSupervisors.Count == 0)
                {
                    mre.Reset();
                    mre.Wait();
                }

                if (newSupervisors.Count > 0)
                {
                    using (myLock.Lock())
                    {
                        activeSupervisors.AddRange(newSupervisors);
                        newSupervisors.Clear();
                    }
                }

                foreach (var supervisor in activeSupervisors)
                {
                    (bool valid, TimeSpan maxDelay) = supervisor.Handle();
                    if (valid) handledSupervisors.Add(supervisor);                    
                    else supervisor.IsActive = false;
                }

                var temp = activeSupervisors;
                activeSupervisors = handledSupervisors;
                handledSupervisors = temp;
                handledSupervisors.Clear();

                Thread.Sleep(0);
            }
        }



        class Supervisor : ISupervisor
        {
            Func<(bool valid, TimeSpan maxDelay)> handleAction;
            TimeSpan maxDelay;

            public Supervisor(Func<(bool valid, TimeSpan maxDelay)> supervisionAction)
            {
                this.handleAction = supervisionAction;
            }

            public TimeSpan MaxDelay => maxDelay;

            public bool IsActive { get; set; }

            public (bool valid, TimeSpan maxDelay) Handle()
            {
                return handleAction();
            }
        }

    }

    public interface ISupervisor
    {
        (bool valid, TimeSpan maxDelay) Handle();
        bool IsActive { get; set; }
    }

}