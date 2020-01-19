using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncWaitHandle : IAsyncWaitHandle
    {
        public static bool WaitAll(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool allProvideWaitHandle = true;
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (!anyWouldWait) return true;            

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAll(handles);
            }
            else
            {
                bool allReady;
                do
                {
                    allReady = true;
                    for (int i = 0; i < asyncWaitHandles.Length; i++)
                    {
                        if (asyncWaitHandles[i].WouldWait())
                        {
                            allReady = false;
                            asyncWaitHandles[i].Wait();
                            break;
                        }
                    }
                }
                while (!allReady);
            }

            return true;
        }
        public static bool WaitAll(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            TimeFrame timeoutFrame = new TimeFrame(timeout);
            

            bool allProvideWaitHandle = true;
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (!anyWouldWait) return true;
            if (timeoutFrame.Elapsed) return false;

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAll(handles, timeoutFrame.RemainingTime);
            }
            else
            {
                bool allReady;
                do
                {
                    allReady = true;
                    for (int i = 0; i < asyncWaitHandles.Length; i++)
                    {
                        if (asyncWaitHandles[i].WouldWait())
                        {
                            allReady = false;
                            asyncWaitHandles[i].Wait(timeoutFrame.RemainingTime);
                            break;
                        }
                    }
                }
                while (!allReady);
            }

            return !timeoutFrame.Elapsed;
        }


        public static int WaitAny(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAny(handles);
            }
            else
            {
                Task[] tasks = new Task[asyncWaitHandles.Length];
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = asyncWaitHandles[i].WaitingTask;
                }
                Task.WhenAny(tasks).Wait();                
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }

        public static int WaitAny(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            TimeFrame timeoutFrame = new TimeFrame(timeout);

            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (timeoutFrame.Elapsed) return WaitHandle.WaitTimeout;

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAny(handles, timeoutFrame.RemainingTime);
            }
            else
            {
                Task[] tasks = new Task[asyncWaitHandles.Length+1];                
                for (int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    tasks[i] = asyncWaitHandles[i].WaitingTask;
                }
                tasks[tasks.Length - 1] = Task.Delay(timeoutFrame.RemainingTime);
                Task.WhenAny(tasks).Wait();
                for (int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }


        public static IAsyncWaitHandle NoWaitingHandle { get; } = new AsyncManualResetEvent3(true);
        public static IAsyncWaitHandle FromTask(Task task) => task.IsCompleted ? NoWaitingHandle : new AsyncWaitHandle(task);
        private Task task;
        
        private AsyncWaitHandle(Task task)
        {
            this.task = task;
        }

        public Task WaitingTask => task;

        public Task<bool> WaitAsync()
        {
            return task.WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return task.WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return task.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return task.WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            task.Wait();
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout)
        {
            return task.Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            task.Wait(cancellationToken);
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return task.Wait((int)timeout.TotalMilliseconds, cancellationToken);            
        }

        public bool WouldWait()
        {
            return !task.IsCompleted;
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            if (task.IsCompleted)
            {
                return NoWaitingHandle.TryConvertToWaitHandle(out waitHandle);
            }
            else
            {
                waitHandle = null;
                return false;
            }
        }

        public static implicit operator AsyncWaitHandle(Task task) => new AsyncWaitHandle(task);
    }
}