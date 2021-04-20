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

        static void Main(string[] args)
        {

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
