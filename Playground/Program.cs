using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Scheduling;
using FeatureLoom.Time;
using FeatureLoom.Extensions;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using System.Text;
using FeatureLoom.Workflows;
using FeatureLoom.Web;
using System.Net;
using FeatureLoom.Storages;
using FeatureLoom.Security;
using System.IO;
using System.Globalization;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Serialization;
using FeatureLoom.TCP;
using System.Runtime.CompilerServices;
using FeatureLoom.TCP;

namespace Playground
{
    public static class SocketExtensions
    {
        public static void SetBlocking(this Socket socket, bool blocking)
        {
            if (socket != null) socket.Blocking = blocking;
        }
    }


    public class TestWF : Workflow<TestWF.SM>
    {
        int iterations = 10_000_000;
        int currentIteration = -1;

        public class SM : StateMachine<TestWF>
        {
            protected override void Init()
            {
                var run = State("Run");

                run.Build()
                    .Step()
                        .Do(c => { var x = c.iterations; })
                    .Step()
                        .Do(c => c.currentIteration++)                                        
                    .Step()
                        .If(c => c.currentIteration < c.iterations)
                            .Loop()
                        .Else()
                            .Finish();
            }
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

        static int dummy = 0;
        static int numIterations = 10_000_000;


        public static void TestSync()
        {
            var runner = new BlockingRunner();
            var tk = AppTime.TimeKeeper;            
            runner.RunAsync(new TestWF()).WaitFor();
            Console.WriteLine($"TestSync: {tk.Elapsed.TotalMilliseconds}");
        }

        public static async Task TestAsync()
        {
            var runner = new AsyncRunner();
            var tk = AppTime.TimeKeeper;
            await runner.RunAsync(new TestWF());
            Console.WriteLine($"TestAsync: {tk.Elapsed.TotalMilliseconds}");
        }

        public static async Task TestSmartAsync()
        {
            var runner = new SmartRunner();
            var tk = AppTime.TimeKeeper;
            await runner.RunAsync(new TestWF());
            Console.WriteLine($"TestSmartAsync: {tk.Elapsed.TotalMilliseconds}");
        }

        public class TestConfig : Configuration
        {
            public string aaa = "Hallo";
            public int bbb = 99;
        }

        

        private static async Task Main()
        {
            TcpClientEndpoint2 client = new TcpClientEndpoint2(null, true,
                                                               () => new VariantStreamReader(null, new TypedJsonMessageStreamReader()),
                                                               () => new VariantStreamWriter(null, new TypedJsonMessageStreamWriter()));

            TcpServerEndpoint2 server = new TcpServerEndpoint2(null, true,
                                                               () => new VariantStreamReader(null, new TypedJsonMessageStreamReader()),
                                                               () => new VariantStreamWriter(null, new TypedJsonMessageStreamWriter()));

            client.ConnectionWaitHandle.Wait();
            server.ProcessMessage<object>(msg =>
            {
                var x = msg;
            });

            client.Send(new TestConfig());            
            client.Send(new TestConfig() { aaa = "XX", bbb = 123 });
            
            client.Send(new TestConfig() { aaa = "XdsfX", bbb = 1233 });
            
            client.Send(new TestConfig() { aaa = "XdsfX123", bbb = 12332 });

            Console.ReadKey();

            var amre = new FeatureLoom.Synchronization.AsyncManualResetEvent(false);
            var amre2 = new Nito.AsyncEx.AsyncManualResetEvent(false);
            var mre = new ManualResetEvent(false);
            var mres = new ManualResetEventSlim(false);
            TimeKeeper tk1 = AppTime.TimeKeeper;
            int itertions = 1_000_000;
            bool running = false;
            int waitCounter = 0;
            while(true)
            {
                Thread.Sleep(1000);
                running = true;
                waitCounter = 0;
                Task task = Task.Run(() => 
                { 
                    while (running)
                    { 
                        mre.WaitOne();
                        waitCounter++;
                        //Thread.Yield();
                    }
                });
                Thread.Sleep(1000);
                tk1.Restart();
                for (int i=0; i < itertions; i++)
                {                    
                    mre.Set();
                    mre.Reset();                    
                }
                running = false;
                Console.WriteLine($"  MRE: {tk1.Elapsed.TotalMilliseconds} ms //\t waitCounter: {waitCounter}, {waitCounter/tk1.LastElapsed.Milliseconds}");

                Thread.Sleep(1000);
                running = true;
                waitCounter = 0;
                task = Task.Run(() =>
                {
                    while (running)
                    {
                        mres.Wait();
                        waitCounter++;
                        //Thread.Yield();
                    }
                });
                Thread.Sleep(1000);
                tk1.Restart();
                for (int i = 0; i < itertions; i++)
                {
                    mres.Set();
                    mres.Reset();
                }
                running = false;
                Console.WriteLine($" MRES: {tk1.Elapsed.TotalMilliseconds} ms //\t waitCounter: {waitCounter}, {waitCounter / tk1.LastElapsed.Milliseconds}");

                Thread.Sleep(1000);
                running = true;
                waitCounter = 0;
                task = Task.Run(async () =>
                {
                    while (running)
                    {
                        await amre.WaitAsync();
                        waitCounter++;
                        //Thread.Yield();
                    }
                });
                Thread.Sleep(1000);
                tk1.Restart();
                for (int i = 0; i < itertions; i++)
                {
                    amre.Set();
                    amre.Reset();
                }
                running = false;
                Console.WriteLine($" AMRE: {tk1.Elapsed.TotalMilliseconds} ms //\t waitCounter: {waitCounter}, {waitCounter / tk1.LastElapsed.Milliseconds}");

                Thread.Sleep(1000);
                running = true;
                waitCounter = 0;
                task = Task.Run(async () =>
                {
                    while (running)
                    {
                        await amre2.WaitAsync();
                        waitCounter++;
                        //Thread.Yield();
                    }
                });
                Thread.Sleep(1000);
                tk1.Restart();
                for (int i = 0; i < itertions; i++)
                {
                    amre2.Set();
                    amre2.Reset();
                }
                running = false;
                Console.WriteLine($"AMRE2: {tk1.Elapsed.TotalMilliseconds} ms //\t waitCounter: {waitCounter}, {waitCounter / tk1.LastElapsed.Milliseconds}");
                Console.WriteLine("----------");
            }

            ulong asd = ulong.MaxValue;
            long unix = (long)asd.ClampHigh((ulong)long.MaxValue);


            Service<DefaultWebServer>.Instance.MapStorage<string>("/files", "wwwRoot");
            /*

            var writer = Storage.GetWriter("test");
            var reader = Storage.GetReader("test");

            CultureInfo culture =  Thread.CurrentThread.CurrentCulture.CloneAndCast<CultureInfo>();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = culture;
            string path = "Project/Hallo/IntProp=123/pups=12.2";
            if (path.TryExtract(0, "Project/","/", out string projectName, out int startIndex, true) &&
                path.TryExtract(startIndex, "IntProp=", "/", out int intProp, out startIndex) &&
                path.TryExtract(startIndex, "/pups=", "", out float pups, out startIndex))
            {
                Console.WriteLine($"Project: {projectName} ,Prop: {intProp} ({intProp.GetType()})");
            }

            Forwarder sender = new Forwarder();


            sender
                .ConvertMessage<int,string>(i => i.ToString())
                .FilterMessage<string>(s => !s.EmptyOrNull())
                .ProcessMessage<string>(s => Console.WriteLine(s));

            sender.Send("abc");
            sender.Send(123);
            sender.Send("");


            ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();

            
            Session.CleanUpAsync().WaitFor();
            IdentityRole guest = new IdentityRole("Guest", new string[] { "GuestThings" });
            guest.TryStoreAsync().WaitFor();
            IdentityRole admin = new IdentityRole("Admin", new string[] { "GuestThings", "AdminThings", "ManageIdentities" });
            admin.TryStoreAsync().WaitFor();                

            Identity mig = new Identity("MiG", credentialHandler.GenerateStoredCredential(new UsernamePassword("MiG", "1234")));                
            mig.AddRole(admin);
            mig.TryStoreAsync().WaitFor();
            Identity paul = new Identity("Paul", credentialHandler.GenerateStoredCredential(new UsernamePassword("Paul", "abc")));                
            paul.AddRole(guest);
            paul.TryStoreAsync().WaitFor();

            Service<DefaultWebServer>.Init(() => new DefaultWebServer());
            var webServer = Service<IWebServer>.Instance;

            webServer.AddRequestInterceptor(new SessionCookieInterceptor());

            webServer.AddRequestHandler(new UsernamePasswordSignupHandler() { defaultRole = guest });
            webServer.AddRequestHandler(new UsernamePasswordLoginHandler());
            webServer.AddRequestHandler(new LogoutHandler());
            webServer.AddRequestHandler(new IdentityAndAccessManagementHandler());

            webServer.HandleGET("/customers/{1}/{2}", (string name, int num) =>
            {
                string result = "";
                for (int i = 0; i < num; i++) result += name;
                return HandlerResult.Handled_OK(result);
            });

            webServer.HandleGET("/throw", (IWebRequest req) =>
            {
                (req as IWebServer).Run().WaitFor();
                return HandlerResult.Handled_OK();
            }).HandleException((NullReferenceException e) => HandlerResult.Handled_Conflict(e.ToString()));

            webServer.HandlePOST("/customers/{name}", (string name) =>
            {
                return HandlerResult.Handled_OK(name +"POSTED");
            });

            webServer.HandleGET("/test", (req, resp) =>
            {
                return HandlerResult.Handled_OK(Session.Current.LifeTime.Remaining().ToString());
            }).CheckMatchesPermission("Guest*");

            webServer.HandleGET("/test/xx", (req, resp) =>
            {
                if (Session.Current?.Identity?.HasPermission("GuestThings") ?? false)
                {
                    return HandlerResult.Handled_OK("xx");
                }
                else
                {                    
                    return HandlerResult.Handled_Forbidden();
                }
            }).CheckHasPermission("GuestThings");


            webServer.AddRequestHandler(
                new RequestHandlerPermissionWrapper(
                    new StorageWebAccess<string>("/config", new StorageWebAccess<string>.Config() { category = "config" }),
                    "AdminThings", true));

            webServer.HandleException((NullReferenceException e) =>
            {
                return HandlerResult.Handled_InternalServerError();
            });

            webServer.HandleResult((HandlerResult result) =>
            {
                if (result.statusCode == HttpStatusCode.BadRequest) Log.WARNING(HttpStatusCode.BadRequest.ToString());
            });

            _ = webServer.Run(IPAddress.Loopback, 50123);

            Console.ReadKey();            
            */
            while (true)
            {
                TestSync();
                TestAsync().WaitFor();
                TestSmartAsync().WaitFor();
                Console.WriteLine("----");
            }

            Console.ReadKey();


            while (true)
            {
                var tk = AppTime.TimeKeeper;
                for(int i=0; i <100_000; i++)
                {
                    var x = AppTime.CoarseNow;
                }
                long coarseTicks = tk.Elapsed.Ticks;
                tk.Restart();
                for (int i = 0; i < 100_000; i++)
                {
                    var x = DateTime.UtcNow;
                }
                long nowTicks = tk.Elapsed.Ticks;
                Console.WriteLine($"CoarseNow to Now: {(coarseTicks * 100.0)/nowTicks}%");
                
                
                Console.WriteLine($"CoarseNow Diff: {(DateTime.UtcNow-AppTime.CoarseNow).TotalMilliseconds}ms");                
                Thread.Sleep(RandomGenerator.Int32(500,1000));
            }


            Console.ReadLine();


            object A = new object();
            object B = new object();
            object C = new object();

            Console.WriteLine("Thread1 attempts lock A");
            using(LockOrderDeadlockResolver.Lock(A))
            {
                Console.WriteLine("Thread1 took lock A");

                _ = Task.Run(() =>
                {
                    Console.WriteLine("Thread2 attempts lock B");
                    using (LockOrderDeadlockResolver.Lock(B))
                    {
                        Console.WriteLine("Thread2 took lock B");

                        _ = Task.Run(() =>
                        {
                            Console.WriteLine("Thread3 attempts lock C");
                            using (LockOrderDeadlockResolver.Lock(C))
                            {
                                Console.WriteLine("Thread3 took lock C");

                                Console.WriteLine("Thread3 attempts lock A - blocked");
                                using (LockOrderDeadlockResolver.Lock(A))
                                {
                                    Console.WriteLine("Thread3 took lock A");
                                }
                                Console.WriteLine("Thread3 releases lock A");
                            }
                            Console.WriteLine("Thread3 releases lock C");
                        });

                        Thread.Sleep(100);

                        Console.WriteLine("Thread2 attempts lock C - blocked");
                        using (LockOrderDeadlockResolver.Lock(C))
                        {
                            Console.WriteLine("Thread2 took lock C");
                        }
                        Console.WriteLine("Thread2 releases lock C");
                    }
                    Console.WriteLine("Thread2 releases lock B");
                });

                Thread.Sleep(200);

                Console.WriteLine("Thread1 attempts lock B");
                using (LockOrderDeadlockResolver.Lock(B))
                {
                    Console.WriteLine("Thread1 took lock B - borrowed");
                }
                Console.WriteLine("Thread1 releases lock B");
            }
            Console.WriteLine("Thread1 releases lock A");


            Console.ReadLine();




            int ex = 10_000;

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