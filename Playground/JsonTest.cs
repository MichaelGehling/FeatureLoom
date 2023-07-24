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
        public object self;
        private int privInt = 42;
        public int myInt = 123;
        public string myString = "Hello: \\, \", \\, \n";
        public IMyInterface myEmbedded;
        public List<float> myFloats = new List<float>(){ 123.1f, 23.4f};
        public List<object> myObjects = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
        public IDictionary<string, IMyInterface> myEmbeddedDict = new Dictionary<string, IMyInterface>();
        public object someObj = "Something";

        public string MyProperty { get; set; } = "propValue";

        public TestDto(int myInt, IMyInterface myEmbedded)
        {
            this.myInt = myInt;
            this.myEmbedded = myEmbedded;
            //this.self = this;

            myEmbeddedDict["1"] = new MyEmbedded1();
            myEmbeddedDict["2"] = new MyEmbedded2();

            myObjects.Add(myEmbedded);
        }
        public TestDto() { }

        public override void Mutate()
        {
            base.Mutate();

            privInt = privInt * 2;
            myInt = myInt * 2;
            myString += "*";
            myEmbedded = new MyEmbedded1() { x = 42 };

        }
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
        public string str1 = "Mystring";
        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public int int1 = 12345;
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
            var opt = new JsonSerializerOptions()
            {
                IncludeFields = true,
                //ReferenceHandler = ReferenceHandler.Preserve
            };

            int iterations = 1_000_000;

            var testDto = new TestDto(99, new MyEmbedded1());
            //var testDto = new TestDto2();
            Type testDtoType = testDto.GetType();
            string json;
            //byte[] json;

            NullStream nullStream = new NullStream();

            GC.Collect();
            AppTime.Wait(1.Seconds());
            var tk = AppTime.TimeKeeper;
            for (int i = 0; i < iterations; i++)
            {
                //json = JsonSerializer.SerializeToUtf8Bytes(testDto, testDtoType, opt);
                JsonSerializer.Serialize(nullStream, testDto, opt);
                //json = JsonSerializer.Serialize(testDto, opt);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());

            var settingsloop = new LoopJsonSerializer.Settings()
            {
                referenceCheck = LoopJsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields
            };
            LoopJsonSerializer loopSerializer = new();
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                loopSerializer.Serialize(nullStream, testDto, settingsloop);
                //json = loopSerializer.Serialize(testDto, settingsloop);
            }
            Console.WriteLine(tk.Elapsed);
            AppTime.Wait(1.Seconds());
            GC.Collect();
            AppTime.Wait(1.Seconds());

            var settings = new MyJsonSerializer.Settings()
            {
                referenceCheck = MyJsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
                typeInfoHandling = MyJsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
                dataSelection = MyJsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields
            };
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                //json = MyJsonSerializer.SerializeToUtf8Bytes(testDto, settings);
                MyJsonSerializer.Serialize(nullStream, testDto, settings);
                //json = MyJsonSerializer.Serialize(testDto, settings);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());

            settingsloop = new LoopJsonSerializer.Settings()
            {
                referenceCheck = LoopJsonSerializer.ReferenceCheck.NoRefCheck,
                typeInfoHandling = LoopJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = LoopJsonSerializer.DataSelection.PublicFieldsAndProperties
            };
            loopSerializer = new();
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                //json = loopSerializer.SerializeToUtf8Bytes(testDto, settingsloop);
                loopSerializer.Serialize(nullStream, testDto, settingsloop);
                //json = loopSerializer.Serialize(testDto, settingsloop);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());

            settings = new MyJsonSerializer.Settings()
            {
                referenceCheck = MyJsonSerializer.ReferenceCheck.NoRefCheck,
                typeInfoHandling = MyJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = MyJsonSerializer.DataSelection.PublicFieldsAndProperties
            };
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {                
                //json = MyJsonSerializer.SerializeToUtf8Bytes(testDto, settings);
                MyJsonSerializer.Serialize(nullStream, testDto, settings);
                //json = MyJsonSerializer.Serialize(testDto, settings);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());


            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
