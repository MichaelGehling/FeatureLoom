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
using System.Linq;
using FeatureLoom.MetaDatas;

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