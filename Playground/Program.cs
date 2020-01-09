using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.Helper;

namespace Playground
{
    class Program
    {
        static AsyncLock myLock = new AsyncLock();

        static void Main(string[] args)
        {
            FunctionTest();
            Console.WriteLine("----");
            //PerformanceTest();
            Console.WriteLine("----");
            PerformanceTestParallel();

            Console.ReadKey();
        }
        private static void PerformanceTestParallel()
        {
            var duration = 5.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            int numReadLocks = 0;
            int numWriteLocks = 2;
            Action work = () =>
            {
                TimeFrame tf = new TimeFrame(0.1.Milliseconds());
                while(!tf.Elapsed) ;
            };

            name = "Overhead";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, Overhead, numReadLocks, Overhead, numWriteLocks, work);
            double time_overhead_ns = timeFactor / c;
            Console.WriteLine(time_overhead_ns + " " + (-1) + " " + name);

            name = "ClassicLock";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ClassicLock, numReadLocks, ClassicLock, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL SyncLock";
            Prepare(out gcs);
            c = RunParallel(new AsyncLock(), duration, ASLReadLock, numReadLocks, ASLWriteLock, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL AsyncLock";
            Prepare(out gcs);
            c = RunParallel(new AsyncLock(), duration, (l,d,w) => ASLReadLockAsync(l,d, w).Result, numReadLocks, (l, d,w) => ASLWriteLockAsync(l, d,w).Result, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL SyncLock Extension";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ASLReadLockExtension, numReadLocks, ASLWriteLockExtension, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "SemaphoreSlim";
            Prepare(out gcs);
            c = RunParallel(new SemaphoreSlim(1,1), duration, SemaphoreLock, numReadLocks, SemaphoreLock, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ReaderWriterLockSlim";
            Prepare(out gcs);
            c = RunParallel(new ReaderWriterLockSlim(), duration, RwLockRead, numReadLocks, RwLockWrite, numWriteLocks, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);
        }

        private static long RunParallel<T>(T lockObj, TimeSpan duration, Func<T, TimeSpan, Action, long> readLock, int numReadLockThreads, Func<T, TimeSpan, Action, long> writeLock, int numWriteLockThreads, Action work)
        {
            long count = 0;
            List<Task> tasks = new List<Task>();
            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            for (int i= 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = writeLock(lockObj, duration, work);
                    Interlocked.Add(ref count, c);
                }));
            }

            for(int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, duration, work);
                    Interlocked.Add(ref count, c);
                }));
            }

            Thread.Sleep(100);
            starter.SetResult(true);
            Task.WhenAll(tasks.ToArray()).Wait();
            return count;
        }


         private static void PerformanceTest()
         {
            var duration = 2.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            Action work = null;

            name = "Overhead";
            Prepare(out gcs);
            c = Overhead(new object(), duration, work);
            double time_overhead_ns = timeFactor / c;
            Console.WriteLine(time_overhead_ns + " " + -1 + " " + name);

            name = "ASL ReadLock";
            Prepare(out gcs);
            c = ASLReadLock(new AsyncLock(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock";
            Prepare(out gcs);
            c = ASLWriteLock(new AsyncLock(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);


            name = "ASL WriteLockAsync";
            Prepare(out gcs);
            c = ASLWriteLockAsync(new AsyncLock(), duration, work).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL ReadLockAsync";
            Prepare(out gcs);
            c = ASLReadLockAsync(new AsyncLock(), duration, work).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock Extension";
            Prepare(out gcs);
            c = ASLWriteLockExtension(new AsyncLock(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock Async Extension";
            Prepare(out gcs);
            c = ASLWriteLockExtensionAsync(new AsyncLock(), duration, work).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Classic Lock";
            Prepare(out gcs);
            c = ClassicLock(new object(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Monitor";
            Prepare(out gcs);
            c = Monitor(new object(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Mutex";
            Prepare(out gcs);
            c = Mutex(new Mutex(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "SpinLock";
            Prepare(out gcs);
            c = SpinLock(new SpinLock(), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock";
            Prepare(out gcs);
            c = SemaphoreLock(new SemaphoreSlim(1,1), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock Async";
            Prepare(out gcs);
            c = SemaphoreLockAsync(new SemaphoreSlim(1, 1), duration, work).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (no recursion)";
            Prepare(out gcs);
            c = RwLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (with recursion)";
            Prepare(out gcs);
            c = RwLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), duration, work);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

        }

        private static void Finish(double timeFactor, string name, long c, int gcs, double time_overhead_ns)
        {
            double time = timeFactor / c - time_overhead_ns;
            gcs = (GC.CollectionCount(0) - gcs);
            long iterationsPerGC = gcs > 0 ? c / gcs : -1;
            Console.WriteLine(time + " " + iterationsPerGC + " " + name);
        }

        private static void Prepare(out int gcs)
        {
            gcs = GC.CollectionCount(0);
        }

        private static long RwLockWrite(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                rwLock.EnterWriteLock();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }

            return c;
        }

        private static long RwLockRead(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                rwLock.EnterReadLock();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }

            return c;
        }

        private static long Mutex(Mutex mutex, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                mutex.WaitOne();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            return c;
        }

        private static long SpinLock(SpinLock spinLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool taken = false;
                try
                {
                    spinLock.Enter(ref taken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if (taken) spinLock.Exit();
                }
            }

            return c;
        }

        private static async Task<long> SemaphoreLockAsync(SemaphoreSlim sema, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                await sema.WaitAsync();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    sema.Release();
                }
            }

            return c;
        }

        private static long SemaphoreLock(SemaphoreSlim sema, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                sema.Wait();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    sema.Release();
                }
            }

            return c;
        }

        private static long Monitor(object obj, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool lockWasTaken = false;
                try
                {
                    System.Threading.Monitor.Enter(obj, ref lockWasTaken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if(lockWasTaken)
                        System.Threading.Monitor.Exit(obj);
                }
            }

            return c;
        }

        private static long ClassicLock(object obj, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                lock(obj)
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static async Task<long> ASLWriteLockExtensionAsync(object obj, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await obj.LockForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
            }
            return c;
        }

        private static long ASLReadLockExtension(object obj, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(obj.LockForReading())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static long ASLWriteLockExtension(object obj, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(obj.LockForWriting())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static async Task<long> ASLReadLockAsync(AsyncLock myLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await myLock.ForReadingAsync())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static async Task<long> ASLWriteLockAsync(AsyncLock myLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await myLock.ForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static long ASLWriteLock(AsyncLock myLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForWriting())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static long ASLReadLock(AsyncLock myLock, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForReading())
                {
                    c++;
                    work?.Invoke();
                }
            }

            return c;
        }

        private static long Overhead(object dummy, TimeSpan duration, Action work)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                c++;
                work?.Invoke();
            }

            return c;
        }

        private static void FunctionTest()
        {
            var duration = 2.Seconds();

            myLock = new AsyncLock();

            long c1 = 0;
            long c2 = 0;
            long c3 = 0;
            long c4 = 0;

            var t1 = Task.Run(async () =>
            {
                TimeFrame tf = new TimeFrame(duration);
                while(!tf.Elapsed)
                {
                    using(await myLock.ForWritingAsync())
                    {
                        c1++;
                    }
                }
            });

            var t2 = Task.Run(async () =>
            {
                TimeFrame tf = new TimeFrame(duration);
                while(!tf.Elapsed)
                {
                    using(await myLock.ForReadingAsync())
                    {
                        c2++;
                    }
                }
            });

            var t3 = Task.Run(() =>
            {
                TimeFrame tf = new TimeFrame(duration);
                while(!tf.Elapsed)
                {
                    using(myLock.ForWriting())
                    {
                        c3++;
                    }
                }
            });

            var t4 = Task.Run(() =>
            {
                TimeFrame tf = new TimeFrame(duration);
                while(!tf.Elapsed)
                {
                    using(myLock.ForReading())
                    {
                        c4++;
                    }
                }
            });

            Task.WhenAll(t1, t2, t3, t4).Wait();

            Console.WriteLine(c1);
            Console.WriteLine(c2);
            Console.WriteLine(c3);
            Console.WriteLine(c4);

            //Console.ReadKey();
        }
    }
}
