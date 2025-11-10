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
using System.Reflection.Emit;
using System.Buffers.Text;

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

        interface EmptyInterface
        {

        }

        class TestClass : EmptyInterface
        {
            public int a = 1;
            private int p = 42;
            public int P { get => this.p; set => p = value; }

            public void Inc()
            {
                a++;
            }
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

            public void Inc()
            {
                i++;
            }
        }


        public class XXX
        {
            public NullableStruct? mns;
        }

        public struct NullableStruct
        {
            public int x;
        }

        public class RecordingInfo
        {
             public int dataVersion = 1;
           public DateTime recordingDate;
            public Guid id;
            public string sourceServer = null;
            public string rootPath;
            public List<string> namespaces;
            public TimeSpan length;
            public int samplesCount;
            public string name;
            public string creator;
            public ItemAccessHelper access = new ItemAccessHelper();
            
        }

        public class JsonFragmentTester
        {
            public JsonFragment obj;
        }

        enum Xenum : short
        {
            A,B,C
        }

        public class BaseTest
        {
            [JsonIgnore]
            public int base_publicField = 1;
            [JsonInclude]
            private int base_privateField = 2;

            [JsonIgnore]
            public int Base_publicProperty { get; set; } = 3;
            [JsonInclude]
            private int Base_privateProperty { get; set; } = 4;
        }

        public class MainTest: BaseTest
        {
            [JsonIgnore]
            public int main_publicField = 11;
            [JsonInclude]
            private int main_privateField = 22;
            [JsonIgnore]
            public int Main_publicProperty { get; set; } = 33;
            [JsonInclude]
            private int Main_privateProperty { get; set; } = 44;
        }

        private static async Task Main()
        {

            await JsonTest.Run2();

            var tk = AppTime.TimeKeeper;
            numIterations = 10_000_000;
            Xenum enumValue = Xenum.B;

            while (true)
            {
                tk.Restart();
                for (int i = 0; i < numIterations; i++)
                {
                    int intValue = Convert.ToInt32(enumValue);
                }
                Console.WriteLine($"Convert.ToInt32: {tk.Elapsed.TotalMilliseconds} ms for {numIterations} iterations.");

                tk.Restart();
                for (int i = 0; i < numIterations; i++)
                {
                    int intValue = (int)enumValue;
                }
                Console.WriteLine($"(int) cast: {tk.Elapsed.TotalMilliseconds} ms for {numIterations} iterations.");

                tk.Restart();
                for (int i = 0; i < numIterations; i++)
                {
                    int intValue = Unsafe.As<Xenum, int>(ref enumValue);
                }
                Console.WriteLine($"Unsafe.As<Xenum, int>: {tk.Elapsed.TotalMilliseconds} ms for {numIterations} iterations.");

                tk.Restart();
                for (int i = 0; i < numIterations; i++)
                {
                    int intValue = EqualityComparer<Xenum>.Default.GetHashCode();
                }
                Console.WriteLine($"EqualityComparer<Xenum>.Default.GetHashCode: {tk.Elapsed.TotalMilliseconds} ms for {numIterations} iterations.");
            }


            byte[] bytes = RandomGenerator.Bytes(30);
            FeatureJsonSerializer serializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings()
            {
                indent = true,
                dataSelection = FeatureJsonSerializer.DataSelection.PublicFieldsAndProperties,
                writeByteArrayAsBase64String = false
            });
            var bytesJson = serializer.Serialize(bytes);
            JsonHelper.DefaultDeserializer.TryDeserialize<byte[]>(bytesJson, out var bytesOut);


            TestStruct ts = new TestStruct()
            {
                i = 42,
                obj = new TestClass()
            };

            var j = JsonHelper.DefaultSerializer.Serialize(ts);

            JsonHelper.DefaultDeserializer.TryDeserialize<JsonFragmentTester>(j, out var jft);            

            JsonHelper.DefaultDeserializer.TryDeserialize<TestClass>(jft.obj.JsonString, out var tc);

            FeatureJsonDeserializer des = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings()
            {
                initialBufferSize = 10, 
            });

            Stream stream = "xxxxxxxxaaa123".ToStream();
            des.SetDataSource(stream);
            des.SkipBufferUntil("aaa", true, out bool found);
            des.TryDeserialize(out int x);

            string t1 = "";
            string result154 = JsonHelper.DefaultSerializer.Serialize(t1);


            bool success = JsonHelper.DefaultDeserializer.TryDeserialize<Xenum?>("1", out var t2);


            Log.DefaultConsoleLogger.config.loglevel = Loglevel.TRACE;
            Log.DefaultConsoleLogger.config.format = "";
            var OptLog = Service<OptLogService>.Instance;
            var settings = new OptLogService.Settings()
            {
                globalLogLevel = Loglevel.INFO,
            };
            /*settings.blackListFilterSettings.Add(new OptLogService.LogFilterSettings()
            {
                sourceFileMask = "*Program.cs",
                methodMask = "Main",
                minloglevel = Loglevel.CRITICAL,
                maxloglevel = Loglevel.CRITICAL,
            });
            */
            OptLog.ApplySettings(settings);

            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex) 
            {

                OptLog.IMPORTANT()?.Build("Log this");
                OptLog.CRITICAL()?.Build("Log this", ex);
                OptLog.ERROR()?.Build("Log this");
                OptLog.WARNING()?.Build("Log this");
                OptLog.INFO()?.Build("Log this", ex);
                OptLog.DEBUG()?.Build("Log this");
                OptLog.TRACE()?.Build("Log this");
            }
            

            await AppTime.WaitAsync(1.Hours());


            /*
            var batchTK = AppTime.TimeKeeper;
            Batcher<int> batcher = new Batcher<int>(5, 1.Seconds(), 100.Milliseconds());
            batcher.ProcessMessage<int[]>(batch => ConsoleHelper.WriteLine($"time: {batchTK.Elapsed.TotalSeconds} num elements: {batch.Length}, Elements: [{batch.AllItemsToString(",")}]"));

            for(int i = 0; i < 100; i++)
            {
                batcher.Send(i);
                await AppTime.WaitAsync(RandomGenerator.Int32(100, 500).Milliseconds());
            }

            await AppTime.WaitAsync(10.Minutes());

            string inputText = "That is a test!";
            var inputBytes = inputText.ToByteArray();
            byte[] output1 = new byte[inputBytes.Length * 2];            
            Base64.EncodeToUtf8(inputBytes, output1, out int bytesConsumed, out int bytesWritten, true);


            MemoryStream outputStream = new MemoryStream();
            var base64Stream = new Base64EncodingStream(outputStream);            
            base64Stream.Write(inputBytes, 0, inputBytes.Length);
            base64Stream.Flush();
            var output2 = outputStream.ToArray();
            */
            /*var rw = new TextFileStorage("pathTest", new TextFileStorage.Config()
            {
                basePath = "./pathTestBasePath",
                fileSuffix = "/bla.json",
                useCategoryFolder = true
            });

            //await rw.TryWriteAsync("myUri", "Content");
            (await rw.TryReadAsync<string>("myUri")).TryOut(out var content);

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

            TcpClientEndpoint client = new TcpClientEndpoint(null, true,
                                                               () => new VariantStreamReader(null, new TypedJsonMessageStreamReader()),
                                                               () => new VariantStreamWriter(null, new TypedJsonMessageStreamWriter()));

            TcpServerEndpoint server = new TcpServerEndpoint(null, true,
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