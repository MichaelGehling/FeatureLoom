using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public static class TaskExtensions
    {
        public static Task StartAndReturn(this Task task)
        {
            task.Start();
            return task;
        }

        public async static Task<bool> WaitAsync(this Task task)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;

            await task;

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        /*
        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            if(task.IsCanceled || task.IsFaulted) return false;
            else if(task.IsCompleted) return true;

            //TODO: may create a memory leak, because Delay may run forever!!!
            await Task.WhenAny(task, Task.Delay(timeout));

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if(task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if(task.IsCompleted) return true;

            //TODO: will create a memory leak, because Delay will run forever!!!
            await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if(task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if(task.IsCompleted) return true;

            //TODO: may create a memory leak, because Delay may run forever!!!
            await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }
        */

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            var cancellationToken = cts.Token;

            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.SetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = (Tuple<TaskCompletionSource<bool>, CancellationTokenRegistration>)s;

                if (t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.Item1.TrySetException(t.Exception.GetBaseException());
                }

                if (t.IsCanceled)
                {
                    tcsAndRegistration.Item1.TrySetCanceled();
                }

                if (t.IsCompleted)
                {
                    tcsAndRegistration.Item1.TrySetResult(true);
                }

                tcsAndRegistration.Item2.Dispose();
            },
            Tuple.Create(tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;

            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                // TODO name tuple items (TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr) 
                var tcsAndRegistration = (Tuple<TaskCompletionSource<bool>, CancellationTokenRegistration>)s;

                if (t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.Item1.TrySetException(t.Exception.GetBaseException());
                }

                if (t.IsCanceled)
                {
                    tcsAndRegistration.Item1.TrySetCanceled();
                }

                if (t.IsCompleted)
                {
                    tcsAndRegistration.Item1.TrySetResult(true);
                }

                tcsAndRegistration.Item2.Dispose();
            }, Tuple.Create(tcs, registration), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            await tcs.Task;

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;

            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCTS.CancelAfter(timeout);
            var linkedToken = linkedCTS.Token;

            var tcs = new TaskCompletionSource<bool>();
            var registration = linkedToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.SetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = (Tuple<TaskCompletionSource<bool>, CancellationTokenRegistration>)s;

                if (t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.Item1.TrySetException(t.Exception.GetBaseException());
                }

                if (t.IsCanceled)
                {
                    tcsAndRegistration.Item1.TrySetCanceled();
                }

                if (t.IsCompleted)
                {
                    tcsAndRegistration.Item1.TrySetResult(true);
                }

                tcsAndRegistration.Item2.Dispose();
            }, Tuple.Create(tcs, registration), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            await tcs.Task;

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }
    }
}