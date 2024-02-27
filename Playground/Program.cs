using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Scheduling;
using FeatureLoom.Time;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using System.Text;
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
using System.Linq;
using FeatureLoom.MetaDatas;
using FeatureLoom.Statemachines;
using System.Data;
using System.Net.WebSockets;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;

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

        static int dummy = 0;
        static int numIterations = 10_000_000;


    

        public class TestConfig : Configuration
        {
            public string aaa = "Hallo";
            public int bbb = 3;
            public List<int> intList;
            public DateTime dt = DateTime.Now;
            public TestEnum testEnum;

            public TestConfig()
            {
                Uri = "TestConfig";
            }
        }

        public class OuterClass
        {
            public class InnerClass
            {

            }
        }

        class TestClass
        {
            public int a = 1;
            private int p = 42;
            public int P { get => this.p; set => p = value; }
        }

        class TestClass2 : TestClass
        {
            public int b = 2;            
        }

        struct TestStruct
        {
            public string str;
            public int i;
            public TestClass obj;
        }


        private static async Task Main()
        {

            /*
            TestStruct testStruct1 = new TestStruct()
            {
                str = "abc",
                i = 123,
                obj = new TestClass2() { a = 1, b = 99 }
            };

            TestStruct testStruct2 = new TestStruct()
            {
                str = "abc",
                i = 123,
                obj = new TestClass2() { a = 1, b= 100 }
            };



            bool a = testStruct1.EqualsDeep(testStruct1);
            bool b = testStruct1.EqualsDeep(testStruct2);
            bool c1 = testStruct2.EqualsDeep(testStruct2);
            bool d = 1.EqualsDeep(1);
            bool e = 1.EqualsDeep(2);
            TestClass2 t1 = new TestClass2();
            TestClass2 t2 = new TestClass2();
            bool r1 = t1.EqualsDeep(t2);
            t1.P = 43;
            bool r3 = t1.EqualsDeep(t2);
            */

            /*
            IWebServer webserver = new DefaultWebServer();

            Sender serverSender = new Sender();
            Forwarder serverReceiver = new Forwarder();
            webserver.AddWebSocketEndpoint("/websocket", serverSender, serverReceiver, typeof(DateTime));
            _ = webserver.Run(IPAddress.Loopback, 5001);

            ClientWebSocket clientWebSocket = new ClientWebSocket();
            Uri serverUri = new Uri("ws://localhost:5001/websocket");
            bool connected = false;
            while (!connected)
            {
                try
                {
                    Console.WriteLine("Trying to connect...");
                    await clientWebSocket.ConnectAsync(serverUri, CancellationToken.None);
                    connected = true;
                }
                catch
                {
                    Console.WriteLine("Connection attempt faild! Retry...");
                    AppTime.Wait(1.Seconds());
                }
            }

            Console.WriteLine("Connected to server");
            WebSocketEndpoint clientEndpoint = new WebSocketEndpoint(clientWebSocket, typeof(DateTime));

            serverReceiver.ProcessMessage<DateTime>(async msg =>
            {
                Log.FORCE("S:" + msg.ToString());      
                await AppTime.WaitAsync(1.Seconds());
                serverSender.Send(AppTime.Now);
            });
            clientEndpoint.ProcessMessage<DateTime>(async msg =>
            {
                Log.FORCE("C:" + msg.ToString());
                await AppTime.WaitAsync(1.Seconds());
                clientEndpoint.Send(AppTime.Now);
            });
            
            clientEndpoint.Send(AppTime.Now);

            Console.ReadKey();
            */

            /*
            DefaultWebServer webserver = new DefaultWebServer();

            Sender sender = new Sender();
            ProcessingEndpoint<object> printer = new ProcessingEndpoint<object>(msg =>
            {
                if (msg.TryGetMetaData(WebSocketEndpoint.META_DATA_CONNECTION_KEY, out ObjectHandle handle))
                {
                    Console.Write($"{handle}: ");
                }
                Console.WriteLine(msg);
                if (msg is string str) sender.Send(str);
            });

            webserver.AddWebSocketEndpoint("/", sender, printer);

            _ = webserver.Run(IPAddress.Loopback, 5001);



            Console.ReadKey();
            */
            //string typeName = typeof(Dictionary<string, List<List<TestDto>[]>>).GetSimplifiedTypeName();

            //Type resolvedType = TypeHelper.GetTypeFromSimplifiedName(typeName);

            //TestDto orig = new TestDto();


            //orig.Mutate();
            //orig.TryClone(out var clone);

            /*
            object obj = 99;
            Type objType = obj.GetType();
            int iterations = 100_000_000;
            bool dummy = true;

            TimeKeeper tk = AppTime.TimeKeeper;
            for(int i =0; i < iterations; i++)
            {
                dummy = objType.IsEnum;
            }
            Console.WriteLine($"IsEnum: {tk.Elapsed}");
            var x = dummy;


            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                dummy = objType.IsPrimitive;
            }
            Console.WriteLine($"IsPrimitive: {tk.Elapsed}");
            x = dummy;

            Dictionary<object, bool> dict = new Dictionary<object, bool>();
            dict[obj] = true;
            bool isPrimitive = false;
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                dummy = dict[obj];
            }
            Console.WriteLine($"dict: {tk.Elapsed}");
            x = dummy;
            
            


            Console.ReadKey();
            */

            /*
            SlicedBuffer<byte> slicedBuffer = new SlicedBuffer<byte>(1000*1000);

            int iterations = 10_000;            
            RandomGenerator.Reset(123);
            int[] sizes = Enumerable.Range(0, 1000).Select(i => RandomGenerator.Int32(1, 10)).ToArray(); 
            bool[] keep = Enumerable.Range(0, 1000).Select(i => RandomGenerator.Bool(0.5)).ToArray();

            TimeSpan elapsed;
            long beforeCollection;
            long afterCollection;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var tk = AppTime.TimeKeeper;
            while (true)
            {
                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    byte[] buffer;
                    for (int j = 0; j < sizes.Length; j++)
                    {
                        buffer = new byte[sizes[j]];
                        buffer[0] = 1;
                        if (keep[j]) continue;
                        buffer = null;
                    }
                }
                elapsed = tk.Elapsed;
                var elapsed_DUMMY = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"byte[]: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(1.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    slicedBuffer.Reset();
                    SlicedBuffer<byte>.Slice buffer;                    
                    for (int j = 0; j < sizes.Length; j++)
                    {
                        slicedBuffer.TryGetSlice(sizes[j], out buffer);
                        buffer[0] = 1;
                        if (keep[j]) continue;
                        buffer.Dispose();
                    }                    
                }
                elapsed = tk.Elapsed;
                var elapsed_A = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"slice:  {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(1.Seconds());

            }
            */

            Console.ReadKey();
            await JsonTest.Run();

            Log.INFO("InfoTest");
            Log.ERROR("ErrorTest");
            Console.ReadKey();

            Statemachine<Box<int>> statemachine = new Statemachine<Box<int>>(
                ("Starting", async (c, token) =>
                {
                    Console.WriteLine($"Statemachine Starting in 1 second...");
                    await AppTime.WaitAsync(1.Seconds(), token);
                    return "Counting";
                }),
                ("Counting", async (c, token) =>
                {
                    Console.WriteLine($"Statemachine Finishing in {c} seconds...");
                    await AppTime.WaitAsync(1.Seconds(), token);
                    if (token.IsCancellationRequested) return "Starting";
                    c.value--;
                    if (c == 0) return "Ending";
                    return "Counting";
                }),
                ("Ending", async (c, token) =>
                {
                    Console.WriteLine($"Statemachine Finished");
                    return null;
                }));

            TestConfig c = new TestConfig();
            c.bbb = 10;
            CancellationTokenSource cts = new CancellationTokenSource();
            var job = statemachine.CreateJob(10);
            job.UpdateSource.ProcessMessage<IStatemachineJob>(job => Console.WriteLine($"Current State: {job.CurrentStateName} Status: {job.ExecutionState.ToString()}"));            
            statemachine.ForceAsyncRun = false;
            statemachine.StartJob(job, cts.Token);
            Console.WriteLine("--------");
            AppTime.Wait(4.Seconds());
            cts.Cancel();
            AppTime.Wait(1.Seconds());
            Console.WriteLine(job.CurrentStateName);
            Console.WriteLine(job.Context);
            Console.WriteLine(job.ExecutionState.ToString());
            AppTime.Wait(2.Seconds());
            statemachine.ContinueJob(job, CancellationToken.None);
            await job;
            Console.WriteLine(job.CurrentStateName);
            Console.WriteLine(job.Context);
            Console.WriteLine(job.ExecutionState.ToString());



            Console.ReadKey();

            List<int> l = new List<int>(Enumerable.Range(1, 100));            
            _ = Task.Run(() =>
            {
                foreach(int x in l)
                {
                    Console.Write($"{x}, ");
                    Thread.Sleep(100);                    
                }
            });
            Thread.Sleep(1000);
            
            Console.Write($"!!!!!!!!");
            l = null;

            Console.ReadKey();

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

       
        }
    }
}