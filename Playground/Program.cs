using FeatureLoom.Collections;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
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
using System.Runtime.Serialization;
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

    public abstract class TestBase
    {
        public abstract V Take<V>();
    }

    public class TestClass<T> : TestBase
    {
        public T value;

        public override V Take<V>()
        {
            if (value is V v) return v;
            else throw new InvalidCastException($"Cannot cast value of type {typeof(T).FullName} to {typeof(V).FullName}");
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


    

        public enum TestEnum
        {
            A,
            B,
            C
        }

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

        public struct Sample
        {
            public object value;
            public DateTime sourceTimestamp;
            public DateTime serverTimestamp;
        }

        public class SampleMessage
        {
            /// <summary>
            /// The name of the device.
            /// </summary>
            public string deviceName;
            /// <summary>
            /// The node ID the sample belongs to.
            /// </summary>
            public string nodeId;
            /// <summary>
            /// The browse paths to the node.
            /// </summary>
            public string[] browsePaths;
            /// <summary>
            /// The namespace name of the node.
            /// </summary>
            public string nsName;
            /// <summary>
            /// The sample value and timestamps.
            /// </summary>
            public Sample sample;
        }

    }
}