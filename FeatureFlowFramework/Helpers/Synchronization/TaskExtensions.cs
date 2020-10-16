using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public static class TaskExtensions
    {
        public async static Task<bool> WaitAsync(this Task task)
        {
            if(task.IsCanceled || task.IsFaulted) return false;
            else if(task.IsCompleted) return true;

            await task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            if(task.IsCanceled || task.IsFaulted) return false;
            else if(task.IsCompleted) return true;

            await Task.WhenAny(task, Task.Delay(timeout));

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;

            /*
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            var cancellationToken = cts.Token;

            var tcs = new TaskCompletionSource<bool>();            
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if(t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if(t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if(t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */
        }

        public async static Task<bool> WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if(task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if(task.IsCompleted) return true;
            try
            {
                using(var cancelRegistration = cancellationToken.Register(() => throw new TaskCanceledException(task), true))
                {
                    await task;
                }
            }    
            catch(TaskCanceledException)
            {
                return false;
            }

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;

            /*
            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if(t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if(t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if(t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if(task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if(task.IsCompleted) return true;

            try
            {
                await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));
            }
            catch (TaskCanceledException)
            {                
                return false;
            }

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;

            /*
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCTS.CancelAfter(timeout);
            var linkedToken = linkedCTS.Token;

            var tcs = new TaskCompletionSource<bool>();
            var registration = linkedToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if(t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if(t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if(t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */
        }
    }
}