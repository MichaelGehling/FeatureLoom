using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class AsyncWaitHandle : IAsyncWaitHandle
    {
        #region static
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

        public static async Task<bool> WaitAllAsync(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool anyWouldWait = false;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if(!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks());
            return true;
        }

        public static bool WaitAll(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return false;

            bool allReady;
            do
            {
                allReady = true;
                for(int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    if(asyncWaitHandles[i].WouldWait())
                    {
                        allReady = false;
                        asyncWaitHandles[i].Wait(token);
                        break;
                    }
                }
            }
            while(!allReady);

            return true;
        }

        public static async  Task<bool> WaitAllAsync(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return false;

            bool anyWouldWait = false;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if(!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).WaitAsync(token);
            return true;
        }

        public static bool WaitAll(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(timeout <= TimeSpan.Zero) return false;

            DateTime now = AppTime.Now;
            TimeFrame timeoutFrame = new TimeFrame(now, timeout);

            bool allProvideWaitHandle = true;
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (!anyWouldWait) return true;
            if (timeoutFrame.Elapsed(now)) return false;

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAll(handles, timeoutFrame.Remaining(now));
            }
            else
            {
                bool allReady;
                do
                {
                    allReady = true;
                    for (int i = 0; i < asyncWaitHandles.Length && !timeoutFrame.Elapsed(now); i++)
                    {
                        if (asyncWaitHandles[i].WouldWait())
                        {
                            allReady = false;
                            asyncWaitHandles[i].Wait(timeoutFrame.Remaining(now));
                            now = AppTime.Now;
                            break;
                        }
                    }
                }
                while (!allReady);
            }

            return !timeoutFrame.Elapsed(now);
        }

        public static async Task<bool> WaitAllAsync(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(timeout <= TimeSpan.Zero) return false;

            bool anyWouldWait = false;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if(!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).WaitAsync(timeout);
            return true;
        }

        public static bool WaitAll(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return false;
            if(timeout <= TimeSpan.Zero) return false;

            DateTime now = AppTime.Now;
            TimeFrame timeoutFrame = new TimeFrame(now, timeout);

            bool allReady;
            do
            {
                allReady = true;
                for(int i = 0; i < asyncWaitHandles.Length && !timeoutFrame.Elapsed(now); i++)
                {
                    if(asyncWaitHandles[i].WouldWait())
                    {
                        allReady = false;
                        asyncWaitHandles[i].Wait(timeoutFrame.Remaining(now), token);
                        now = AppTime.Now;
                        break;
                    }
                }
            }
            while(!allReady);

            return true;
        }

        public static async Task<bool> WaitAllAsync(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return false;
            if(timeout <= TimeSpan.Zero) return false;

            bool anyWouldWait = false;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if(!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).WaitAsync(timeout, token);
            return true;
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
                Task[] tasks = asyncWaitHandles.GetWaitingTasks();
                Task.WhenAny(tasks).WaitFor();                
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }

        public static async Task<int> WaitAnyAsync(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks();
            await Task.WhenAny(tasks);
            for(int i = 0; i < tasks.Length; i++)
            {
                if(tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }

        public static int WaitAny(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return WaitHandle.WaitTimeout;

            bool allProvideWaitHandle = true;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if(allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length + 1];
                for(int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                handles[handles.Length - 1] = token.WaitHandle;
                return WaitHandle.WaitAny(handles);
            }
            else
            {
                Task[] tasks = asyncWaitHandles.GetWaitingTasks();
                Task.WhenAny(tasks).Wait(token);
                for(int i = 0; i < tasks.Length; i++)
                {
                    if(tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }

        public static async Task<int> WaitAnyAsync(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return WaitHandle.WaitTimeout;

            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks();
            await Task.WhenAny(tasks).WaitAsync(token);
            for(int i = 0; i < tasks.Length; i++)
            {
                if(tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }

        public static int WaitAny(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

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
                return WaitHandle.WaitAny(handles, timeout);
            }
            else
            {
                Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
                Task.WhenAny(tasks).WaitFor();
                for (int i = 0; i < tasks.Length-1; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }

        public static async Task<int> WaitAnyAsync(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
            await Task.WhenAny(tasks);
            for(int i = 0; i < tasks.Length-1; i++)
            {
                if(tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }

        public static int WaitAny(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return WaitHandle.WaitTimeout;
            if(timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            bool allProvideWaitHandle = true;
            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if(allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length + 1];
                for(int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                handles[handles.Length - 1] = token.WaitHandle;
                var index = WaitHandle.WaitAny(handles, timeout);
                if(index == handles.Length - 1) index = WaitHandle.WaitTimeout;
                return index;
            }
            else
            {
                Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
                Task.WhenAny(tasks).Wait(token);
                for(int i = 0; i < tasks.Length-1; i++)
                {
                    if(tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
            }
        }

        public static async Task<int> WaitAnyAsync(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if(token.IsCancellationRequested) return WaitHandle.WaitTimeout;
            if(timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            for(int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if(!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
            await Task.WhenAny(tasks).WaitAsync(token);
            for(int i = 0; i < tasks.Length-1; i++)
            {
                if(tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }


        public static IAsyncWaitHandle NoWaitingHandle { get; } = new AsyncManualResetEvent(true);
        public static IAsyncWaitHandle FromTask(Task task) => task.IsCompleted ? NoWaitingHandle : new AsyncWaitHandle(task);
        #endregion static

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
            task.WaitFor();
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