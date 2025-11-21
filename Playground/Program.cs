using FeatureLoom.Collections;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.MetaDatas;
using FeatureLoom.Scheduling;
using FeatureLoom.Security;
using FeatureLoom.Serialization;
using FeatureLoom.Statemachines;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.TCP;
using FeatureLoom.Time;
using FeatureLoom.Web;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.Identity.Client;
using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        sealed class StringCache
        {
            struct CacheEntry
            {
                public string value;
                public long creationTick; 
                public long accesses;
            }

            Dictionary<ByteSegment, CacheEntry> cache;
            long accessTick = 0;
            bool shrinking = false;

            // Eviction settings
            readonly int maxEntries;
            readonly int shrinkToEntries;

            // Metrics (optional)
            public int Count => cache.Count;
            public long Evictions { get; set; }
            public long ShrinkPasses { get; set; }
            public bool IsShrinking => shrinking;

            public StringCache(int maxEntries = 50_000, double shrinkToRatio = 0.80, int initialCapacity = 0)
            {
                this.maxEntries = Math.Max(1, maxEntries);
                this.shrinkToEntries = Math.Max(1, (int)(this.maxEntries * Math.Clamp(shrinkToRatio, 0.1, 0.95)));
                this.cache = initialCapacity > 0 ? new Dictionary<ByteSegment, CacheEntry>(initialCapacity) : new Dictionary<ByteSegment, CacheEntry>();
                this.offHandCache = initialCapacity > 0 ? new Dictionary<ByteSegment, CacheEntry>(initialCapacity) : new Dictionary<ByteSegment, CacheEntry>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public string GetOrCreate(ref ByteSegment segment)
            {
                segment.EnsureHashCode();

                accessTick++;                
                
                ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(cache, segment, out bool exists);
                if (exists)
                {
                    entry.accesses++;
                    return entry.value;
                }

                string value = segment.ToString();
                entry.value = value;
                entry.creationTick = accessTick;
                entry.accesses = 1;

                if (cache.Count > maxEntries && !shrinking) StartShrinking();
                return value;
            }

            // Score favors frequently used and recently created entries.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            double Score(in CacheEntry e, long currentAccessTick)
            {
                var age = currentAccessTick - e.creationTick;
                if (age <= 0) age = 1;
                return (double)e.accesses / age;
            }

            List<(ByteSegment segment, CacheEntry entry, double score)> sortlist = new ();
            Dictionary<ByteSegment, CacheEntry> offHandCache;

            // Simple full-scan shrink on a background thread with snapshot+swap.
            void StartShrinking()
            {
                if (shrinking) return; // already shrinking

                shrinking = true;
                int toRemove = cache.Count - shrinkToEntries;
                if (toRemove <= 0)
                {
                    shrinking = false; // nothing to do, release shrinking flag
                    return;
                }

                ShrinkPasses++;
                long currentAccessTick = accessTick;

                // Snapshot to sort by score (ascending: worst first)
                foreach (var kv in cache) sortlist.Add((kv.Key, kv.Value, 0));

                Task.Run(() =>
                {
                    offHandCache.Clear();
                    
                    for (int i=0; i< sortlist.Count; i++)
                    {
                        var elem = sortlist[i];
                        elem.score = Score(elem.entry, currentAccessTick);
                        sortlist[i] = elem;
                    }

                    // Sort and keep best entries
                    sortlist.Sort((a, b) =>a.score.CompareTo(b.score));
                    for (int i = sortlist.Count - 1; i >= toRemove; i--)
                    {
                        offHandCache[sortlist[i].segment] = sortlist[i].entry;
                    }
                    sortlist.Clear();
                    Evictions += toRemove;
                    var temp = cache;
                    cache = offHandCache;
                    offHandCache = temp;                    
                    shrinking = false;
                });
            }
        }

        private static async Task Main()
        {

            {
                int numInitSegaments = 50_000;
                int numSegments = 1_000_000;
                int minStringLength = 10;
                int maxStringLength = 50;
                int maxValueChangeStep = 20;
                double relativeCacheSize = 0.5;

                List<ByteSegment> segments = new();
                List<ByteSegment> initSegments = new();
                for (int i = 0; i < numInitSegaments; i ++)
                {
                    ByteSegment segment = new ByteSegment(RandomGenerator.String(RandomGenerator.Int32(minStringLength, maxStringLength)));
                    initSegments.Add(segment);
                }


                List<string> results = new(segments.Count);
                Dictionary<ByteSegment, string> segmentToStringMap1 = new();
                Dictionary<ByteSegment, string> segmentToStringMap2 = new();     
                int cacheSize = (int)(initSegments.Count * relativeCacheSize);
                StringCache stringCache = new StringCache(cacheSize, 0.8, cacheSize);
                var tk = AppTime.TimeKeeper;
                double ms;
                while (true)
                {
                    for (int i = 0; i < initSegments.Count; i+=RandomGenerator.Int32(1, maxValueChangeStep))
                    {
                        if (i > initSegments.Count - 1) break;

                        ByteSegment segment = new ByteSegment(RandomGenerator.String(RandomGenerator.Int32(minStringLength, maxStringLength)));
                        initSegments[i] = segment;
                    }
                    segments.Clear();
                    for (int i = 0; i < numSegments; i++)
                    {                        
                        ByteSegment segment = initSegments[(int)(RandomGenerator.Int32(0, initSegments.Count - 1) * RandomGenerator.Double() * RandomGenerator.Double())];
                        segments.Add(segment);
                    }
                    

                    // 1) Baseline: new string per segment                    
                    results.Clear();
                    //GC.Collect();
                    long allocBefore1 = GC.GetAllocatedBytesForCurrentThread();
                    tk.Restart();
                    foreach (var segment in segments)
                    {
                        results.Add(segment.ToString());
                    }
                    ms = tk.Elapsed.TotalMilliseconds;
                    long allocAfter1 = GC.GetAllocatedBytesForCurrentThread();
                    long totalAlloc1 = allocAfter1 - allocBefore1;
                    double perIterAlloc1 = totalAlloc1 / (double)segments.Count;
                    Console.WriteLine($"new string:\t\t {ms} ms for {segments.Count} items.\t Alloc: {totalAlloc1} B\t total (~{perIterAlloc1:F1} B/iter)");

                    // Snapshot baseline for correctness checks
                    var baseline = results.ToArray();

/*
                    // 2) Cached w/o pre-hash                    
                    results.Clear();
                    GC.Collect();
                    long allocBefore2 = GC.GetTotalAllocatedBytes();
                    tk.Restart();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var segment = segments[i];
                        string str = segmentToStringMap1.GetOrCreate(segment, static seg => seg.ToString(), segment);
                        results.Add(str);
                    }
                    ms = tk.Elapsed.TotalMilliseconds;
                    long allocAfter2 = GC.GetTotalAllocatedBytes();
                    long totalAlloc2 = allocAfter2 - allocBefore2;
                    double perIterAlloc2 = totalAlloc2 / (double)segments.Count;
                    Console.WriteLine($"cached w/o hash:\t {ms} ms for {segments.Count} items.\t Alloc: {totalAlloc2} B\t total (~{perIterAlloc2:F1} B/iter)");
*/
/*
                    // 3) Cached w/ EnsureHashCode in loop (kept for comparison)                    
                    results.Clear();
                    GC.Collect();
                    long allocBefore3 = GC.GetTotalAllocatedBytes();
                    tk.Restart();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var segment = segments[i];
                        segment.EnsureHashCode();
                        string str = segmentToStringMap2.GetOrCreate(segment, static seg => seg.ToString(), segment);
                        results.Add(str);
                    }
                    ms = tk.Elapsed.TotalMilliseconds;
                    long allocAfter3 = GC.GetTotalAllocatedBytes();
                    long totalAlloc3 = allocAfter3 - allocBefore3;
                    double perIterAlloc3 = totalAlloc3 / (double)segments.Count;
                    Console.WriteLine($"cached w/ hash:\t\t {ms} ms for {segments.Count} items.\t Alloc: {totalAlloc3} B\t total (~{perIterAlloc3:F1} B/iter)");
*/
/*
                    // 4) Cache+ with background shrinking
                    stringCache.Evictions = 0;
                    stringCache.ShrinkPasses = 0;
                    results.Clear();
                    //GC.Collect();
                    long allocBefore4 = GC.GetTotalAllocatedBytes();
                    tk.Restart();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var segment = segments[i];
                        var str = stringCache.GetOrCreate(ref segment);                        
                        results.Add(str);
                    }
                    ms = tk.Elapsed.TotalMilliseconds;
                    long allocAfter4 = GC.GetTotalAllocatedBytes();
                    long totalAlloc4 = allocAfter4 - allocBefore4;
                    double perIterAlloc4 = totalAlloc4 / (double)segments.Count;
                    Console.WriteLine($"StringCache:\t\t {ms} ms for {segments.Count} items.\t Alloc: {totalAlloc4} B\t total (~{perIterAlloc4:F1} B/iter)");
                    Console.WriteLine($"Cache entries: {stringCache.Count}, Shrinking: {stringCache.IsShrinking}, Evictions: {stringCache.Evictions}, ShrinkPasses: {stringCache.ShrinkPasses}");
*/
                    await AppTime.WaitAsync(1.Seconds());
                }
            }






            {
                Dictionary<int, string> intToStringMap;

                string jsonIntToStringMap = @"{
                    ""1"": ""One"",
                    ""2"": ""Two"",
                    ""3"": ""Three""
                }";

                FeatureJsonDeserializer des = new FeatureJsonDeserializer();
                bool success = des.TryDeserialize(jsonIntToStringMap, out intToStringMap);

            }
            ValueWrappingQueueReceiver rec = new();

            int numValues = 1000;

            AsyncManualResetEvent syncHandle = new AsyncManualResetEvent(false);

            var readerContext = new SingleThreadSynchronizationContext("reader");
            readerContext.Post(async _ =>
            {
                while (true)
                {
                    if ((await rec.TryReceiveAsync(CancellationToken.None)).TryOut(out object msg))
                    {
                        if (msg is ValueWrapper<int> wrapper)
                        {
                            int value = wrapper.UnwrapAndDispose();
                            Console.WriteLine($"Received wrapped value: {value}");
                            if (value == numValues)
                            {
                                Console.WriteLine($"Finished ValueWrapper test (reader). Global:{SharedPool<ValueWrapper<int>>.GlobalCount} Local:{SharedPool<ValueWrapper<int>>.LocalCount}");
                                syncHandle.Set();
                                break;
                            }                            
                        }
                    }
                }
            }, null);


            var writerContext = new SingleThreadSynchronizationContext("writerContext");
            writerContext.Post(async _ =>
            {
                for (int i = 1; i <= numValues; i++)
                {
                    Console.WriteLine($"Sending wrapped value: {i}");
                    rec.Post(i);                    
                }
                Console.WriteLine($"Finished ValueWrapper test (writer). Global:{SharedPool<ValueWrapper<int>>.GlobalCount} Local:{SharedPool<ValueWrapper<int>>.LocalCount}");
            }, null);

            await syncHandle.WaitingTask;
            

            await JsonTest.Run2();


            {
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
            }

            {

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
            }

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