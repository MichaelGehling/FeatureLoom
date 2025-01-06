using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class MonitorSubjects
    {
        private object lockObj = new object();

        public void Init() => lockObj = new object();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            lock (lockObj)
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action, bool prio)
        {
            lock (lockObj)
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if (Monitor.TryEnter(lockObj))
            {
                try
                {
                    action();
                }
                finally
                {
                    Monitor.Exit(lockObj);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action, bool prio)
        {
            if (Monitor.TryEnter(lockObj))
            {
                try
                {
                    action();
                }
                finally
                {
                    Monitor.Exit(lockObj);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            lock (lockObj)
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (Monitor.TryEnter(lockObj))
            {
                try
                {
                }
                finally
                {
                    Monitor.Exit(lockObj);
                }
            }
        }
    }
}