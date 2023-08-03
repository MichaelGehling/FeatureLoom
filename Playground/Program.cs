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
        }

        

        private static async Task Main()
        {
            TestDto orig = new TestDto();
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