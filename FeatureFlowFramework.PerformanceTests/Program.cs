using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;

namespace FeatureLoom.PerformanceTests
{
    class Program
    {
        static void Main(string[] args)
        {
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b010101010101;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b101010101010;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b000000111111;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0b000000000101;
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
        }
    }
}
