// Test-only polyfills for BCL members that are missing on .NET Framework 4.8, so the test code
// can stay identical across all target frameworks. Uses C# 14 extension members, which compile
// to plain static methods and need no runtime support.
#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// Global namespace so the extensions are visible in every test file without extra usings.
internal static class NetFrameworkPolyfills
{
    extension(Task task)
    {
        public bool IsCompletedSuccessfully => task.Status == TaskStatus.RanToCompletion;

        public async Task WaitAsync(TimeSpan timeout)
        {
            if (task != await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false)) throw new TimeoutException();
            await task.ConfigureAwait(false);
        }
    }

    extension<T>(Task<T> task)
    {
        public bool IsCompletedSuccessfully => task.Status == TaskStatus.RanToCompletion;

        public async Task<T> WaitAsync(TimeSpan timeout)
        {
            if (task != await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false)) throw new TimeoutException();
            return await task.ConfigureAwait(false);
        }
    }

    extension(TimeSpan)
    {
        public static TimeSpan operator /(TimeSpan timeSpan, double divisor) => TimeSpan.FromTicks((long)(timeSpan.Ticks / divisor));
    }

    extension(double)
    {
        public static bool IsNegative(double value) => BitConverter.DoubleToInt64Bits(value) < 0;
    }

    extension(string text)
    {
        public string[] Split(string separator) => text.Split(new[] { separator }, StringSplitOptions.None);
    }

    extension<T>(ArraySegment<T> segment)
    {
        public T[] ToArray()
        {
            var result = new T[segment.Count];
            Array.Copy(segment.Array, segment.Offset, result, 0, segment.Count);
            return result;
        }
    }

    extension(Process process)
    {
        public Task WaitForExitAsync() => Task.Run(() => process.WaitForExit());
    }
}
#endif
