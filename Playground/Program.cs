using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.RPC;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services;
using FeatureFlowFramework.Services.MetaData;
using FeatureFlowFramework.Services.Supervision;
using FeatureFlowFramework.Services.Web;
using FeatureFlowFramework.Workflows;
using Nito.AsyncEx;

namespace Playground
{

    public static class SocketExtensions
    {
        public static void SetBlocking(this Socket socket, bool blocking)
        {
            if (socket != null) socket.Blocking = blocking;
        }
    }
    partial class Program
    {

        volatile static bool done = false;

        public static async Task YieldAsync() => await Task.Yield();
        public static async Task WaitAsync() {}//await Task.Delay(0);      
        public static void EmptyAction() { }

        static void Main(string[] args)
        {

            int ex = 200_000;

            long start_mem = GC.GetTotalMemory(true);
            object[] array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new FeatureLock();
                array[n] = obj;
                using (obj.LockAsync().WaitFor()) {}
                obj.TryLockAsync(TimeSpan.Zero).WaitFor();
            }
            double used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" FeatureLock Async: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new FeatureLock();
                array[n] = obj;
                using (obj.Lock()) { }
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" FeatureLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            var settings = new FeatureLock.FeatureLockSettings();
            for (int n = 0; n < ex; n++)
            {
                var obj = new FeatureLock(settings);
                array[n] = obj;
                using (obj.Lock()) { }
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" FeatureLock settings: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new object();
                array[n] = obj;
                Monitor.Enter(obj);
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" Monitor: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            var array3 = new SpinLock[ex];
            for (int n = 0; n < ex; n++)
            {
                array3[n] = new SpinLock(false);
                bool sl = false;
                array3[n].Enter(ref sl);
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" SpinLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new SemaphoreSlim(1, 1);
                array[n] = obj;
                obj.Wait();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" SemaphoreSlim: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new ReaderWriterLockSlim();
                array[n] = obj;
                obj.EnterWriteLock();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" ReaderWriterLockSlim: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new AsyncLock();
                array[n] = obj;
                obj.Lock();                
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" AsyncEx.AsyncLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new AsyncReaderWriterLock();
                array[n] = obj;
                obj.WriterLock();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" AsyncEx.AsyncReaderWriterLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new NeoSmart.AsyncLock.AsyncLock();
                array[n] = obj;
                obj.Lock();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" NeoSmart.AsyncLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new Bmbsqd.Async.AsyncLock();
                array[n] = obj;
                obj.GetAwaiter();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" Bmbsqd.AsyncLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new Microsoft.VisualStudio.Threading.AsyncReaderWriterLock();
                array[n] = obj;
                obj.WriteLockAsync();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" VS.AsyncReaderWriterLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new MicroLock();
                array[n] = obj;
                obj.Lock();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" MicroLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            var array2 = new MicroValueLock[ex];
            for (int n = 0; n < ex; n++)
            {
                array2[n] = new MicroValueLock();
                array2[n].Enter();
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" MicroValueLock: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                array[n] = new List<object>(3);
                //using (array[n].LockAsync().WaitFor()) { }
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" List1: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                array[n] = new object[3];
                //using (array[n].LockAsync().WaitFor()) { }
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" Array1: {used_mem_median} Bytes");

            start_mem = GC.GetTotalMemory(true);
            array = new Task[ex];
            for (int n = 0; n < ex; n++)
            {
                array[n] = new Task(EmptyAction);
                //using (array[n].LockAsync().WaitFor()) { }
            }
            used_mem_median = (GC.GetTotalMemory(false) - start_mem) / ex;
            Console.WriteLine($" Task: {used_mem_median} Bytes");

            Console.ReadKey();


            int num = 100_000;
            TimeKeeper y;

            DateTime x;
            var tk = AppTime.TimeKeeper;

            tk.Restart();
            for (int i = 0; i < num; i++)
            {
                using (SynchronizationContext.Current.Suspend()) WaitAsync().WaitFor();
            };
            Console.WriteLine($"CON {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++)
            {
                using(SynchronizationContext.Current.Suspend()) WaitAsync().WaitFor();
            };
            Console.WriteLine($"CON {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            tk.Restart();
            for (int i = 0; i < num; i++)
            {
                using (SynchronizationContext.Current.Suspend()) WaitAsync().WaitFor();
            };
            Console.WriteLine($"CON {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++)
            {
                try
                {
                    WaitAsync().WaitFor();
                }
                finally
                {

                }
            }
            Console.WriteLine($"ASYNC {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) YieldAsync().WaitFor();
            Console.WriteLine($"Y {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) YieldAsync().WaitFor();
            Console.WriteLine($"Y {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) WaitAsync().WaitFor();
            Console.WriteLine($"ASYNC {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) AppTime.WaitAsync(1.Milliseconds()).WaitFor();
            Console.WriteLine($"AWAIT {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) ;
            Console.WriteLine($"NIX {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) AppTime.Wait(1.Milliseconds());
            Console.WriteLine($"WAIT {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) x = AppTime.Now;
            Console.WriteLine($"AT {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) x = AppTime.CoarseNow;
            Console.WriteLine($"CT {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) x = DateTime.UtcNow;
            Console.WriteLine($"UTC {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");

            tk.Restart();
            for (int i = 0; i < num; i++) y = AppTime.TimeKeeper;
            Console.WriteLine($"SW {tk.Elapsed.TotalMilliseconds / num * 1_000_000}");


            Console.ReadKey();


            ManualResetEventSlim mre = new ManualResetEventSlim(false);
            
            int count = 0;
            new Thread(() =>
            {
                Thread.Sleep(100);
                mre.Set();
                var ticks = new TimeSpan((long)Environment.TickCount * 1000);                
                done = true;
            }).Start();
            mre.Wait();
            while (!done) count++;
            Console.WriteLine($"{count}");
            Console.ReadKey();

            int a = 0;
            SupervisionService.Supervise(() => Console.WriteLine($"a{a++}"), ()=> a < 10);

            int b = 0;
            SupervisionService.Supervise(() => Console.WriteLine($"b{b++}"), () => b < 10);

            int c = 0;
            SupervisionService.Supervise(() => Console.WriteLine($"c{c++}"), () => c < 10);

            Console.ReadKey();

        }



    }
}
