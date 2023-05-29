using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Threading;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Microsoft.VisualBasic;
using FeatureLoom.Workflows;
using Microsoft.Extensions.Primitives;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using FeatureLoom.Synchronization;

namespace Playground
{
    public class BaseDto
    {
        private double privBase = 0.77;
        protected int protBase = 1;
        public int pubBase = 2;
    }

    public class TestDto : BaseDto
    {
        private int privInt = 42;
        public int myInt = 123;
        public string myString = "Hello";
        public IMyInterface myEmbedded;
        public List<float> myFloats = new List<float>(){ 123.1f, 23.4f};
        public Dictionary<string, IMyInterface> myEmbeddedDict = new Dictionary<string, IMyInterface>();
        public object someObj = "Something";

        public TestDto(int myInt, string myString, IMyInterface myEmbedded)
        {
            this.myInt = myInt;
            this.myString = myString;
            this.myEmbedded = myEmbedded;

            myEmbeddedDict["1"] = new MyEmbedded1();
            myEmbeddedDict["2"] = new MyEmbedded2();
        }
        public TestDto() { }
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

    public interface IJsonWriter
    {
        void OpenObject();
        void CloseObject();
        void OpenCollection();
        void CloseCollection();
        void WriteLabel(string label);
        void WriteLabel(ArraySegment<byte> utf8Label);
        void WriteEncodedValue(string value);
        void WriteEncodedValue(ArraySegment<byte> utf8Value);
        void WriteValue(string value);
        void WriteValue(byte value);
        void WriteValue(sbyte value);
        void WriteValue(short value);
        void WriteValue(ushort value);
        void WriteValue(int value);
        void WriteValue(uint value);
        void WriteValue(long value);
        void WriteValue(ulong value);
        void WriteValue(float value);
        void WriteValue(double value);
        void WriteValue(decimal value);
        void WriteValue(bool value);
        void WriteValue(char value);
        void WriteValue(DateTime value);
        void WriteValue(TimeSpan value);
        void WriteValue<T>(T value) where T : Enum;
    }


    public static class MyJsonSerializer
    {
        static Dictionary<Type, List<FieldInfo>> fieldInfos = new Dictionary<Type, List<FieldInfo>>();
        static FeatureLock fieldInfosLock = new FeatureLock();
        
        public static string Serialize<T>(T obj)
        {
            var sb = new StringBuilder();
            Serialize(obj, typeof(T), sb);
            return sb.ToString();
        }

        private static void Serialize(object obj, Type expectedType, StringBuilder sb)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; ;
            Type objType = obj.GetType();
            bool deviatingType = obj.GetType() != expectedType;
            
            if (objType.IsPrimitive)
            {
                if (!deviatingType)
                {
                    sb.Append(obj.ToString());
                    return;
                }
                else
                {
                    sb.Append("{")
                      .Append($"\"$Type\":\"").Append(objType.FullName).Append("\",")
                      .Append($"\"$Value\":").Append(obj.ToString())
                      .Append("}");
                    return;
                }
            }

            if (obj is string str)
            {
                if (!deviatingType)
                {
                    sb.Append("\"").Append(str).Append("\"");
                }
                else
                {
                    sb.Append("{")
                      .Append($"\"$Type\":\"").Append(objType.FullName).Append("\",")
                      .Append($"\"$Value\":\"").Append(str).Append("\"")
                      .Append("}");
                }
                return;
            }


            if (obj is ICollection && obj is IEnumerable items)
            {
                Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>));
                collectionType = collectionType ?? typeof(object);
                
                sb.Append("[");
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else sb.Append(",");
                    Serialize(item, collectionType, sb);
                }
                sb.Append("]");
                return;
            }
            if (objType.IsNullable() && obj is object o && o == null)
            {
                sb.Append("null");
                return;
            }


            bool isFirstField = true;
            sb.Append("{");

            if (deviatingType)
            {
                if (isFirstField) isFirstField = false;
                sb.Append($"\"$Type\":\"").Append(objType.FullName).Append("\"");
            }

            List<FieldInfo> fields;

            using (var lockHandle = fieldInfosLock.LockReadOnly())
            {
                if (!fieldInfos.TryGetValue(objType, out fields)) 
                {
                    lockHandle.UpgradeToWriteMode();

                    fields = new List<FieldInfo>();
                    fields.AddRange(objType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                    Type t = objType.BaseType;
                    while (t != null)
                    {

                        fields.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !fields.Any(field => field.Name == baseField.Name)));
                        t = t.BaseType;
                    }
                    fieldInfos[objType] = fields;
                }
            }
            
            foreach (var field in fields)
            {
                if (isFirstField) isFirstField = false;
                else sb.Append(",");
                sb.Append("\"").Append(field.Name).Append("\":");
                var value = field.GetValue(obj);                
                Serialize(value, field.FieldType, sb);                
            }
            sb.Append("}");

            return;
        }
    }

    internal class JsonTest
    {
        public static void Run()
        {
            var opt = new JsonSerializerOptions()
            {
                IncludeFields = true
            };

            object testDto = new TestDto(99, "World", new MyEmbedded1());
            string json;
            var tk = AppTime.TimeKeeper;
            for (int i = 0; i < 2_000_000; i++)
            {
                json = JsonSerializer.Serialize(testDto, opt);
            }
            Console.WriteLine(tk.Elapsed);
            tk.Restart();
            for (int i = 0; i < 2_000_000; i++)
            {
                json = FeatureLoom.Serialization.Json.SerializeToJson(testDto);
            }
            Console.WriteLine(tk.Elapsed);
            tk.Restart();
            for (int i = 0; i < 2_000_000; i++)
            {
                json = MyJsonSerializer.Serialize<object>(testDto);
            }
            Console.WriteLine(tk.Elapsed);

            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
