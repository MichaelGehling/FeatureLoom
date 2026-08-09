using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System.Linq;

namespace FeatureLoom.PerformanceTests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b010101010101;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b101010101010;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b000000111111;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b000000000101;

            // A config passed to Run() takes precedence over class level job attributes, so a
            // hard coded DebugInProcessConfig would force every benchmark in process and prevent
            // the allocation diagnoser from attaching. Using the default config lets each
            // benchmark class choose its own job, while "--inProcess" still restores the previous
            // in-process behaviour for debugging sessions.
            IConfig config = args.Contains("--inProcess") ? new DebugInProcessConfig() : DefaultConfig.Instance;

            JsonSerializer.SampleOutput.Reset();
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

            JsonSerializer.SampleOutput.PrintAll();
        }
    }
}