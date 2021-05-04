using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FeatureLoom.Services.Supervision
{
    public static class SupervisionService
    {
        static MicroLock myLock = new MicroLock();
        static List<ISupervisor> newSupervisors = new List<ISupervisor>();
        static List<ISupervisor> activeSupervisors = new List<ISupervisor>();
        static List<ISupervisor> handledSupervisors = new List<ISupervisor>();

        static Thread supervisionThread = null;
        static ManualResetEventSlim mre = new ManualResetEventSlim(true);
        static CancellationTokenSource cts = new CancellationTokenSource();        

        public static void AddSupervisor(ISupervisor supervisor)
        {
            using (myLock.Lock())
            {                
                if (supervisionThread == null)
                {
                    newSupervisors.Add(supervisor);
                    mre.Set();
                    supervisionThread = new Thread(StartSupervision);                    
                    supervisionThread.Start();
                }
                else
                {
                    newSupervisors.Add(supervisor);
                    cts.Cancel();
                    mre.Set();
                }
            }
            
        }

        public static void Supervise(Action supervisionAction, Func<bool> checkValidity, Func<TimeSpan> getDelay = null)
        {
            AddSupervisor(new Supervisor(supervisionAction, checkValidity, getDelay));
        }

        static void StartSupervision()
        {
            int stopCounter = 0;
            int timingCounter = 0;
            TimeKeeper timer = AppTime.TimeKeeper;
            while (true)
            {
                if (!CheckForPause(ref stopCounter, ref timingCounter, ref timer)) return;
                stopCounter = CheckForNewSupervisors(stopCounter);
                HandleActiveSupervisors();
                SwapHandledToActive();

                TimeSpan delay = GetDelay();
                if (timingCounter++ == 100_000)
                {
                    timingCounter = 0;
                    //Console.Write($"|{timer.Elapsed.divide(100_000).TotalMilliseconds}|");                    
                    timer.Restart(timer.StartTime + timer.LastElapsed);                    
                }

                AppTime.Wait(delay, cts.Token);
            }
        }

        private static TimeSpan GetDelay()
        {
            TimeSpan minDelay = TimeSpan.MaxValue;
            foreach (var supervisor in activeSupervisors)
            {
                var delay = supervisor.MaxDelay;
                if (delay < minDelay) minDelay = delay;
            }
            return minDelay.Clamp(0.001.Milliseconds(), int.MaxValue.Milliseconds());
        }

        private static void HandleActiveSupervisors()
        {
            foreach (var supervisor in activeSupervisors)
            {
                supervisor.Handle();
                if (supervisor.IsActive) handledSupervisors.Add(supervisor);
            }
        }

        private static int CheckForNewSupervisors(int stopCounter)
        {
            if (newSupervisors.Count > 0)
            {
                using (myLock.Lock())
                {
                    stopCounter = 0;
                    activeSupervisors.AddRange(newSupervisors);
                    newSupervisors.Clear();
                    if (cts.IsCancellationRequested) cts = new CancellationTokenSource();
                }
            }

            return stopCounter;
        }

        private static void SwapHandledToActive()
        {
            var temp = activeSupervisors;
            activeSupervisors = handledSupervisors;
            handledSupervisors = temp;
            handledSupervisors.Clear();
        }

        static bool CheckForPause(ref int stopCounter, ref int timingCounter, ref TimeKeeper timer)
        {
            if (activeSupervisors.Count == 0 && newSupervisors.Count == 0)
            {
                if (++stopCounter > 100)
                {
                    stopCounter = 0;
                    var lockHandle = myLock.Lock();
                    if (activeSupervisors.Count == 0 && newSupervisors.Count == 0)
                    {
                        mre.Reset();
                        lockHandle.Exit();
                        bool wokeUp = mre.Wait(10.Seconds());
                        if (!wokeUp)
                        {
                            using (myLock.Lock())
                            {
                                if (activeSupervisors.Count == 0 && newSupervisors.Count == 0)
                                {
                                    supervisionThread = null;
                                    return false;
                                }
                            }
                        }

                        timingCounter = 0;
                        timer = AppTime.TimeKeeper;

                    }
                    else lockHandle.Exit();
                }
            }

            return true;
        }


        class Supervisor : ISupervisor
        {
            Action handleAction;
            Func<bool> validityCheck;
            Func<TimeSpan> getDelay = () => TimeSpan.Zero;


            public Supervisor(Action handleAction, Func<bool> validityCheck, Func<TimeSpan> getDelay = null)
            {
                this.handleAction = handleAction;
                this.validityCheck = validityCheck;
                this.getDelay = getDelay;
            }

            public TimeSpan MaxDelay => getDelay == null ? TimeSpan.Zero : getDelay();

            public bool IsActive => validityCheck();

            public void Handle()
            {
                handleAction();
            }
        }

    }

}