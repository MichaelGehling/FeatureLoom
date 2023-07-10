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
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.IO;
using System.Data;
using FeatureLoom.TCP;

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
        private int privInt = 42;
        public int myInt = 123;
        public string myString = "Hello: \\, \", \\, \n";
        public IMyInterface myEmbedded;
        public List<float> myFloats = new List<float>(){ 123.1f, 23.4f};
        public List<object> myObjects = new List<object>() { 99.9f, new MyEmbedded1(), "Hallo" };
        public IDictionary<string, IMyInterface> myEmbeddedDict = new Dictionary<string, IMyInterface>();
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
        public class Settings
        {
            public bool addDeviatingTypeInfo = true;
            public bool addAllTypeInfo = false;
            public bool onlyPublicFields = false;
            public bool performKnownRefCheck = true;            
        }

        private struct Crawler
        {
            public Settings settings;
            public string currentPath;
            public Dictionary<object, string> pathMap;

            public static Crawler Root(object rootObj, Settings settings)
            {                
                if (!settings.performKnownRefCheck) return new Crawler() { settings = settings };

                return new Crawler()
                {
                    settings = settings,
                    currentPath = "",
                    pathMap = new Dictionary<object, string>(){{rootObj, ""}}
                };
            }

            public Crawler NewChild(object child, string name)
            {
                if (!settings.performKnownRefCheck) return this;

                string childPath = currentPath + "." + name;
                pathMap[child] = childPath;

                return new Crawler()
                {
                    settings = settings,
                    currentPath = childPath,
                    pathMap = pathMap
                };
            }

            public bool Exists(object obj, out string path)
            {
                path = null;
                if (!settings.performKnownRefCheck) return false;
                return pathMap.TryGetValue(obj, out path);
            }
        }

        static Settings defaultSettings = new Settings();

        static Dictionary<Type, List<FieldInfo>> fieldInfos = new Dictionary<Type, List<FieldInfo>>();
        static Dictionary<FieldInfo, Action<object, FieldInfo, StringBuilder, Settings>> fieldWriters = new Dictionary<FieldInfo, Action<object, FieldInfo, StringBuilder, Settings>>();
        static Dictionary<Type, int> typeBufferInfo = new Dictionary<Type, int>();
        static FeatureLock fieldInfosLock = new FeatureLock();
        
        public static string Serialize<T>(T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;            

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Type objType = obj.GetType();
            int buffer = 64;
            using (fieldInfosLock.LockReadOnly())
            {
                if (typeBufferInfo.TryGetValue(objType, out var b)) buffer = b;
            }
            var sb = new StringBuilder(buffer);

            SerializeValue(obj, typeof(T), sb, settings);

            Thread.CurrentThread.CurrentCulture = oldCulture;

            if (sb.Length > buffer)
            {
                using (fieldInfosLock.Lock())
                {
                    typeBufferInfo[objType] = sb.Length;
                }
            }
            return sb.ToString();
        }

        private static void SerializeValue(object obj, Type expectedType, StringBuilder sb, Settings settings)
        {
            if (obj == null)
            {
                sb.Append("null");
                return;
            }

            Type objType = obj.GetType();

            bool deviatingType = expectedType != obj.GetType();

            if (objType.IsPrimitive)
            {
                PrepareUnexpectedValue();
                sb.Append(obj.ToString());
                FinishUnexpectedValue();
            }
            else if (obj is string str)
            {
                PrepareUnexpectedValue();
                sb.Append('\"').WriteEscapedString(str).Append('\"');
                FinishUnexpectedValue();
            }
            else if (obj is IEnumerable items && (obj is ICollection || objType.ImplementsGenericInterface(typeof(ICollection<>))))
            {
                PrepareUnexpectedValue();
                SerializeCollection(sb, objType, items, settings);
                FinishUnexpectedValue();
            }
            else SerializeComplexType(obj, expectedType, sb, settings);

            void PrepareUnexpectedValue()
            {
                if (settings.addAllTypeInfo || (settings.addDeviatingTypeInfo && deviatingType))
                {
                    sb.Append('{')
                    .Append("\"$Type\":\"").Append(objType.FullName).Append("\",")
                    .Append("\"$Value\":");
                }
            }

            void FinishUnexpectedValue()
            {
                if (settings.addAllTypeInfo || (settings.addDeviatingTypeInfo && deviatingType))
                {
                    sb.Append("}");
                }
            }
        }




        private static void SerializeCollection(StringBuilder sb, Type objType, IEnumerable items, Settings settings)
        {
            if (items is IEnumerable<string> string_items) SerializeStringCollection(string_items, sb);
            else if (items is IEnumerable<int> int_items) SerializePrimitiveCollection(int_items, sb);
            else if (items is IEnumerable<uint> uint_items) SerializePrimitiveCollection(uint_items, sb);
            else if (items is IEnumerable<byte> byte_items) SerializePrimitiveCollection(byte_items, sb);
            else if (items is IEnumerable<sbyte> sbyte_items) SerializePrimitiveCollection(sbyte_items, sb);
            else if (items is IEnumerable<short> short_items) SerializePrimitiveCollection(short_items, sb);
            else if (items is IEnumerable<ushort> ushort_items) SerializePrimitiveCollection(ushort_items, sb);
            else if (items is IEnumerable<long> long_items) SerializePrimitiveCollection(long_items, sb);
            else if (items is IEnumerable<ulong> ulong_items) SerializePrimitiveCollection(ulong_items, sb);
            else if (items is IEnumerable<bool> bool_items) SerializePrimitiveCollection(bool_items, sb);
            else if (items is IEnumerable<char> char_items) SerializePrimitiveCollection(char_items, sb);
            else if (items is IEnumerable<float> float_items) SerializePrimitiveCollection(float_items, sb);
            else if (items is IEnumerable<double> double_items) SerializePrimitiveCollection(double_items, sb);
            else if (items is IEnumerable<IntPtr> intPtr_items) SerializePrimitiveCollection(intPtr_items, sb);
            else if (items is IEnumerable<UIntPtr> uIntPtr_items) SerializePrimitiveCollection(uIntPtr_items, sb);
            else 
            {
                Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>));
                collectionType = collectionType ?? typeof(object);

                sb.Append('[');
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else sb.Append(',');
                    SerializeValue(item, collectionType, sb, settings);
                }
                sb.Append(']');
            }
        }

        private static void SerializeComplexType(object obj, Type expectedType, StringBuilder sb, Settings settings)
        {
            Type objType = obj.GetType();

            if (objType.IsNullable() && obj is object o && o == null)
            {
                sb.Append("null");
                return;
            }

            bool isFirstField = true;
            sb.Append('{');

            bool deviatingType = expectedType != obj.GetType();
            if (settings.addAllTypeInfo || (settings.addDeviatingTypeInfo && deviatingType))
            {
                if (isFirstField) isFirstField = false;
                sb.Append($"\"$Type\":\"").Append(objType.FullName).Append('\"');
            }

            List<FieldInfo> fields;

            using (var lockHandle = fieldInfosLock.LockReadOnly())
            {
                if (!fieldInfos.TryGetValue(objType, out fields))
                {
                    lockHandle.UpgradeToWriteMode();

                    fields = new List<FieldInfo>();
                    if (settings.onlyPublicFields) fields.AddRange(objType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                    else fields.AddRange(objType.GetFields(BindingFlags.Public | BindingFlags.Instance));
                    Type t = objType.BaseType;
                    if (!settings.onlyPublicFields)
                    {
                        while (t != null)
                        {

                            fields.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !fields.Any(field => field.Name == baseField.Name)));
                            t = t.BaseType;
                        }
                    }
                    fieldInfos[objType] = fields;
                }
            }

            foreach (var field in fields)
            {
                if (isFirstField) isFirstField = false;
                else sb.Append(',');

                Action<object, FieldInfo, StringBuilder, Settings> writer;
                using (var lockHandle = fieldInfosLock.LockReadOnly())
                {
                    if (!fieldWriters.TryGetValue(field, out writer))
                    {
                        lockHandle.UpgradeToWriteMode();
                        writer = CreateFieldWriter(objType, field);
                        fieldWriters[field] = writer;
                    }
                }
                writer.Invoke(obj, field, sb, settings);
            }
            sb.Append("}");
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateFieldWriter(Type objType, FieldInfo field)
        {
            if (field.FieldType == typeof(string)) return CreateStringFieldWriter(field);
            else if (field.FieldType == typeof(int)) return CreateFieldWriter<int>(objType, field);
            else if (field.FieldType == typeof(uint)) return CreateFieldWriter<uint>(objType, field);
            else if (field.FieldType == typeof(byte)) return CreateFieldWriter<byte>(objType, field);
            else if (field.FieldType == typeof(sbyte)) return CreateFieldWriter<sbyte>(objType, field);
            else if (field.FieldType == typeof(short)) return CreateFieldWriter<short>(objType, field);
            else if (field.FieldType == typeof(ushort)) return CreateFieldWriter<ushort>(objType, field);
            else if (field.FieldType == typeof(long)) return CreateFieldWriter<long>(objType, field);
            else if (field.FieldType == typeof(ulong)) return CreateFieldWriter<ulong>(objType, field);
            else if (field.FieldType == typeof(bool)) return CreateFieldWriter<bool>(objType, field);
            else if (field.FieldType == typeof(char)) return CreateFieldWriter<char>(objType, field);
            else if (field.FieldType == typeof(float)) return CreateFieldWriter<float>(objType, field);
            else if (field.FieldType == typeof(double)) return CreateFieldWriter<double>(objType, field);
            else if (field.FieldType == typeof(IntPtr)) return CreateFieldWriter<IntPtr>(objType, field);
            else if (field.FieldType == typeof(UIntPtr)) return CreateFieldWriter<UIntPtr>(objType, field);
            else if (field.FieldType.IsAssignableTo(typeof(IEnumerable)) && 
                        (field.FieldType.IsAssignableTo(typeof(ICollection)) || 
                         field.FieldType.IsOfGenericType(typeof(ICollection<>))))
            {

                return CreateCollectionFieldWriter(field);
            }

            string fieldName = $"\"{field.Name}\":";
            Action<object, FieldInfo, StringBuilder, Settings> writer = (obj, field, sb, settings) =>
            {
                sb.Append(fieldName);
                var value = field.GetValue(obj);
                SerializeValue(value, field.FieldType, sb, settings);
            };

            return writer;
        }

        private static void SerializePrimitiveCollection<T>(IEnumerable<T> items, StringBuilder sb)
        {
            sb.Append('[');
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else sb.Append(',');
                sb.Append(item.ToString());
            }
            sb.Append(']');
        }

        private static void SerializeStringCollection(IEnumerable<string> items, StringBuilder sb)
        {
            sb.Append('[');
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else sb.Append(',');
                sb.Append('\"').WriteEscapedString(item).Append('\"');
            }
            sb.Append(']');
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateFieldWriter<T>(Type objType, FieldInfo field)
        {
            string fieldName = $"\"{field.Name}\":";

            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = Expression.Field(castedParameter, field);
            var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, FieldInfo, StringBuilder, Settings>  writer = (obj, field, sb, settings) =>
            {
                sb.Append(fieldName);
                var value = compiledGetter(obj);
                sb.Append(value.ToString());
            };

            return writer;
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateStringFieldWriter(FieldInfo field)
        {
            string fieldName = $"\"{field.Name}\":";

            Action<object, FieldInfo, StringBuilder, Settings> writer = (obj, field, sb, settings) =>
            {
                sb.Append(fieldName).Append('\"');
                var value = (string)field.GetValue(obj);
                sb.WriteEscapedString(value).Append('\"');
            };

            return writer;
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateCollectionFieldWriter<T>(FieldInfo field)
        {
            string fieldName = $"\"{field.Name}\":";

            Action<object, FieldInfo, StringBuilder, Settings> writer = (obj, field, sb, settings) =>
            {
                IEnumerable<T> items = (IEnumerable<T>)field.GetValue(obj);
                sb.Append(fieldName);
                sb.Append('[');
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else sb.Append(',');
                    sb.Append(item.ToString());
                }
                sb.Append(']');
            };

            return writer;
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateStringCollectionFieldWriter(FieldInfo field)
        {
            string fieldName = $"\"{field.Name}\":";

            Action<object, FieldInfo, StringBuilder, Settings> writer = (obj, field, sb, settings) =>
            {
                IEnumerable<string> items = (IEnumerable<string>)field.GetValue(obj);
                sb.Append(fieldName);
                sb.Append('[');
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else sb.Append(',');
                    sb.Append('\"').WriteEscapedString(item).Append('\"');
                }
                sb.Append(']');
            };

            return writer;
        }

        private static Action<object, FieldInfo, StringBuilder, Settings> CreateCollectionFieldWriter(FieldInfo field)
        {
            Type collectionType = field.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>));
            collectionType = collectionType ?? typeof(object);

            if (collectionType == typeof(string)) return CreateStringCollectionFieldWriter(field);
            else if (collectionType == typeof(int)) return CreateCollectionFieldWriter<int>(field);
            else if (collectionType == typeof(uint)) return CreateCollectionFieldWriter<uint>(field);
            else if (collectionType == typeof(byte)) return CreateCollectionFieldWriter<byte>(field);
            else if (collectionType == typeof(sbyte)) return CreateCollectionFieldWriter<sbyte>(field);
            else if (collectionType == typeof(short)) return CreateCollectionFieldWriter<short>(field);
            else if (collectionType == typeof(ushort)) return CreateCollectionFieldWriter<ushort>(field);
            else if (collectionType == typeof(long)) return CreateCollectionFieldWriter<long>(field);
            else if (collectionType == typeof(ulong)) return CreateCollectionFieldWriter<ulong>(field);
            else if (collectionType == typeof(bool)) return CreateCollectionFieldWriter<bool>(field);
            else if (collectionType == typeof(char)) return CreateCollectionFieldWriter<char>(field);
            else if (collectionType == typeof(float)) return CreateCollectionFieldWriter<float>(field);
            else if (collectionType == typeof(double)) return CreateCollectionFieldWriter<double>(field);
            else if (collectionType == typeof(IntPtr)) return CreateCollectionFieldWriter<IntPtr>(field);
            else if (collectionType == typeof(UIntPtr)) return CreateCollectionFieldWriter<UIntPtr>(field);

            string fieldName = $"\"{field.Name}\":";

            Action<object, FieldInfo, StringBuilder, Settings> writer = (obj, field, sb, settings) =>
            {
                IEnumerable items = (IEnumerable)field.GetValue(obj);
                sb.Append(fieldName);
                sb.Append('[');
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else sb.Append(',');
                    SerializeValue(item, collectionType, sb, settings);
                }
                sb.Append(']');
            };

            return writer;
        }

        private static StringBuilder WriteEscapedString(this StringBuilder sb, string str)
        {
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb;
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

            int iterations = 1_000_000;

            //object testDto = new TestDto(99, "World", new MyEmbedded1());
            object testDto = new TestDto();
            string json;
            var tk = AppTime.TimeKeeper;
            for (int i = 0; i < iterations; i++)
            {
                json = JsonSerializer.Serialize(testDto, opt);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());
            
            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                json = MyJsonSerializer.Serialize(testDto);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());

            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                json = FeatureLoom.Serialization.Json.SerializeToJson(testDto);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());

            tk.Restart();
            MyJsonSerializer.Settings settings = new MyJsonSerializer.Settings()
            {
                addDeviatingTypeInfo = false,
                addAllTypeInfo = false,
                onlyPublicFields = true,
            };
            for (int i = 0; i < iterations; i++)
            {
                json = MyJsonSerializer.Serialize<object>(testDto, settings);
            }
            Console.WriteLine(tk.Elapsed);
            GC.Collect();
            AppTime.Wait(1.Seconds());


            //var result = JsonSerializer.Deserialize<TestDto>(json);
            Console.ReadKey();
        }

    }
}
