using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

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

        public static void BackgroundTest()
        {
            var tk = AppTime.TimeKeeper;
            for (int i = 0; i < 1_000_000; i++)
            {
                Thread.CurrentThread.IsBackground = true;
                Thread.CurrentThread.IsBackground = false;
            }
            Console.WriteLine(tk.Elapsed.Ticks * 100 / 1_000_000);
        }

        private static void Main(string[] args)
        {

            var thread = new Thread(BackgroundTest);
            thread.Start();
            thread.Join();
            Console.ReadKey();


            InMemoryCache<string, string> cache = new FeatureLoom.Collections.InMemoryCache<string, string>(str => System.Text.ASCIIEncoding.Unicode.GetByteCount(str),
                new InMemoryCache<string, string>.CacheSettings() 
                {
                    targetCacheSizeInByte= 300,
                    cacheSizeMarginInByte = 200,
                    maxUnusedTimeInSeconds = 100
                });
                        
            cache.Add("B", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");
            cache.Add("A", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            cache.Add("C", "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC");
            cache.StartCleanUp();

            Console.ReadKey();



            var tk = AppTime.TimeKeeper;
            for(int i=0; i < 1_000_000; i++)
            {
                var x = AppTime.Elapsed;
            }
            Console.WriteLine($"SW: { tk.Elapsed.Ticks * 100.0 / 1_000_000}");

            tk = AppTime.TimeKeeper;
            for (int i = 0; i < 1_000_000; i++)
            {
                var x = AppTime.Now;
            }
            Console.WriteLine($"UTC: { tk.Elapsed.Ticks * 100.0 / 1_000_000}");

            tk = AppTime.TimeKeeper;
            for (int i = 0; i < 1_000_000; i++)
            {
                var x = AppTime.CoarseNow;
            }
            Console.WriteLine($"Coarse: { tk.Elapsed.Ticks * 100.0 / 1_000_000}");

            tk = AppTime.TimeKeeper;
            for (int i = 0; i < 1_000_000; i++)
            {
                //var x = Environment.TickCount;
                var x = Environment.TickCount.Milliseconds();
            }
            Console.WriteLine($"tick: { tk.Elapsed.Ticks * 100.0 / 1_000_000}");

            Console.ReadKey();








            int ex = 200_000;

            long start_mem = GC.GetTotalMemory(true);
            object[] array = new object[ex];
            for (int n = 0; n < ex; n++)
            {
                var obj = new FeatureLock();
                array[n] = obj;
                using (obj.LockAsync().WaitFor()) { }
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

            Console.ReadKey();
        }
    }
}