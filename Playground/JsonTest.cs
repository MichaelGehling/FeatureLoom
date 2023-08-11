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

namespace Playground
{
    public class BaseDto
    {
        private double privBase = 0.77;
        protected int protBase = 1;
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
        private int privInt = 42;
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
        //public List<object> myObjects = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
        public Dictionary<string, MyEmbedded1> myEmbeddedDict = new Dictionary<string, MyEmbedded1>();
        //public object someObj = "Something";

        public string MyProperty { get; set; } = "propValue";

        public TestDto(int myInt, MyEmbedded1 myEmbedded)
        {
            this.myInt = myInt;
            //this.myEmbedded = myEmbedded;
            //this.self = this;

            //myEmbeddedDict["1"] = new MyEmbedded1();
            //myEmbeddedDict["2"] = new MyEmbedded1();

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
        public int y = 2;
    }

    public class TestDto2
    {
        public string str1 = "Mystring1";
        public string str2 = "Mystring2";
        public string str3 = "Mystring3";
        public string str4 = "Mystring4";
        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public List<float> myFloats = new List<float>() { 123.1f, 23.4f, 236.34f, 87.0f, 0f, 1234.0f, 0.12345f };
        public int int1 = 123451;
        public int int2 = 123452;
        public int int3 = 123453;
        public int int4 = 123454;
        public double double1 = 12.1231;
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

            var testDto = new TestDto(99, new MyEmbedded1());
            //var testDto = new TestDto2();
            //var testDto = 1234.5678;
            //var testDto = "Hallo";
            
            Type testDtoType = testDto.GetType();
            string json;
            //byte[] json;

            Stream stream = new NullStream();
            //MemoryStream stream = new MemoryStream();

            LoopJsonSerializer loopSerializer1 = new LoopJsonSerializer(new LoopJsonSerializer.Settings()
            {
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = LoopJsonSerializer.ReferenceCheck.NoRefCheck,
                enumAsString = false,
            });

            LoopJsonSerializer loopSerializer2 = new(new LoopJsonSerializer.Settings()
            {
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = LoopJsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
                enumAsString = false
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
                    JsonSerializer.Serialize(stream, testDto, opt);
                    //json = JsonSerializer.Serialize(testDto, opt);
                    //var result1 = JsonSerializer.Deserialize<double>("-1234.567899");
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"JsonSerializer:  {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(2.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                    loopSerializer1.Serialize(stream, testDto);
                    //json = loopSerializer.Serialize(testDto);
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
                    //json = loopSerializer.Serialize(testDto);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"LoopSerializer2: {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(2.Seconds());

            }

            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
