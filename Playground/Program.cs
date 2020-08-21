using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using FeatureFlowFramework.Services.Web;
using FeatureFlowFramework.Workflows;
using Nito.AsyncEx;

namespace Playground
{
    partial class Program
    {



        static void Test1(DateTime i)
        {
            var x = i;
        }

        static void Test2(in DateTime i)
        {
            var x = i;
        }

        static void Test3<T>(T i)
        {
            var x = i;
        }

        static void Test4<T>(in T i)
        {
            var x = i;
        }

        static void Test5(object i)
        {
            var x = i;
        }

        static void Main(string[] args)
        {

            /* var guessTheWord = new GuessTheWord();
             guessTheWord.Run();
             guessTheWord.WaitUntil(info => info.executionEvent == Workflow.ExecutionEventList.WorkflowFinished);

                 HttpServerRpcAdapter webRPC = new HttpServerRpcAdapter("rpc/a/", 1.Seconds());
             RpcCallee callee = new RpcCallee();
             callee.RegisterMethod<int, int, int>("Add", (a, b) =>
             {
                 int c = a + b;
                 Console.WriteLine($"RPC: {a}+{b}={c}");
                 return c;
             });
             callee.RegisterMethod("Kill", () =>
             {
                 Task.Run(()=> 
                 {
                     Thread.Sleep(100.Milliseconds());
                     Environment.Exit(1);
                 });
             });
             webRPC.ConnectToAndBack(callee);
             SharedWebServer.WebServer.Start();

             Console.ReadKey();

             Sender sender = new Sender();
             sender.ConnectTo(new ProcessingEndpoint<DateTime>(i => { var xy = i; }), weakReference:true);

             var timeKeeper = AppTime.TimeKeeper;

             var now = AppTime.Now;

             timeKeeper.Restart();
             for (long i = 0; i < 10_000_000; i++) Test1(now.AddMilliseconds(i));
             Console.WriteLine($"Test1(long i):{timeKeeper.Elapsed}");

             timeKeeper.Restart();
             for(long i = 0; i < 10_000_000; i++) Test2(now.AddMilliseconds(i));
             Console.WriteLine($"Test2(in long i):{timeKeeper.Elapsed}");

             timeKeeper.Restart();
             for(long i = 0; i < 10_000_000; i++) Test3(now.AddMilliseconds(i));
             Console.WriteLine($"Test3<T>(T i):{timeKeeper.Elapsed}");

             timeKeeper.Restart();
             for(long i = 0; i < 10_000_000; i++) Test4(now.AddMilliseconds(i));
             Console.WriteLine($"Test4<T>(in T i):{timeKeeper.Elapsed}");

             timeKeeper.Restart();
             for(long i = 0; i < 10_000_000; i++) Test5(now.AddMilliseconds(i));
             Console.WriteLine($"Test5(object i):{timeKeeper.Elapsed}");

             timeKeeper.Restart();
             for (long i = 0; i < 10_000_000; i++) sender.Send(now.AddMilliseconds(i));
             Console.WriteLine($"DataFlow:{timeKeeper.Elapsed}");


             Console.ReadKey();

             QueueReceiver<SharedDataUpdateNotification> updateReceiver = new QueueReceiver<SharedDataUpdateNotification>();
             SharedData<int> sharedInt = new SharedData<int>(42);
             SharedData<string> sharedObj = new SharedData<string>("Hello");
             sharedObj.UpdateNotifications.ConnectTo(updateReceiver);

             using (var myInt = sharedInt.GetReadAccess())
             using (var myObj = sharedObj.GetWriteAccess(99))
             {
                 myObj.SetValue(myObj.Value + myInt.Value);                
             }            

             if (updateReceiver.TryReceive(out SharedDataUpdateNotification update))
             {
                 if (update.originatorId == 99 && update.sharedData is SharedData<string> objUpdate)
                 {
                     objUpdate.WithReadAccess(reader => Console.WriteLine(reader.Value));
                 }
             }            

             var timer = AppTime.TimeKeeper;
             TimeSpan x;
             long c1 = 0;
             while(timer.Elapsed < 1.Seconds())
             {
                 x = AppTime.Elapsed;
                 c1++;
             }

             timer.Restart();
             long c2 = 0;
             DateTime s = AppTime.Now;
             TimeSpan y;
             while(timer.Elapsed < 1.Seconds())
             {
                 y = AppTime.Now.Subtract(s);
                 c2++;
             }

             Console.WriteLine($"c1={1.Seconds().TotalMilliseconds/c1}ms, c2={1.Seconds().TotalMilliseconds / c2}ms");
 */

            //FeatureLock reLock = new FeatureLock(true);
            //using (reLock.ForReading())
            //{
            //    using (reLock.ForWriting())
            //    {
            //    }
            //}
            

            //Console.ReadKey();

            int numReader = 1;
            TimeSpan readerSlack = 0.00.Milliseconds();
            int numWriter = 10;
            TimeSpan writerSlack = 0.01.Milliseconds();
            TimeSpan executionTime = 0.0.Milliseconds();
            TimeSpan duration = 1.Seconds();

            Console.WriteLine("WARMUP");

            var classic = new MessageQueueLockTester<object>("ClassicLock", new object(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (obj, action, qc) => { lock (obj) action(); },
                (obj, action, qc) => { lock (obj) action(); });
            Console.WriteLine(classic.Run());

            var sema = new MessageQueueLockTester<SemaphoreSlim>("SemaphoreSlim", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { myLock.Wait(); try { action(); } finally { myLock.Release(); } },
                (myLock, action, qc) => { myLock.Wait(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(sema.Run());

            var FL = new MessageQueueLockTester<FeatureLock>("FeatureLock", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FL.Run());

            var FLPrio = new MessageQueueLockTester<FeatureLock>("FeatureLock Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock((0 + (uint)qc).Clamp<uint>(0,100))) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLPrio.Run());


            var asyncEx = new MessageQueueLockTester<AsyncLock>("AsyncEx", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(asyncEx.Run());

            var FLre = new MessageQueueLockTester<FeatureLock>("FeatureLock RE", new FeatureLock(true), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLre.Run());



            var semaAsync = new MessageQueueAsyncLockTester<SemaphoreSlim>("SemaphoreSlim Async", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } },
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(semaAsync.Run());

            var FLAsync = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsync.Run());

            var FLAsyncPrio = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync((0 + (uint)qc).Clamp<uint>(0, 100))) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsyncPrio.Run());

            var asyncExAsync = new MessageQueueAsyncLockTester<AsyncLock>("AsyncEx Async", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(asyncExAsync.Run());


            Console.WriteLine("TEST 1");
            Console.WriteLine(classic.Run());
            Console.WriteLine(sema.Run());
            Console.WriteLine(FL.Run());
            Console.WriteLine(FLPrio.Run());
            Console.WriteLine(asyncEx.Run());
            Console.WriteLine(FLre.Run());

            Console.WriteLine(semaAsync.Run());
            Console.WriteLine(FLAsync.Run());
            Console.WriteLine(FLAsyncPrio.Run());
            Console.WriteLine(asyncExAsync.Run());


            Console.WriteLine("TEST 2");
            numReader = 1;
            readerSlack = 0.01.Milliseconds();
            numWriter = 1;
            writerSlack = 0.01.Milliseconds();
            executionTime = 0.01.Milliseconds();
            duration = 1.Seconds();


            classic = new MessageQueueLockTester<object>("ClassicLock", new object(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (obj, action, qc) => { lock (obj) action(); },
                (obj, action, qc) => { lock (obj) action(); });
            Console.WriteLine(classic.Run());

            sema = new MessageQueueLockTester<SemaphoreSlim>("SemaphoreSlim", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { myLock.Wait(); try { action(); } finally { myLock.Release(); } },
                (myLock, action, qc) => { myLock.Wait(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(sema.Run());

            FL = new MessageQueueLockTester<FeatureLock>("FeatureLock", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FL.Run());

            FLPrio = new MessageQueueLockTester<FeatureLock>("FeatureLock Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock((0 + (uint)qc).Clamp<uint>(0, 100))) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLPrio.Run());

            asyncEx = new MessageQueueLockTester<AsyncLock>("AsyncEx", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(asyncEx.Run());

            FLre = new MessageQueueLockTester<FeatureLock>("FeatureLock RE", new FeatureLock(true), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLre.Run());

            semaAsync = new MessageQueueAsyncLockTester<SemaphoreSlim>("SemaphoreSlim Async", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } },
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(semaAsync.Run());

            FLAsync = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsync.Run());

            FLAsyncPrio = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync((0 + (uint)qc).Clamp<uint>(0, 100))) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsyncPrio.Run());

            asyncExAsync = new MessageQueueAsyncLockTester<AsyncLock>("AsyncEx Async", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(asyncExAsync.Run());




            Console.ReadKey();

            //FunctionTestRWLock(new RWLock(RWLock.SpinWaitBehaviour.NoSpinning), 3.Seconds(), 4, 4, 0, 0);
            Console.WriteLine("--2,2,2,2--");
            for (int i = 0; i < 5; i++) FunctionTestRWLock(new FeatureLock(), 1.Seconds(), 2, 2, 2, 2);
            /*Console.WriteLine("--0,0,1,1--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 1, 1);
            Console.WriteLine("--0,0,0,4--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 0, 4);
            Console.WriteLine("--0,0,4,0--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 4, 0);
            Console.WriteLine("--0,0,3,1--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 3, 1);
            Console.WriteLine("--0,0,1,2--");
            for(int i = 0; i < 15; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 1, 2);
            Console.WriteLine("--0,0,2,2--");
            for(int i = 0; i < 15; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 2, 2);*/
            Console.WriteLine("----");
            PerformanceTest();
            Console.WriteLine("----");
            //PerformanceTestParallel();

            Console.ReadKey();
        }


        private static void PerformanceTestParallel()
        {
            var duration = 3.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            int numReadLocks = 0;
            int numWriteLocks = 30;

            List<int> dummyList = new List<int>();
            Random rnd = new Random();

            Action workWrite = () =>
            {
                //if(dummyList.Count > 10000) dummyList.Clear();
                //dummyList.Add(dummyList.Count);
                TimeFrame tf = new TimeFrame(0.01.Milliseconds());
                while (!tf.Elapsed) ; 
                //Thread.Sleep(1);
                //Thread.Yield();
            };
            Action workRead = () =>
            {
                //int x;
                //foreach (var d in dummyList) x = d +1;
                TimeFrame tf = new TimeFrame(0.01.Milliseconds());
                while (!tf.Elapsed) ;
                //Thread.Sleep(1);
                //Thread.Yield();
            };
            Action slack = () =>
            {
                /*TimeFrame tf = new TimeFrame(1.0.Milliseconds());
                while (!tf.Elapsed) ;*/
                //Thread.Sleep(1.Milliseconds());
                //Thread.Sleep(1);
                TimeFrame tf = new TimeFrame(0.1.Milliseconds());
                while (!tf.Elapsed) Thread.Yield();
                //Thread.Sleep(1);
                //Thread.Yield();
            };

            /*name = "Overhead";
            Prepare(out gcs);
            //c = RunParallel(new object(), duration, Overhead, numReadLocks, Overhead, numWriteLocks, workRead, workWrite, slack).Sum();
            double overhead = timeFactor / c;
            Console.WriteLine(overhead + " " + (-1) + " " + c + " " + name);
            dummyList.Clear();*/
            double overhead = 0;

            name = "RWLock";
            Prepare(out gcs);
            c = RunParallel(new FeatureLock(), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "RWLock";
            Prepare(out gcs);
            c = RunParallel(new FeatureLock(), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "RWLock Reentrant";
            Prepare(out gcs);
            c = RunParallel(new FeatureLock(true), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "RWLock Async";
            Prepare(out gcs);
            c = RunParallelAsync(new FeatureLock(), duration, RWLockReadAsync, numReadLocks, RWLockWriteAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();


            name = "ClassicLock";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ClassicLock, numReadLocks, ClassicLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            /*
            name = "SpinLock";
            Prepare(out gcs);
            c = RunParallel(new SpinLock(), duration, SpinLock, numReadLocks, SpinLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();
             */

            name = "AsyncEx";
            Prepare(out gcs);
            c = RunParallel(new AsyncLock(), duration, AsyncEx, numReadLocks, AsyncEx, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "AsyncEx Async";
            Prepare(out gcs);
            c = RunParallelAsync(new AsyncLock(), duration, AsyncExAsync, numReadLocks, AsyncExAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "SemaphoreSlim";
            Prepare(out gcs);
            c = RunParallel(new SemaphoreSlim(1, 1), duration, SemaphoreLock, numReadLocks, SemaphoreLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "SemaphoreSlim Async";
            Prepare(out gcs);
            c = RunParallelAsync(new SemaphoreSlim(1, 1), duration, SemaphoreLockAsync, numReadLocks, SemaphoreLockAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "ReaderWriterLockSlim w/o recursion";
            Prepare(out gcs);
            c = RunParallel(new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, ReaderWriterLockRead, numReadLocks, ReaderWriterLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();

            name = "ReaderWriterLockSlim with recursion";
            Prepare(out gcs);
            c = RunParallel(new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), duration, ReaderWriterLockRead, numReadLocks, ReaderWriterLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            dummyList.Clear();
        }

        private static List<long> RunParallelAsync<T>(T lockObj, TimeSpan duration, Func<T, TimeFrame, Action, Action, Task<long>> readLock, int numReadLockThreads, Func<T, TimeFrame, Action, Action, Task<long>> writeLock, int numWriteLockThreads, Action workRead, Action workWrite, Action slack)
        {
            List<long> counts = new List<long>();
            List<long> countsW = new List<long>();
            List<long> countsR = new List<long>();
            List<Task> tasks = new List<Task>();

            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            Box<TimeFrame> timeBox = new Box<TimeFrame>();

            for (int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, timeBox, workRead, slack).Result;
                    Console.WriteLine("R" + c);
                    lock (counts) counts.Add(c);
                    lock (countsR) countsR.Add(c);
                }));
            }

            for (int i = 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
               {
                   starter.Task.Wait();
                   var c = writeLock(lockObj, timeBox, workWrite, slack).Result;
                   Console.WriteLine("W" + c);
                   lock (counts) counts.Add(c);
                   lock (countsW) countsW.Add(c);
               }));
            }

            Thread.Sleep(200);
            timeBox.value = new TimeFrame(duration);
            starter.SetResult(true);
            Task.WhenAll(tasks.ToArray()).Wait();
            //Console.WriteLine("W*R " + (countsR.Min()* countsR.Max() * countsW.Min()*countsW.Max()) / (counts.Sum() * counts.Sum()));
            return counts;
        }

        private static List<long> RunParallel<T>(T lockObj, TimeSpan duration, Func<T, TimeFrame, Action, Action, long> readLock, int numReadLockThreads, Func<T, TimeFrame, Action, Action, long> writeLock, int numWriteLockThreads, Action workRead, Action workWrite, Action slack)
        {
            List<long> counts = new List<long>();
            List<long> countsW = new List<long>();
            List<long> countsR = new List<long>();
            List<Task> tasks = new List<Task>();

            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            Box<TimeFrame> timeBox = new Box<TimeFrame>();

            for (int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, timeBox, workRead, slack);
                    Console.WriteLine("R" + c);
                    lock (counts) counts.Add(c);
                    lock (countsR) countsR.Add(c);
                }));
            }

            for (int i = 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = writeLock(lockObj, timeBox, workWrite, slack);
                    Console.WriteLine("W" + c);
                    lock (counts) counts.Add(c);
                    lock (countsW) countsW.Add(c);
                }));
            }

            Thread.Sleep(200);
            timeBox.value = new TimeFrame(duration);
            starter.SetResult(true);
            Task.WhenAll(tasks.ToArray()).Wait();
            //Console.WriteLine("W*R " + (countsR.Min()* countsR.Max() * countsW.Min()*countsW.Max()) / (counts.Sum() * counts.Sum()));
            return counts;
        }


        private static void PerformanceTest()
        {
            var duration = 0.5.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            Action work = null;
            Action slack = null;

            /*name = "Overhead";
            Prepare(out gcs);
            c = Overhead(new object(), new TimeFrame(duration), work, slack);
            double time_overhead_ns = timeFactor / c;
            Console.WriteLine(time_overhead_ns + " " + -1 + " " + name);*/
            double time_overhead_ns = 0;

            name = "RWLock Read";
            Prepare(out gcs);
            c = RWLockRead(new FeatureLock(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write";
            Prepare(out gcs);
            c = RWLockWrite(new FeatureLock(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Read Reentrant";
            Prepare(out gcs);
            c = RWLockRead(new FeatureLock(true), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write Reentrant";
            Prepare(out gcs);
            c = RWLockWrite(new FeatureLock(true), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Read Async";
            Prepare(out gcs);
            c = RWLockReadAsync(new FeatureLock(), new TimeFrame(duration), work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write Async";
            Prepare(out gcs);
            c = RWLockWriteAsync(new FeatureLock(), new TimeFrame(duration), work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Classic Lock";
            Prepare(out gcs);
            c = ClassicLock(new object(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Monitor";
            Prepare(out gcs);
            c = Monitor(new object(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            /*name = "Mutex";
            Prepare(out gcs);
            c = Mutex(new Mutex(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);
            */

            name = "AsyncEx";
            Prepare(out gcs);
            c = AsyncEx(new AsyncLock(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "AsyncEx Async";
            Prepare(out gcs);
            c = AsyncExAsync(new AsyncLock(), new TimeFrame(duration), work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "SpinLock";
            Prepare(out gcs);
            c = SpinLock(new SpinLock(), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock";
            Prepare(out gcs);
            c = SemaphoreLock(new SemaphoreSlim(1, 1), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock Async";
            Prepare(out gcs);
            c = SemaphoreLockAsync(new SemaphoreSlim(1, 1), new TimeFrame(duration), work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (no recursion)";
            Prepare(out gcs);
            c = ReaderWriterLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (with recursion)";
            Prepare(out gcs);
            c = ReaderWriterLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), new TimeFrame(duration), work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

        }

        private static void Finish(double timeFactor, string name, long c, int gcs, double time_overhead_ns)
        {
            double time = timeFactor / c - time_overhead_ns;
            gcs = (GC.CollectionCount(0) - gcs);
            //long iterationsPerGC = gcs > 0 ? c / gcs : -1;
            double iterationsPerGC = gcs / (((double)c) / 1_000_000);
            Console.WriteLine(time + " " + iterationsPerGC + " " + c + " " + name);
        }

        private static void Prepare(out int gcs)
        {
            gcs = GC.CollectionCount(0);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long ReaderWriterLockWrite(ReaderWriterLockSlim rwLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
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
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long ReaderWriterLockRead(ReaderWriterLockSlim rwLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
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
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long Mutex(Mutex mutex, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
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
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long SpinLock(SpinLock spinLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                bool spinLockTaken = false;
                try
                {
                    spinLock.Enter(ref spinLockTaken);
                    if (spinLockTaken)
                    {
                        c++;
                        work?.Invoke();
                    }
                }
                finally
                {
                    if (spinLockTaken) spinLock.Exit();
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long AsyncEx(AsyncLock asyncLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (asyncLock.Lock())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static async Task<long> AsyncExAsync(AsyncLock asyncLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (await asyncLock.LockAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static async Task<long> SemaphoreLockAsync(SemaphoreSlim sema, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
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
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long SemaphoreLock(SemaphoreSlim sema, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
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
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long Monitor(object obj, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                bool monitorLockTaken = false;
                try
                {
                    System.Threading.Monitor.Enter(obj, ref monitorLockTaken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if (monitorLockTaken)
                        System.Threading.Monitor.Exit(obj);
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long ClassicLock(object obj, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                lock (obj)
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockWrite(FeatureLock myLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (myLock.Lock())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockRead(FeatureLock myLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (myLock.LockReadOnly())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockWriteAsync(FeatureLock myLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (await myLock.LockAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockReadAsync(FeatureLock myLock, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                using (await myLock.LockReadOnlyAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }


        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long Overhead(object dummy, TimeFrame timeFrame, Action work, Action slack)
        {
            long c = 0;
            while (!timeFrame.Elapsed)
            {
                c++;
                work?.Invoke();
                slack?.Invoke();
            }
            return c;
        }

        private static void FunctionTestRWLock(FeatureLock myLock, TimeSpan duration, int numReading, int numWriting, int numReadingAsync, int numWritingAsync)
        {
            List<DateTime> dummyList = new List<DateTime>();
            Action workWrite = () =>
            {
                if (dummyList.Count > 100) dummyList.Clear();
                foreach(var d in dummyList) d.Add(1.Milliseconds());
                dummyList.Add(AppTime.Now);
            };
            Action workRead = () =>
            {
                foreach (var d in dummyList) d.Add(1.Milliseconds());
            };

            var t1 = Task.Run(() => RunParallel(myLock, duration, RWLockRead, numReading, RWLockWrite, numWriting, workRead, workWrite, null));
            var t2 = Task.Run(() => RunParallelAsync(myLock, duration, RWLockReadAsync, numReadingAsync, RWLockWriteAsync, numWritingAsync, workRead, workWrite, null));
            Task.WhenAll(t1, t2).Wait();

            foreach (var c in t1.Result)
            {
                Console.WriteLine(c);
            }
            foreach (var c in t2.Result)
            {
                Console.WriteLine(c);
            }
        }

    }
}
