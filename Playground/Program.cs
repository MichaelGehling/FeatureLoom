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


              //  Console.ReadKey();



            int numReader = 100;
            TimeSpan readerSlack = 0.0.Milliseconds();
            int numWriter = 100;
            TimeSpan writerSlack = 0.0.Milliseconds();
            TimeSpan executionTime = 0.0.Milliseconds();
            TimeSpan duration = 3.Seconds();

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

            var FLAsync = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsync.Run());

            var FLPrio = new MessageQueueLockTester<FeatureLock>("FeatureLock Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock(FeatureLock.DEFAULT_PRIORITY + qc)) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLPrio.Run());

            var FLAsyncPrio = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async Prio", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync(FeatureLock.DEFAULT_PRIORITY + qc)) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsyncPrio.Run());


            var asyncEx = new MessageQueueLockTester<AsyncLock>("AsyncEx", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(asyncEx.Run());

            var FLre = new MessageQueueLockTester<FeatureLock>("FeatureLock RE", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLre.Run());

            var NeoSmart = new MessageQueueLockTester<NeoSmart.AsyncLock.AsyncLock>("NeoSmart", new NeoSmart.AsyncLock.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(NeoSmart.Run());


            var semaAsync = new MessageQueueAsyncLockTester<SemaphoreSlim>("SemaphoreSlim Async", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } },
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(semaAsync.Run());


            var asyncExAsync = new MessageQueueAsyncLockTester<AsyncLock>("AsyncEx Async", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(asyncExAsync.Run());

            var BmbsqdAsync = new MessageQueueAsyncLockTester<Bmbsqd.Async.AsyncLock>("Bmbsqd Async", new Bmbsqd.Async.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock) action(); },
                async (myLock, action, qc) => { using(await myLock) action(); });
            Console.WriteLine(BmbsqdAsync.Run());

            var neoAsync = new MessageQueueAsyncLockTester<NeoSmart.AsyncLock.AsyncLock>("NeoSmart Async", new NeoSmart.AsyncLock.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(neoAsync.Run());


            Console.WriteLine("TEST FastPath");

            duration = 1.Seconds();

            var FP_NoLock = new FastPathLockTester<object>("FP_NoLock", new object(), duration, null,
                 (myLock) => {  });
            var offset = FP_NoLock.Run();

            var FP_FL = new FastPathLockTester<FeatureLock>("FP_FeatureLock", new FeatureLock(), duration, offset,
                 (myLock) => { using(myLock.Lock()) { } });
            Console.WriteLine(FP_FL.Run());

            var FP_FLRO = new FastPathLockTester<FeatureLock>("FP_FeatureLock Read", new FeatureLock(), duration, offset,
                 (myLock) => { using(myLock.LockReadOnly()) { } });
            Console.WriteLine(FP_FLRO.Run());

            var FP_FLRE = new FastPathLockTester<FeatureLock>("FP_FeatureLock RE", new FeatureLock(), duration, offset,
                 (myLock) => { using(myLock.Lock()) { } });
            Console.WriteLine(FP_FLRE.Run());

            var FP_classic= new FastPathLockTester<object>("FP_ClassicLock", new object(), duration, offset,
                 (myLock) => { lock(myLock) { } });
            Console.WriteLine(FP_classic.Run());

            var FP_sema = new FastPathLockTester<SemaphoreSlim>("FP_SemaphoreSlim", new SemaphoreSlim(1,1), duration, offset,
                 (myLock) => { myLock.Wait(); try {  } finally { myLock.Release(); } });
            Console.WriteLine(FP_sema.Run());

            var FP_asyncEx = new FastPathLockTester<AsyncLock>("FP_AsyncEx", new AsyncLock(), duration, offset,
                 (myLock) => { using(myLock.Lock()) { } });
            Console.WriteLine(FP_asyncEx.Run());

            var FP_rw = new FastPathLockTester<ReaderWriterLockSlim>("FP_ReaderWriterLockSlim", new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, offset,
                  (myLock) => { myLock.EnterWriteLock(); try { } finally { myLock.ExitWriteLock(); } });
            Console.WriteLine(FP_rw.Run());

            var FP_rwRO = new FastPathLockTester<ReaderWriterLockSlim>("FP_ReaderWriterLockSlim Read", new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, offset,
                  (myLock) => { myLock.EnterReadLock(); try { } finally { myLock.ExitReadLock(); } });
            Console.WriteLine(FP_rwRO.Run());

            var FP_rwRE = new FastPathLockTester<ReaderWriterLockSlim>("FP_ReaderWriterLockSlim RE", new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), duration, offset,
                  (myLock) => { myLock.EnterWriteLock(); try { } finally { myLock.ExitWriteLock(); } });
            Console.WriteLine(FP_rwRE.Run());

            var FP_spin = new FastPathLockTester<SpinLock>("FP_Spinlock", new SpinLock(), duration, offset,
                  (myLock) => { bool lockTaken = false;  myLock.Enter(ref lockTaken); try { } finally { myLock.Exit(true); } });
            Console.WriteLine(FP_spin.Run());

            var FP_neo = new FastPathLockTester<NeoSmart.AsyncLock.AsyncLock>("FP_NeoSmart", new NeoSmart.AsyncLock.AsyncLock(), duration, offset,
                  (myLock) => { using(myLock.Lock()) { } });
            Console.WriteLine(FP_neo.Run());



            var FP_NoLock_Async = new FastPathAsyncLockTester<object>("FP_NoLock", new object(), duration, null,
                 async (myLock) => { });
            var offsetAsync = FP_NoLock_Async.Run();

            var FP_FL_Async = new FastPathAsyncLockTester<FeatureLock>("FP_FeatureLock Async", new FeatureLock(), duration, offsetAsync,
                 async (myLock) => { using (await myLock.LockAsync()) { } });
            Console.WriteLine(FP_FL_Async.Run());

            var FP_FLRO_Async = new FastPathAsyncLockTester<FeatureLock>("FP_FeatureLock Read Async", new FeatureLock(), duration, offsetAsync,
                 async (myLock) => { using (await myLock.LockReadOnlyAsync()) { } });
            Console.WriteLine(FP_FLRO_Async.Run());
        
            var FP_FLRE_Async = new FastPathAsyncLockTester<FeatureLock>("FP_FeatureLock Async RE", new FeatureLock(), duration, offsetAsync,
                 async (myLock) => { using(await myLock.LockAsync()) { } });
            Console.WriteLine(FP_FLRE_Async.Run());

            var FP_sema_Async = new FastPathAsyncLockTester<SemaphoreSlim>("FP_SemaphoreSlim Async", new SemaphoreSlim(1, 1), duration, offsetAsync,
                 async (myLock) => { await myLock.WaitAsync(); try { } finally { myLock.Release(); } });
            Console.WriteLine(FP_sema_Async.Run());

            var FP_asyncEx_Async = new FastPathLockTester<AsyncLock>("FP_AsyncEx Async", new AsyncLock(), duration, offsetAsync,
                 async (myLock) => { using(await myLock.LockAsync()) { } });
            Console.WriteLine(FP_asyncEx_Async.Run());

            var FP_Bmbsqd_Async = new FastPathLockTester<Bmbsqd.Async.AsyncLock>("FP_Bmbsqd Async", new Bmbsqd.Async.AsyncLock(), duration, offsetAsync,
                 async (myLock) => { using(await myLock) { } });
            Console.WriteLine(FP_Bmbsqd_Async.Run());

            var FP_neo_Async = new FastPathLockTester<NeoSmart.AsyncLock.AsyncLock>("FP_NeoSmart Async", new NeoSmart.AsyncLock.AsyncLock(), duration, offsetAsync,
                 async (myLock) => { using(await myLock.LockAsync()) { } });
            Console.WriteLine(FP_neo_Async.Run());



            Console.WriteLine("TEST 1");
            Console.WriteLine(classic.Run());
            Console.WriteLine(sema.Run());
            Console.WriteLine(FL.Run());
            Console.WriteLine(FLPrio.Run());
            Console.WriteLine(asyncEx.Run());
            Console.WriteLine(FLre.Run());
            Console.WriteLine(NeoSmart.Run());

            Console.WriteLine(semaAsync.Run());
            Console.WriteLine(FLAsync.Run());
            Console.WriteLine(FLAsyncPrio.Run());
            Console.WriteLine(asyncExAsync.Run());
            Console.WriteLine(BmbsqdAsync.Run());
            Console.WriteLine(neoAsync.Run());


            Console.WriteLine("TEST 2");
            numReader = 1;
            readerSlack = 0.00.Milliseconds();
            numWriter = 1;
            writerSlack = 0.00.Milliseconds();
            executionTime = 0.00.Milliseconds();
            duration = 3.Seconds();


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

        /*    asyncEx = new MessageQueueLockTester<AsyncLock>("AsyncEx", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(asyncEx.Run());

            FLre = new MessageQueueLockTester<FeatureLock>("FeatureLock RE", new FeatureLock(true), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using (myLock.Lock()) action(); },
                (myLock, action, qc) => { using (myLock.Lock()) action(); });
            Console.WriteLine(FLre.Run());

            NeoSmart = new MessageQueueLockTester<NeoSmart.AsyncLock.AsyncLock>("NeoSmart", new NeoSmart.AsyncLock.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                (myLock, action, qc) => { using(myLock.Lock()) action(); },
                (myLock, action, qc) => { using(myLock.Lock()) action(); });
            Console.WriteLine(NeoSmart.Run());
            */


            semaAsync = new MessageQueueAsyncLockTester<SemaphoreSlim>("SemaphoreSlim Async", new SemaphoreSlim(1, 1), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } },
                async (myLock, action, qc) => { await myLock.WaitAsync(); try { action(); } finally { myLock.Release(); } });
            Console.WriteLine(semaAsync.Run());

            FLAsync = new MessageQueueAsyncLockTester<FeatureLock>("FeatureLock Async", new FeatureLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using(await myLock.LockAsync()) action(); });
            Console.WriteLine(FLAsync.Run());

            asyncExAsync = new MessageQueueAsyncLockTester<AsyncLock>("AsyncEx Async", new AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(asyncExAsync.Run());

            BmbsqdAsync = new MessageQueueAsyncLockTester<Bmbsqd.Async.AsyncLock>("Bmbsqd Async", new Bmbsqd.Async.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock) action(); },
                async (myLock, action, qc) => { using (await myLock) action(); });
            Console.WriteLine(BmbsqdAsync.Run());

            neoAsync = new MessageQueueAsyncLockTester<NeoSmart.AsyncLock.AsyncLock>("NeoSmart Async", new NeoSmart.AsyncLock.AsyncLock(), numReader, numWriter, duration, readerSlack, writerSlack, executionTime,
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); },
                async (myLock, action, qc) => { using (await myLock.LockAsync()) action(); });
            Console.WriteLine(neoAsync.Run());



            Console.ReadKey();
        }



    }
}
