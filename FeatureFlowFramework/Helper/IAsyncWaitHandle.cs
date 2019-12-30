using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public interface IAsyncWaitHandle
    {
        Task WaitingTask { get; }

        Task<bool> WaitAsync();

        Task<bool> WaitAsync(TimeSpan timeout);

        Task<bool> WaitAsync(CancellationToken cancellationToken);

        Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);

        bool Wait();

        bool Wait(TimeSpan timeout);

        bool Wait(CancellationToken cancellationToken);

        bool Wait(TimeSpan timeout, CancellationToken cancellationToken);
    }

    public static class IAsyncWaitHandleExtensions
    {
        public static Task[] GetWaitingTasks(this IAsyncWaitHandle[] waitHandles)
        {
            if(waitHandles.Length == 0) return Array.Empty<Task>();

            var tasks = new Task[waitHandles.Length];
            for(int i = 0; i < waitHandles.Length; i++)
            {
                tasks[i] = waitHandles[i].WaitingTask;
            }
            return tasks;
        }

        public static Task[] GetWaitingTasks(this IAsyncWaitHandle[] waitHandles, Task extraTask)
        {
            var tasks = new Task[waitHandles.Length + 1];
            for(int i = 0; i < waitHandles.Length; i++)
            {
                tasks[i] = waitHandles[i].WaitingTask;
            }
            tasks[waitHandles.Length] = extraTask;
            return tasks;
        }
    }
}
