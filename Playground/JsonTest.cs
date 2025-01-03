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
//using Microsoft.VisualBasic.FileIO;
//using System.Text.Json.Serialization;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using FeatureLoom.Security;
using FeatureLoom.Extensions;
using System.Reflection.Metadata.Ecma335;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Newtonsoft.Json;
using System.Threading;
using FeatureLoom.Serialization;
using System.Xml;

namespace Playground
{
    public class BaseDto
    {
        private double privBase = 0.77;
        protected int protBase = 1;
        public int pubBase = 2;

        public virtual void Mutate()
        {
          //  privBase = privBase * 2;
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
        public string myString = "Hello: \\, \", \\, \n";
        public MyEmbedded1 myEmbedded1 = new MyEmbedded1();
        public MyEmbedded2 myEmbedded2 = new MyEmbedded2();
        public IEnumerable myObjects = new HashSet<object>() { 99.9f, new MyEmbedded1(), "Hallo" };

        public string str1 = "Mystring1";
        public string str2 = "Mystring2";
        public string str3 = "Mystring3";
        public string str4 = "Mystring4";
        public string myString1 = "Hello: \\, \", \\, \n";
        public string myString2 = "Hello: \\, \", \\, \n";
        public int int1 = 123451;
        public short int2 = 1234;
        public long int3 = 123453;
        public ulong int4 = 123454;
        public double double1 = 12.1231;

        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public List<float> myFloats = new List<float>() { 123.1f, 23.4f, 236.34f, 87.0f, 0f, 1234.0f, 0.12345f };
        
        public object someObj = "Something";
        public List<MyEmbedded1> embeddedList = new List<MyEmbedded1>() { new MyEmbedded1() { x=43}, new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1() };

        public Dictionary<string, MyEmbedded1> myEmbeddedDict = new Dictionary<string, MyEmbedded1>();

        public string MyProperty { get; set; } = "propValue";
        
        public TestDto()
        {
            //this.self = this;

            myEmbeddedDict["prop1"] = new MyEmbedded1();
            myEmbeddedDict["prop2"] = new MyEmbedded1();
            myEmbeddedDict["prop1ref"] = embeddedList[0];
        }

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

    public interface IMyGenericInterface<T>
    {

    }

    public class MyEmbedded1 : IMyInterface
    {
        public int? x = 1;
    }

    public class MyEmbedded1x : IMyInterface
    {
        public int? x = 1;

        protected MyEmbedded1x()
        {
        }
    }

    public class MyEmbedded2 : IMyInterface
    {
        public IEnumerable myObjects = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
        public short y = 2;
    }

    public class MyEmbedded3 : IMyInterface
    {
        public MyEmbedded3(short y)
        {
            this.y = y;
        }

       

        public IMyInterface interfaceObject = new MyEmbedded1();
        public short y = 2;
        public List<int> intList = new List<int>() { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public string[] strArray = { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public MyEmbedded1 myEmbedded1 = new MyEmbedded1();
    }

    public class MyGenericEmbedded<T> : IMyInterface, IMyGenericInterface<T>
    {
        T x = default(T);
    }

    public readonly struct MyStruct
    {
        public MyStruct(int x)
        {
            this.x = x;
        }

        private readonly int x;
        public int X { get => x; }
    }

    public class TestDto2
    {
        
        public string str1 = "Mystring1";
        public string str2 = "Mystring2";
        public string str3 = "Mystring3";
        public string str4 = "Mystring4";
        public string myString1 = "Hello: \\, \", \\, \n";
        public string myString2 = "Hello: \\, \", \\, \n";        
        public int int1 = 123451;
        public short int2 = 1234;
        public long int3 = 123453;
        public ulong int4 = 123454;
        public double double1 = 12.1231;
     
        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
        public int[] intList = new int[] { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
        public List<float> myFloats = new List<float>() { 123.1f, 23.4f, 236.34f, 87.0f, 0f, 1234.0f, 0.12345f };     
     
    }

    public class TestDto3
    {
        public List<string> strList = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
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

        class MyGenericIListTypeHandlerCreator : FeatureJsonSerializer.GenericTypeHandlerCreator
        {
            public MyGenericIListTypeHandlerCreator() : base(typeof(IList<>))
            {
            }

            protected override void CreateAndSetGenericTypeHandler<ARG1>(FeatureJsonSerializer.ExtensionApi api, FeatureJsonSerializer.ICachedTypeHandler cachedTypeHandler)
            {                
                var elementTypeHandler = api.GetCachedTypeHandler(typeof(ARG1));
                FeatureJsonSerializer.ItemHandler<IList<ARG1>> itemHandler;
                if (!api.RequiresHandler && api.Writer.TryPreparePrimitiveWriteDelegate<ARG1>(out var primitiveWrite))
                {
                    itemHandler = list =>
                    {
                        var count = list.Count;
                        if (count >= 1) primitiveWrite(list[0]);
                        for (int i = 1; i < count; i++)
                        {
                            api.Writer.WriteComma();
                            primitiveWrite(list[i]);                            
                        }                        
                    };                    
                }
                else if (!api.RequiresItemNames && elementTypeHandler.NoRefTypes)
                {
                    itemHandler = list =>
                    {
                        var count = list.Count;
                        if (count >= 1) elementTypeHandler.HandleItem(list[0], default);
                        for (int i = 1; i < count; i++)
                        {
                            api.Writer.WriteComma();
                            elementTypeHandler.HandleItem(list[i], default);
                        }
                    };                    
                }
                else
                {
                    itemHandler = list =>
                    {
                        var lastElementTypeHandler = elementTypeHandler;
                        var count = list.Count;
                        if (count >= 1)
                        {
                            var element = list[0];
                            if (element == null) api.Writer.WriteNullValue();
                            else
                            {
                                Type elementType = element.GetType();
                                if (elementType != lastElementTypeHandler.HandlerType) lastElementTypeHandler = api.GetCachedTypeHandler(elementType);
                                lastElementTypeHandler.HandleItem(element, api.Writer.GetCollectionIndexName(0));
                            }
                        }
                        for (int i = 1; i < count; i++)
                        {
                            api.Writer.WriteComma();
                            var element = list[i];
                            if (element == null) api.Writer.WriteNullValue();
                            else
                            {
                                Type elementType = element.GetType();
                                if (elementType != lastElementTypeHandler.HandlerType) lastElementTypeHandler = api.GetCachedTypeHandler(elementType);
                                lastElementTypeHandler.HandleItem(element, api.Writer.GetCollectionIndexName(i));
                            }
                        }
                    };                    
                }

                JsonDataTypeCategory typeCategory = elementTypeHandler.NoRefTypes ? JsonDataTypeCategory.Array_WithoutRefChildren : JsonDataTypeCategory.Array;
                cachedTypeHandler.SetItemHandler(itemHandler, typeCategory);
            }
        }


        public class JsonStreamProcessor<T>
        {
            private readonly Stream _stream;
            private readonly Action<T> processor;

            public JsonStreamProcessor(Stream stream, Action<T> processor)
            {
                _stream = stream;
                this.processor = processor;
            }

            public async Task ProcessStreamAsync()
            {
                using var streamReader = new StreamReader(_stream);
                using var jsonReader = new JsonTextReader(streamReader)
                {
                    SupportMultipleContent = true  // Allow multiple JSON objects in the stream
                };

                var serializer = new Newtonsoft.Json.JsonSerializer();

                try
                {
                    while (await jsonReader.ReadAsync())
                    {
                        //if (jsonReader.TokenType == JsonToken.StartObject)
                        {
                            // Deserialize the JSON object
                            T jsonObject = serializer.Deserialize<T>(jsonReader);
                            //processor(jsonObject);
                        }
                    }
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    Console.WriteLine($"JSON parsing error: {ex.Message}");
                    // Handle parsing errors as needed
                }
            }
        }

        public struct TestStruct
        {
            public int i;
            private Guid guid;
        }


        public static async Task Run()
        {


            /*
            var input = new TestDto2();
            int iterations = 100_000;
            MemoryStream memoryStream;
            while (true)
            {
                Thread.Sleep(1000);

                var tkx = AppTime.TimeKeeper;
                memoryStream = new MemoryStream();
                for (int i = 0; i < iterations; i++)
                {
                    featureJsonSerializer.Serialize(memoryStream, input);                
                    //System.Text.Json.JsonSerializer.Serialize(memoryStream, input);
                    memoryStream.WriteByte((byte)'\n');
                }
                Console.WriteLine("S: " + (iterations / tkx.Elapsed.TotalSeconds).ToString());

                Thread.Sleep(1000);

                memoryStream.Seek(0, SeekOrigin.Begin);
                tkx.Restart();                
                while (featureJsonDeserializer.TryDeserialize(memoryStream, out TestDto2 output))
                {

                }
                Console.WriteLine("FL:" + (iterations / tkx.Elapsed.TotalSeconds).ToString());

                Thread.Sleep(1000);

                memoryStream.Seek(0, SeekOrigin.Begin);
                tkx.Restart();               
                var jsonStreamProcessor = new JsonStreamProcessor<TestDto2>(memoryStream, null);
                await jsonStreamProcessor.ProcessStreamAsync();
                Console.WriteLine("JN:" + (iterations / tkx.Elapsed.TotalSeconds).ToString());
            }

            */

            string jsonString = """
                                
                                {
                                    "interfaceObject" : { "x" : 1111 },
                                    "UnknownField_object" : { "a" : 123, "b" : null },
                                    "UnknownField_number" : 123.321,
                                    "UnknownField_string" : "Something",
                                    "UnknownField_array" : [9,8,7,6,5,4,3,2,1],
                                    "UnknownField_bool" : true,
                                    "UnknownField_null" : null,
                                    "myEmbedded1" : {
                                        "x" : 42
                                    },
                                    "y":99,
                                    "intList":[9,8,7,6,5,4,3,2,1],
                                    "strArray": ["Hello", "World", "!"]                                    
                                }
                                """;
            /*jsonString = """
                         {
                            "field1": 123,
                            "field2": 456
                         }
                         """;

            jsonString = """
                         [
                            { "key": "field1", "value": 123},
                            { "key": "field2", "value": 456}
                         ]
                         """;
            */

            jsonString = """
                         {
                            "privInt": 999,
                            "myInt": 888,
                            "myEmbedded1": { "x": 777 }
                         }
                         "<SomeXmlNode>content</SomeXmlNode>"
                         "NaN"
                         { "i": 123, "guid": "f0ed1af3-4412-4f08-8237-3e2e3e6d29ef"}
                         [{"x": 123}, {"x": 456}]
                         [1,2,3]
                         123.123
                         "Hallo!"
                         {
                            "x": 987,                             
                         }
                         {                         
                            "y": 1234,     
                            "intList": [1,2,3,4,5]
                         }
                         {
                            "bla": 123,
                            "blu": "aakskd"
                         }


                         """;
            FeatureJsonDeserializer.Settings deserializerSettings = new FeatureJsonDeserializer.Settings();
            deserializerSettings.AddMultiOptionTypeMapping(typeof(object), typeof(MyEmbedded1), typeof(MyEmbedded2), typeof(MyEmbedded3));
            deserializerSettings.AddMultiOptionTypeMapping(typeof(IMyInterface), typeof(MyEmbedded1), typeof(MyEmbedded2), typeof(MyEmbedded3));
            deserializerSettings.AddGenericTypeMapping(typeof(IMyGenericInterface<>), typeof(MyGenericEmbedded<>));
            deserializerSettings.AddConstructor<MyEmbedded3>(() => new MyEmbedded3(default));
            //deserializerSettings.initialBufferSize = 20;
            //deserializerSettings.AddConstructor<KeyValuePair<string, int>>(() => new KeyValuePair<string, int>(default, default));
            //deserializerSettings.AddConstructor<KeyValuePair<string, object>>(() => new KeyValuePair<string, object>(default, default));
            //deserializerSettings.AddConstructor<KeyValuePair<object, object>>(() => new KeyValuePair<object, object>(default, default));
            var featureJsonDeserializer = new FeatureJsonDeserializer(deserializerSettings);
            //featureJsonDeserializer.TryDeserialize<MyEmbedded3>(jsonString.ToStream(), out var result);
            //featureJsonDeserializer.TryDeserialize<int[][]>(jsonString.ToStream(), out var result);

            deserializerSettings.AddCustomTypeReader<XmlElement>(new FeatureJsonDeserializer.CustomTypeReader<XmlElement>(
                JsonDataTypeCategory.Primitive,
                api =>
                {
                    if (api.TryReadStringValue(out string xmlString))
                    {
                        XmlElement xml = xmlString.ToXmlElement(null);
                        return xml;
                    }
                    else throw new Exception("Not a string");
                }));

            var jsonStream = jsonString.ToStream();

            featureJsonDeserializer.SetDataSource(jsonStream);
            bool dataLeft = featureJsonDeserializer.IsAnyDataLeft();

            TestDto itemToPopulate = new TestDto()
            {
                myString = "populated",
            };
            featureJsonDeserializer.TryPopulate(ref itemToPopulate);

            featureJsonDeserializer.TryDeserialize<XmlElement>(out var xmlElement);
            featureJsonDeserializer.TryDeserialize<double>(out var d);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<TestStruct>(out var testStruct);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result2);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result3);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result4);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result5);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result6);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();
            featureJsonDeserializer.TryDeserialize<object>(out var result7);
            dataLeft = featureJsonDeserializer.IsAnyDataLeft();





            var opt = new JsonSerializerOptions()
            {
                IncludeFields = true,
                //ReferenceHandler = ReferenceHandler.Preserve
                //WriteIndented = true

            };

            int iterations = 1_000_000;

            //var testDto = new TestDto();
            //var testDto = -128;
            //IEnumerable testDto = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
            var testDto = new TestDto2();
            //var testDto = new MyEmbedded1();
            //var testDto = new List<MyEmbedded1>() { new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1(), new MyEmbedded1() };
            //var testDto = new List<MyStruct>() { new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct(), new MyStruct() };
            //var testDto = new List<float>() { 0.1f, 1.1f, 12.1f, 123.1f, 1234.1f, 12345.1f, 123456.1f, 1234567.1f, 12345678.1f, 123456789.1f };
            //var testDto = new List<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
            //var testDto = new List<double>() { 354476.143, 0983427.1234, 0.000005987654321, 0.0, 12.0213, 123454678901234.1 };
            //var testDto = new HashSet<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" };
            //var testDto = new object();
            //var testDto = new MyStruct();
            //var testDto = 1234567.7f; 
            //var testDto = 12345678.0;
            //var testDto = 0;
            //var testDto = true;
            //var testDto = 123456789012345678901234567890.0;            
            //var testDto = "Hello: \\, \", \\, \n";
            //var testDto = "Mystring1";            
            //var testDto = new Dictionary<int, string>() { [12] = "Hello1", [79] = "Hello2" };
            //var testDto = new Dictionary<int, MyEmbedded1>() { [1] = new MyEmbedded1(), [2] = null };
            //var testDto = new List<int> { 0, 1, -2, 10, -22, 100, -222, 1000, -2222, 10000, -22222 };
            //object testDto = 123;
            //var testDto = new TestDto3();
            //var testDto = AppTime.Now;
            //var testDto = TestEnum.TestB;
            /*var testDto = new Dictionary<string, object>() 
            { 
                ["Hallo"] = new Dictionary<string, int>() { ["a"] = 123, ["b"] = 42 },
                ["Welt"] = new Dictionary<int, string>() { [3] = "Hello3", [4] = "Hello4" },
                ["Wie"] = 99,
                ["Gehts"] = 123.123,
                ["Denn"] = new HashSet<string>() { "Hallo1", "Hallo2", "Hallo3", "Hallo4", "Hallo5" },
                ["So"] = new List<double>() { 354476.143, 0983427.1234, 0.0, 0.0, 12.0213 }
            };
            testDto["?"] = testDto["Hallo"];
            */
            //var testDto = new HashSet<object>() { new Dictionary<string, int>() { ["Hallo"] = 12, ["World"] = 34 }, null, new Dictionary<string, int>(), 99, 42, "Hello", "World", 123.999 };
            //testDto.Add(testDto[0]);

            //var testDto = new ArrayList() { new Dictionary<string, int>() { ["Hallo"] = 12, ["World"] = 34 }, null, new Dictionary<string, int>(), 99, 42, "Hello", "World", 123.999 };            

            /*var testDto = new Dictionary<object, int>()
            {
                [new MyEmbedded1()] = 1,
                [new MyEmbedded2()] = 2,
                [new MyEmbedded3()] = 3,
            };*/
            //var testDto = new KeyValuePair<object, int>(new MyEmbedded1(), 1);
            //var testDto = new decimal(123.123);
            //var testDto = RandomGenerator.GUID();
            //var testDto = AppTime.Now;
            //var testDto = (int?)null;

            //Type testDtoType = testDto.GetType();
            //byte[] json;

            //Stream stream = new NullStream();
            MemoryStream stream = new MemoryStream();

            var settings = new FeatureJsonSerializer.Settings()
            {
                typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                dataSelection = FeatureJsonSerializer.DataSelection.PublicFieldsAndProperties,
                referenceCheck = FeatureJsonSerializer.ReferenceCheck.NoRefCheck,
                enumAsString = true,
                treatEnumerablesAsCollections = true,
                indent = true
            };

            settings.AddCustomTypeHandlerCreator(new MyGenericIListTypeHandlerCreator());

            /*
            settings.AddCustomTypeHandlerCreator<int>(
                FeatureJsonSerializer.JsonDataTypeCategory.Primitive,
                api =>
                {
                    var w = api.Writer;
                    return value => w.WritePrimitiveValue(value);
                });

            settings.AddCustomTypeHandlerCreator<int[]>(
                FeatureJsonSerializer.JsonDataTypeCategory.Array_WithoutRefChildren,
                api =>
                {
                    var w = api.Writer;
                    return value =>
                    {
                        for (var i = 0; i < value.Length; i++)
                        {
                            w.WritePrimitiveValue(value[i]);
                            w.WriteComma();
                        }
                        w.RemoveTrailingComma();
                    };
                });
            
            settings.AddCustomTypeHandlerCreator<List<int>>(
                FeatureJsonSerializer.JsonDataTypeCategory.Array_WithoutRefChildren,
                api =>
                {
                    var w = api.Writer;
                    return value =>
                    {
                        var count = value.Count;
                        for (var i = 0; i < count; i++)
                        {
                            w.WritePrimitiveValue(value[i]);
                            w.WriteComma();
                        }
                        w.RemoveTrailingComma();
                    };
                });
            
            
            settings.AddCustomTypeHandlerCreator<List<MyEmbedded1>>(
                FeatureJsonSerializer.JsonDataTypeCategory.Array,
                (api) =>
                {
                    var xFieldName = api.PrepareFieldNameBytes(nameof(MyEmbedded1.x));

                    return (list) =>
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (i > 0) api.WriteComma();
                            api.OpenObject();
                            api.WriteToBuffer(xFieldName);
                            api.WritePrimitiveValue(list[i].x);
                            api.CloseObject();
                        }
                    };
                });

            settings.AddCustomTypeHandlerCreator<MyEmbedded1>(
                FeatureJsonSerializer.JsonDataTypeCategory.Object_WithoutRefChildren,
                (api) =>
                {
                    var xFieldName = api.PrepareFieldNameBytes(nameof(MyEmbedded1.x));
                    return (item) =>
                    {
                        api.WriteToBuffer(xFieldName);
                        api.WritePrimitiveValue(item.x);
                    };
                });
            */

            var featureJsonSerializer = new FeatureJsonSerializer(settings);

            Console.WriteLine("FeatureJsonSerializer:");
            Console.WriteLine(featureJsonSerializer.Serialize(testDto));
            Console.WriteLine("\nSystem.Text.Json:");            
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(testDto, opt));
            Console.WriteLine("\nUtf8Json:");
            Console.WriteLine(UTF8Encoding.UTF8.GetString(Utf8Json.JsonSerializer.Serialize(testDto)));

            //featureJsonSerializer = new FeatureJsonSerializer(settings);

            TimeSpan elapsed;
            long beforeCollection;
            long afterCollection;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var tk = AppTime.TimeKeeper;
            Console.WriteLine("SerializerTest");
            while (true)
            {
               /* tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    // Do nothing
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                var elapsed_DUMMY = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"EmptyLoop:       {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(0.5.Seconds());
               */
                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {                    
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
                AppTime.Wait(0.5.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = JsonSerializer.SerializeToUtf8Bytes(testDto, testDtoType, opt);
                    System.Text.Json.JsonSerializer.Serialize(stream, testDto, opt);
                    //json = JsonSerializer.Serialize(testDto, opt);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                var elapsed_B = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"Text.Json:       {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(0.5.Seconds());

                tk.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //json = JsonSerializer.SerializeToUtf8Bytes(testDto, testDtoType, opt);
                    Utf8Json.JsonSerializer.Serialize(stream, testDto);
                    //json = UTF8Encoding.UTF8.GetString(Utf8Json.JsonSerializer.Serialize(testDto));
                    //json = JsonSerializer.Serialize(testDto, opt);
                    stream.Position = 0;
                }
                elapsed = tk.Elapsed;
                var elapsed_C = elapsed;
                beforeCollection = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                afterCollection = GC.GetTotalMemory(false);
                Console.WriteLine($"Utf8Json:        {elapsed} / {(beforeCollection - afterCollection)} bytes");
                AppTime.Wait(0.5.Seconds());

                Console.WriteLine($"JsonSerializerF/Text.Json:  {(elapsed_A/elapsed_B).ToString("F")}% of time");
     
                
            }

            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
