using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Microsoft.VisualBasic;
using FeatureLoom.Workflows;
using Microsoft.Extensions.Primitives;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System.Reflection.Metadata;
using System.IO;
using FeatureLoom.TCP;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json.Serialization;
using System.Collections;
using System.Linq;

namespace Playground
{
    public class BaseDto
    {
        public double privBase = 0.77;
        public int protBase = 1;
        public int pubBase = 2;

        public virtual void Mutate()
        {
            privBase = privBase * 2;
            protBase = protBase * 2;
            pubBase = pubBase * 2;
        }
    }

    public class TestDto : BaseDto
    {
        public TestEnum testEnum = TestEnum.TestB;
        public object self;
        //private int privInt = 42;
        public int myInt = 123;
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public string myString = "Hello: \\, \", \\, \n";
        public MyEmbedded1 myEmbedded1 = new MyEmbedded1();
        public MyEmbedded2 myEmbedded2 = new MyEmbedded2();
        public MyEmbedded1 myEmbedded1a = new MyEmbedded1();
        public MyEmbedded2 myEmbedded2a = new MyEmbedded2();
        public MyEmbedded1 myEmbedded1b = new MyEmbedded1();
        public MyEmbedded2 myEmbedded2b = new MyEmbedded2();
        public List<float> myFloats = new List<float>(){ 123.1f, 23.4f};
        public List<object> myObjects = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
        
        public Dictionary<string, MyEmbedded1> myEmbeddedDict = new Dictionary<string, MyEmbedded1>();
        public object someObj = "Something";
        public List<MyEmbedded1> embeddedList = new List<MyEmbedded1>() { new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1() };

        public string MyProperty { get; set; } = "propValue";

        public TestDto(int myInt, MyEmbedded1 myEmbedded)
        {
            this.myInt = myInt;
            //this.myEmbedded = myEmbedded;
            //this.self = this;

            myEmbeddedDict["1"] = new MyEmbedded1();
            myEmbeddedDict["2"] = new MyEmbedded1();

            //myObjects.Add(myEmbedded);
        }
        public TestDto() { }

/*        public override void Mutate()
        {
            base.Mutate();

            privInt = privInt * 2;
            myInt = myInt * 2;
            myString += "*";
            //myEmbedded = new MyEmbedded1() { x = 42 };

        }*/
    }

    public enum TestEnum
    {
        TestA,
        TestB,
        TestC
    }

    public interface IMyInterface
    {

    }

    public class MyEmbedded1 : IMyInterface
    {
        public int x = 1;
    }

    public class MyEmbedded2 : IMyInterface
    {
        public short y = 2;
    }

    public class TestDto2
    {
        public string str1 = "Mystring1";
        public string str2 = "Mystring2";
        public string str3 = "Mystring3";
        public string str4 = "Mystring4";
        public string myString1 = "Hello: \\, \", \\, \n";
        public string myString2 = "Hello: \\, \", \\, \n";
        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public List<float> myFloats = new List<float>() { 123.1f, 23.4f, 236.34f, 87.0f, 0f, 1234.0f, 0.12345f };
        public int int1 = 123451;
        public short int2 = 1234;
        public long int3 = 123453;
        public ulong int4 = 123454;
        public double double1 = 12.1231;
    }

    public class TestDto3
    {
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
    }



    internal class JsonTest
    {
        public class NullStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get; set; } = 0;

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count) => 0;

            public override long Seek(long offset, SeekOrigin origin) => 0;

            public override void SetLength(long value) { }

            public override void Write(byte[] buffer, int offset, int count) { }
        }

        public static async Task Run()
        {

            LoopJsonDeserializer loopJsonDeserializer = new LoopJsonDeserializer();
            //var result = loopJsonDeserializer.Deserialize<int>("5.123e4");



            var opt = new JsonSerializerOptions()
            {
                IncludeFields = true,
                //ReferenceHandler = ReferenceHandler.Preserve
                
            };

            int iterations = 1_000_000;

            //var testDto = new TestDto(99, new MyEmbedded1());
            //var testDto = new TestDto2();
            //var testDto = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
            //var testDto = new HashSet<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
            var testDto = 1234.5678;
            //var testDto = "Hello: \\, \", \\, \n";
            //var testDto = new object();
            //var testDto = new Dictionary<int, string>() { [12] = "Hello1", [79] = "Hello2" };
            //var testDto = new Dictionary<int, MyEmbedded1>() { [1] = new MyEmbedded1(), [2] = null };
            //var testDto = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
            //object testDto = 123;
            //var testDto = new TestDto3();
            //var testDto = AppTime.Now;
            //var testDto = TestEnum.TestB;
            /*var testDto = new Dictionary<int, object>() 
            { 
                [12] = new Dictionary<string, int>() { ["a"] = 123, ["b"] = 42 },
                [42] = new Dictionary<int, string>() { [3] = "Hello3", [4] = "Hello4" },
                [99] = 99,
                [111] = 123.123
            };*/

            //testDto[112] = testDto;

            Type testDtoType = testDto.GetType();
            string json; 
            //byte[] json;

            //Stream stream = new NullStream();
            MemoryStream stream = new MemoryStream();

            LoopJsonSerializer loopSerializer1 = new LoopJsonSerializer(new LoopJsonSerializer.Settings()
            {
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = LoopJsonSerializer.ReferenceCheck.NoRefCheck,
                enumAsString = true,
            });

            LoopJsonSerializer loopSerializer2 = new(new LoopJsonSerializer.Settings()
            {
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = LoopJsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
                enumAsString = false
            });

            LoopJsonSerializer loopSerializer3 = new(new LoopJsonSerializer.Settings()
            {
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = LoopJsonSerializer.ReferenceCheck.OnLoopReplaceByRef,
                enumAsString = false
            });

            FeatureJsonSerializer featureJsonSerializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings()
            {
                typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = FeatureJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = FeatureJsonSerializer.ReferenceCheck.NoRefCheck,
                enumAsString = false,
            });            

            TimeSpan elapsed;
            long beforeCollection;
            long afterCollection;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var tk = AppTime.TimeKeeper;
            Console.WriteLine("SerializerTest");
            while (true)
            {
                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = JsonSerializer.SerializeToUtf8Bytes(testDto, testDtoType, opt);
                    featureJsonSerializer.Serialize(stream, testDto);
                    //json = featureJsonSerializer.Serialize(testDto);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                var elapsed_A = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"FJsonSerializer: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(1.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = JsonSerializer.SerializeToUtf8Bytes(testDto, testDtoType, opt);
                    JsonSerializer.Serialize(stream, testDto, opt);
                    //json = JsonSerializer.Serialize(testDto, opt);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                var elapsed_B = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"JsonSerializer:  {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(1.Seconds());

                Console.WriteLine($"JsonSerializerF/Text.Json:  {100 * elapsed_A/elapsed_B}%");

                /*
                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                    loopSerializer1.Serialize(stream, testDto);
                    //json = loopSerializer1.Serialize(testDto);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"LoopSerializer1: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(2.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                    loopSerializer2.Serialize(stream, testDto);
                    //json = loopSerializer2.Serialize(testDto);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"LoopSerializer2: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(2.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                    loopSerializer3.Serialize(stream, testDto);
                    //json = loopSerializer3.Serialize(testDto);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"LoopSerializer3: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(2.Seconds());
                */
            }

            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
