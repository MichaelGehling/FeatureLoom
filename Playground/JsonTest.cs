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


    public static class MyJsonSerializer
    {
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.AlwaysReplaceByRef;
            public int bufferSize = -1;
        }

        public enum DataSelection
        {
            PublicAndPrivateFields = 0,
            PublicAndPrivateFields_CleanBackingFields = 1,
            PublicFieldsAndProperties = 2,
        }

        public enum ReferenceCheck
        {
            NoRefCheck = 0,
            OnLoopThrowException = 1,
            OnLoopReplaceByNull = 2,
            OnLoopReplaceByRef = 3,
            AlwaysReplaceByRef = 4
        }

        public enum TypeInfoHandling
        {
            AddNoTypeInfo = 0,
            AddDeviatingTypeInfo = 1,
            AddAllTypeInfo = 2,
        }

        public interface IJsonWriter
        {
            void CloseCollection();
            void CloseObject();
            void OpenCollection();
            void OpenObject();
            string ToString();
            void WriteComma();
            void WriteNullValue();
            void WritePreparedString(string str);
            void WritePrimitiveValue<T>(T value);
            void WriteRefObject(string refPath);
            void WriteStringValue(string str);
            void WriteTypeInfo(string typeName);
            void WriteValueFieldName();
        }

        public sealed class JsonStringWriter : IJsonWriter
        {
            private StringBuilder sb;

            public JsonStringWriter(int bufferSize)
            {
                this.sb = new StringBuilder(bufferSize);
            }

            public int UsedBuffer => sb.Length;

            public override string ToString() => sb.ToString();

            public void WriteNullValue() => sb.Append("null");
            public void OpenObject() => sb.Append("{");
            public void CloseObject() => sb.Append("}");
            public void WriteTypeInfo(string typeName) => sb.Append("\"$type\":\"").Append(typeName).Append("\",");
            public void WriteValueFieldName() => sb.Append("\"$value\":");
            public void WritePrimitiveValue<T>(T value) => sb.Append(value.ToString());
            public void WriteStringValue(string str) => sb.Append('\"').WriteEscapedString(str).Append('\"');
            public void WriteRefObject(string refPath) => sb.Append("{\"$ref\":\"").Append(refPath).Append("\"}");
            public void OpenCollection() => sb.Append("[");
            public void CloseCollection() => sb.Append("]");
            public void WriteComma() => sb.Append(",");
            public void WritePreparedString(string str) => sb.Append(str);
        }

        public sealed class JsonUTF8StreamWriter : IJsonWriter
        {
            private Stream stream;
            private StreamWriter stringWriter;


            public JsonUTF8StreamWriter(Stream stream)
            {
                this.stream = stream;
                this.stringWriter = new StreamWriter(stream, Encoding.UTF8);
            }

            public override string ToString() => stream.ReadToString();


            static readonly byte[] NULL = "null".ToByteArray();
            public void WriteNullValue() => stream.Write(NULL, 0, NULL.Length);

            static readonly byte[] OPEN_OBJECT = "{".ToByteArray();
            public void OpenObject() => stream.Write(OPEN_OBJECT, 0, OPEN_OBJECT.Length);

            static readonly byte[] CLOSE_OBJECT = "}".ToByteArray();
            public void CloseObject() => stream.Write(CLOSE_OBJECT, 0, CLOSE_OBJECT.Length);

            static readonly byte[] TYPEINFO_PRE = "\"$type\":\"".ToByteArray();
            static readonly byte[] TYPEINFO_POST = "\",".ToByteArray();
            public void WriteTypeInfo(string typeName)
            {
                stream.Write(TYPEINFO_PRE, 0, TYPEINFO_PRE.Length);
                stringWriter.Write(typeName);
                stream.Write(TYPEINFO_POST, 0, TYPEINFO_POST.Length);
            }

            static readonly byte[] VALUEFIELDNAME = "\"$value\":".ToByteArray();
            public void WriteValueFieldName() => stream.Write(VALUEFIELDNAME, 0, VALUEFIELDNAME.Length);


            public void WritePrimitiveValue<T>(T value) => stringWriter.Write(value.ToString());

            static readonly byte[] STRINGVALUE_PRE = "\"".ToByteArray();
            static readonly byte[] STRINGVALUE_POST = "\"".ToByteArray();
            public void WriteStringValue(string str)
            {
                stream.Write(STRINGVALUE_PRE, 0,  STRINGVALUE_PRE.Length);
                stringWriter.Write(str);
                stream.Write(STRINGVALUE_POST, 0, STRINGVALUE_POST.Length);
            }

            static readonly byte[] REFOBJECT_PRE = "{\"$ref\":\"".ToByteArray();
            static readonly byte[] REFOBJECT_POST = "\"}".ToByteArray();
            public void WriteRefObject(string refPath)
            {
                stream.Write(REFOBJECT_PRE, 0, REFOBJECT_PRE.Length);
                stringWriter.Write(refPath);
                stream.Write(REFOBJECT_POST, 0, REFOBJECT_POST.Length);
            }

            static readonly byte[] OPENCOLLECTION = "[".ToByteArray();
            public void OpenCollection() => stream.Write(OPENCOLLECTION, 0, OPENCOLLECTION.Length);

            static readonly byte[] CLOSECOLLECTION = "]".ToByteArray();
            public void CloseCollection() => stream.Write(CLOSECOLLECTION, 0, CLOSECOLLECTION.Length);

            static readonly byte[] COMMA = ",".ToByteArray();
            public void WriteComma() => stream.Write(COMMA, 0, COMMA.Length);

            public void WritePreparedString(string str) => stringWriter.Write(str);
        }

        private struct Crawler
        {
            public IJsonWriter writer;
            public Settings settings;
            public string currentPath;
            public Dictionary<object, string> pathMap;
            public string refPath;

            public static Crawler Root(object rootObj, Settings settings, IJsonWriter writer)
            {

                if (settings.referenceCheck == ReferenceCheck.NoRefCheck)
                {
                    return new Crawler() 
                    { 
                        settings = settings, 
                        writer = writer
                    };
                }

                return new Crawler()
                {
                    settings = settings,
                    currentPath = "$",
                    pathMap = new Dictionary<object, string>(){{rootObj, "$"}},
                    writer = writer
                };
            }

            public Crawler NewChild(object child, string name)
            {
                if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return this;

                string childRefPath = null;
                string childPath = currentPath + "." + name;
                if (child != null && child.GetType().IsClass && !(child is string))
                {
                    childRefPath = FindObjRefPath(child);
                    pathMap[child] = childPath;
                }

                return new Crawler()
                {
                    settings = settings,
                    currentPath = childPath,
                    pathMap = pathMap,
                    refPath = childRefPath,
                    writer = writer
                };
            }

            public Crawler NewCollectionItem(object item, string fieldName, int index)
            {
                if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return this;

                string childRefPath = null;
                string childPath = $"{currentPath}.{fieldName}[{index}]";
                if (item != null && item.GetType().IsClass && !(item is string))
                {
                    childRefPath = FindObjRefPath(item);
                    pathMap[item] = childPath;
                }

                return new Crawler()
                {
                    settings = settings,
                    currentPath = childPath,
                    pathMap = pathMap,
                    refPath = childRefPath,
                    writer = writer
                };
            }

            private string FindObjRefPath(object obj)
            {
                if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return null;                
                if (pathMap.TryGetValue(obj, out string path)) return path;
                else return null;
            }
        }

        static Settings defaultSettings = new Settings();

        private class TypeCache
        {
            public Dictionary<Type, List<MemberInfo>> memberInfos = new Dictionary<Type, List<MemberInfo>>();
            public Dictionary<MemberInfo, Action<object, MemberInfo, Crawler>> fieldWriters = new Dictionary<MemberInfo, Action<object, MemberInfo, Crawler>>();
            public Dictionary<Type, int> typeBufferInfo = new Dictionary<Type, int>();
        }
        static TypeCache[] typeCaches = InitTypeCaches();
        static FeatureLock typeCacheLock = new FeatureLock();

        private static TypeCache[] InitTypeCaches()
        {
            int numTypeCaches = Enum.GetValues(typeof(DataSelection)).Length;
            TypeCache[] typeCaches = new TypeCache[numTypeCaches];
            for (int i = 0; i < typeCaches.Length; i++)
            {
                typeCaches[i] = new TypeCache();
            }
            return typeCaches;
        }

        public static string Serialize<T>(T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;            

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;


            TypeCache typeCache = typeCaches[settings.dataSelection.ToInt()];
            Type objType = obj.GetType();

            bool autoBufferSize = false;
            if (settings.bufferSize < 0)
            {
                autoBufferSize = true;
                settings.bufferSize = 64;
                using (typeCacheLock.LockReadOnly())
                {
                    if (typeCache.typeBufferInfo.TryGetValue(objType, out var b)) settings.bufferSize = b;
                }
            }


            var writer = new JsonStringWriter(settings.bufferSize);
            Crawler crawler = Crawler.Root(obj, settings, writer);
            SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;

            if (autoBufferSize && writer.UsedBuffer > settings.bufferSize)
            {
                using (typeCacheLock.Lock())
                {
                    typeCache.typeBufferInfo[objType] = writer.UsedBuffer;
                }
            }
            return crawler.ToString();
        }

        public static byte[] SerializeToUtf8Bytes<T>(T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;


            TypeCache typeCache = typeCaches[settings.dataSelection.ToInt()];
            Type objType = obj.GetType();

            bool autoBufferSize = false;
            if (settings.bufferSize < 0)
            {
                autoBufferSize = true;
                settings.bufferSize = 64;
                using (typeCacheLock.LockReadOnly())
                {
                    if (typeCache.typeBufferInfo.TryGetValue(objType, out var b)) settings.bufferSize = b;
                }
            }

            MemoryStream stream = new MemoryStream(settings.bufferSize);
            var writer = new JsonUTF8StreamWriter(stream);
            Crawler crawler = Crawler.Root(obj, settings, writer);
            SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;

            if (autoBufferSize && stream.Length > settings.bufferSize)
            {
                using (typeCacheLock.Lock())
                {
                    typeCache.typeBufferInfo[objType] = (int)stream.Length;
                }
            }
            return stream.GetBuffer();
        }

        public static void Serialize<T>(Stream stream, T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var writer = new JsonUTF8StreamWriter(stream);
            Crawler crawler = Crawler.Root(obj, settings, writer);
            SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;
        }

        private static void SerializeValue(object obj, Type expectedType, Crawler crawler)
        {
            if (obj == null)
            {
                crawler.writer.WriteNullValue();
                return;
            }            
            
            Type objType = obj.GetType();
            bool deviatingType = expectedType != obj.GetType();

            if (objType.IsPrimitive)
            {
                PrepareUnexpectedValue(crawler, deviatingType, objType);
                crawler.writer.WritePrimitiveValue(obj);
                FinishUnexpectedValue(crawler, deviatingType);
                return;
            }
            if (obj is string str)
            {
                PrepareUnexpectedValue(crawler, deviatingType, objType);
                crawler.writer.WriteStringValue(str);
                FinishUnexpectedValue(crawler, deviatingType);
                return;
            }

            if (crawler.refPath != null)
            {
                if (crawler.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
                {
                    crawler.writer.WriteRefObject(crawler.refPath);
                    return;
                }
                else if (crawler.currentPath.StartsWith(crawler.refPath))
                {
                    if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef)
                    {
                        crawler.writer.WriteRefObject(crawler.refPath);
                        return;
                    }
                    if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull)
                    {
                        crawler.writer.WriteNullValue();
                        return;
                    }
                    if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopThrowException)
                    {
                        throw new Exception("Circular referencing detected!");
                    }
                }
            }

            if (obj is IEnumerable items && (obj is ICollection || objType.ImplementsGenericInterface(typeof(ICollection<>))))
            {
                PrepareUnexpectedValue(crawler, deviatingType, objType);
                SerializeCollection(objType, items, crawler);
                FinishUnexpectedValue(crawler, deviatingType);
                return;
            }

            SerializeComplexType(obj, expectedType, crawler);
            return;
        }

        static void PrepareUnexpectedValue(Crawler crawler, bool deviatingType, Type objType)
        {
            if (crawler.settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (crawler.settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
            {
                crawler.writer.OpenObject();
                crawler.writer.WriteTypeInfo(objType.FullName);
                crawler.writer.WriteValueFieldName();
            }
        }

        static void FinishUnexpectedValue(Crawler crawler, bool deviatingType)
        {
            if (crawler.settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (crawler.settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
            {
                crawler.writer.CloseObject();
            }
        }




        private static void SerializeCollection(Type objType, IEnumerable items, Crawler crawler)
        {

            if (items is IEnumerable<string> string_items) SerializeStringCollection(string_items, crawler);
            else if (items is IEnumerable<int> int_items) SerializePrimitiveCollection(int_items, crawler);
            else if (items is IEnumerable<uint> uint_items) SerializePrimitiveCollection(uint_items, crawler);
            else if (items is IEnumerable<byte> byte_items) SerializePrimitiveCollection(byte_items, crawler);
            else if (items is IEnumerable<sbyte> sbyte_items) SerializePrimitiveCollection(sbyte_items, crawler);
            else if (items is IEnumerable<short> short_items) SerializePrimitiveCollection(short_items, crawler);
            else if (items is IEnumerable<ushort> ushort_items) SerializePrimitiveCollection(ushort_items, crawler);
            else if (items is IEnumerable<long> long_items) SerializePrimitiveCollection(long_items, crawler);
            else if (items is IEnumerable<ulong> ulong_items) SerializePrimitiveCollection(ulong_items, crawler);
            else if (items is IEnumerable<bool> bool_items) SerializePrimitiveCollection(bool_items, crawler);
            else if (items is IEnumerable<char> char_items) SerializePrimitiveCollection(char_items, crawler);
            else if (items is IEnumerable<float> float_items) SerializePrimitiveCollection(float_items, crawler);
            else if (items is IEnumerable<double> double_items) SerializePrimitiveCollection(double_items, crawler);
            else if (items is IEnumerable<IntPtr> intPtr_items) SerializePrimitiveCollection(intPtr_items, crawler);
            else if (items is IEnumerable<UIntPtr> uIntPtr_items) SerializePrimitiveCollection(uIntPtr_items, crawler);
            else 
            {                
                Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>));
                collectionType = collectionType ?? typeof(object);

                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                int index = 0;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    if (item != null && !item.GetType().IsPrimitive && !(item is string))
                    {
                        SerializeValue(item, collectionType, crawler.NewCollectionItem(item, "", index++));
                    }
                    else
                    {
                        SerializeValue(item, collectionType, crawler);
                    }
                }
                crawler.writer.CloseCollection();
            }
        }

        private static void SerializeComplexType(object obj, Type expectedType, Crawler crawler)
        {
            var settings = crawler.settings;

            Type objType = obj.GetType();

            if (objType.IsNullable() && obj == null)
            {
                crawler.writer.WriteNullValue();
                return;
            }

            bool isFirstField = true;
            crawler.writer.OpenObject();

            bool deviatingType = expectedType != obj.GetType();
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
            {
                if (isFirstField) isFirstField = false;
                crawler.writer.WriteTypeInfo(objType.FullName);
            }

            TypeCache typeCache = typeCaches[settings.dataSelection.ToInt()];
            List<MemberInfo> members;
            using (var lockHandle = typeCacheLock.LockReadOnly())
            {
                if (!typeCache.memberInfos.TryGetValue(objType, out members))
                {
                    lockHandle.UpgradeToWriteMode();

                    members = new List<MemberInfo>();
                    if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
                    {
                        members.AddRange(objType.GetFields(BindingFlags.Public | BindingFlags.Instance));
                        members.AddRange(objType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.GetMethod != null));
                    }
                    else
                    {
                        members.AddRange(objType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                        Type t = objType.BaseType;
                        while (t != null)
                        {
                            members.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !members.Any(field => field.Name == baseField.Name)));
                            t = t.BaseType;
                        }
                    }
                    typeCache.memberInfos[objType] = members;
                }
            }

            foreach (var field in members)
            {
                if (isFirstField) isFirstField = false;
                else crawler.writer.WriteComma();

                Action<object, MemberInfo, Crawler> writer;
                using (var lockHandle = typeCacheLock.LockReadOnly())
                {
                    if (!typeCache.fieldWriters.TryGetValue(field, out writer))
                    {
                        lockHandle.UpgradeToWriteMode();
                        writer = CreateFieldWriter(objType, field, settings);
                        typeCache.fieldWriters[field] = writer;
                    }
                }
                writer.Invoke(obj, field, crawler);
            }
            crawler.writer.CloseObject();
        }

        private static Action<object, MemberInfo, Crawler> CreateFieldWriter(Type objType, MemberInfo member, Settings settings)
        {
            Type memberType = member is FieldInfo field ? field.FieldType : member is PropertyInfo property ? property.PropertyType : default;

            if (memberType == typeof(string)) return CreateStringFieldWriter(objType, member, settings);
            else if (memberType == typeof(int)) return CreatePrimitiveFieldWriter<int>(objType, member, settings);
            else if (memberType == typeof(uint)) return CreatePrimitiveFieldWriter<uint>(objType, member, settings);
            else if (memberType == typeof(byte)) return CreatePrimitiveFieldWriter<byte>(objType, member, settings);
            else if (memberType == typeof(sbyte)) return CreatePrimitiveFieldWriter<sbyte>(objType, member, settings);
            else if (memberType == typeof(short)) return CreatePrimitiveFieldWriter<short>(objType, member, settings);
            else if (memberType == typeof(ushort)) return CreatePrimitiveFieldWriter<ushort>(objType, member, settings);
            else if (memberType == typeof(long)) return CreatePrimitiveFieldWriter<long>(objType, member, settings);
            else if (memberType == typeof(ulong)) return CreatePrimitiveFieldWriter<ulong>(objType, member, settings);
            else if (memberType == typeof(bool)) return CreatePrimitiveFieldWriter<bool>(objType, member, settings);
            else if (memberType == typeof(char)) return CreatePrimitiveFieldWriter<char>(objType, member, settings);
            else if (memberType == typeof(float)) return CreatePrimitiveFieldWriter<float>(objType, member, settings);
            else if (memberType == typeof(double)) return CreatePrimitiveFieldWriter<double>(objType, member, settings);
            else if (memberType == typeof(IntPtr)) return CreatePrimitiveFieldWriter<IntPtr>(objType, member, settings);
            else if (memberType == typeof(UIntPtr)) return CreatePrimitiveFieldWriter<UIntPtr>(objType, member, settings);
            else if (memberType.IsAssignableTo(typeof(IEnumerable)) && 
                        (memberType.IsAssignableTo(typeof(ICollection)) || 
                         memberType.IsOfGenericType(typeof(ICollection<>))))
            {

                return CreateCollectionFieldWriter(member, settings);
            }

            string fieldName = PrepareFieldName(member, settings);

            Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
            {
                crawler.writer.WritePreparedString(fieldName);
                var (memberType, value) = member is FieldInfo field ? (field.FieldType, field.GetValue(obj)) : 
                                          member is PropertyInfo property ? (property.PropertyType, property.GetValue(obj)) : 
                                          default;
                SerializeValue(value, memberType, crawler.NewChild(value, member.Name));
            };

            return writer;
        }

        private static void SerializePrimitiveCollection<T>(IEnumerable<T> items, Crawler crawler)
        {
            crawler.writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else crawler.writer.WriteComma();
                crawler.writer.WritePrimitiveValue(item);
            }
            crawler.writer.CloseCollection();
        }

        private static void SerializeStringCollection(IEnumerable<string> items, Crawler crawler)
        {
            crawler.writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else crawler.writer.WriteComma();
                crawler.writer.WriteStringValue(item);
            }
            crawler.writer.CloseCollection();
        }

        private static Action<object, MemberInfo, Crawler> CreatePrimitiveFieldWriter<T>(Type objType, MemberInfo member, Settings settings)
        {
            string fieldName = PrepareFieldName(member, settings);

            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = member is FieldInfo field ? Expression.Field(castedParameter, field) : member is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, MemberInfo, Crawler>  writer = (obj, member, crawler) =>
            {
                var value = compiledGetter(obj);
                //var value = (T) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                crawler.writer.WritePreparedString(fieldName);                
                if (value != null) PrepareUnexpectedValue(crawler, false, value.GetType());
                crawler.writer.WritePrimitiveValue(value);
                if (value != null) FinishUnexpectedValue(crawler, false);
            };

            return writer;
        }

        private static Action<object, MemberInfo, Crawler> CreateStringFieldWriter(Type objType, MemberInfo member, Settings settings)
        {
            string fieldName = PrepareFieldName(member, settings);

            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = member is FieldInfo field ? Expression.Field(castedParameter, field) : member is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, string>>(fieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
            {
                var value = compiledGetter(obj);
                //var value = (string) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                crawler.writer.WritePreparedString(fieldName);
                if (value != null) PrepareUnexpectedValue(crawler, false, value.GetType());
                crawler.writer.WriteStringValue(value);
                FinishUnexpectedValue(crawler, false);
            };

            return writer;
        }

        private static Action<object, MemberInfo, Crawler> CreatePrimitiveCollectionFieldWriter<T>(MemberInfo member, Settings settings)
        {
            string fieldName = PrepareFieldName(member, settings);

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<T>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<T>>>(castFieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
            {
                //IEnumerable<T> items = (IEnumerable<T>) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                IEnumerable<T> items = compiledGetter(obj);
                crawler.writer.WritePreparedString(fieldName);
                PrepareUnexpectedValue(crawler, false, items.GetType());
                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    PrepareUnexpectedValue(crawler, false, item.GetType());
                    crawler.writer.WritePrimitiveValue(item);
                    FinishUnexpectedValue(crawler, false);
                }
                crawler.writer.CloseCollection();
                FinishUnexpectedValue(crawler, false);
            };

            return writer;
        }

        private static Action<object, MemberInfo, Crawler> CreateStringCollectionFieldWriter(MemberInfo member, Settings settings)
        {
            string fieldName = PrepareFieldName(member, settings);

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<string>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<string>>>(castFieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
            {
                //IEnumerable<string> items = (IEnumerable<string>)(member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                IEnumerable<string> items = compiledGetter(obj);
                crawler.writer.WritePreparedString(fieldName);
                PrepareUnexpectedValue(crawler, false, items.GetType());
                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    PrepareUnexpectedValue(crawler, false, item.GetType());
                    crawler.writer.WriteStringValue(item);
                    FinishUnexpectedValue(crawler, false);
                }
                crawler.writer.CloseCollection();
                FinishUnexpectedValue(crawler, false);
            };

            return writer;
        }

        private static Action<object, MemberInfo, Crawler> CreateCollectionFieldWriter(MemberInfo member, Settings settings)
        {
            Type collectionType = member is FieldInfo field ? field.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  member is PropertyInfo property ? property.PropertyType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  default;
            collectionType = collectionType ?? typeof(object);

            if (collectionType == typeof(string)) return CreateStringCollectionFieldWriter(member, settings);
            else if (collectionType == typeof(int)) return CreatePrimitiveCollectionFieldWriter<int>(member, settings);
            else if (collectionType == typeof(uint)) return CreatePrimitiveCollectionFieldWriter<uint>(member, settings);
            else if (collectionType == typeof(byte)) return CreatePrimitiveCollectionFieldWriter<byte>(member, settings);
            else if (collectionType == typeof(sbyte)) return CreatePrimitiveCollectionFieldWriter<sbyte>(member, settings);
            else if (collectionType == typeof(short)) return CreatePrimitiveCollectionFieldWriter<short>(member, settings);
            else if (collectionType == typeof(ushort)) return CreatePrimitiveCollectionFieldWriter<ushort>(member, settings);
            else if (collectionType == typeof(long)) return CreatePrimitiveCollectionFieldWriter<long>(member, settings);
            else if (collectionType == typeof(ulong)) return CreatePrimitiveCollectionFieldWriter<ulong>(member, settings);
            else if (collectionType == typeof(bool)) return CreatePrimitiveCollectionFieldWriter<bool>(member, settings);
            else if (collectionType == typeof(char)) return CreatePrimitiveCollectionFieldWriter<char>(member, settings);
            else if (collectionType == typeof(float)) return CreatePrimitiveCollectionFieldWriter<float>(member, settings);
            else if (collectionType == typeof(double)) return CreatePrimitiveCollectionFieldWriter<double>(member, settings);
            else if (collectionType == typeof(IntPtr)) return CreatePrimitiveCollectionFieldWriter<IntPtr>(member, settings);
            else if (collectionType == typeof(UIntPtr)) return CreatePrimitiveCollectionFieldWriter<UIntPtr>(member, settings);
            
            string fieldName = PrepareFieldName(member, settings);

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable));
            var lambda = Expression.Lambda<Func<object, IEnumerable>>(castFieldAccess, parameter);
            var compiledGetter = lambda.Compile();

            Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
            {
                //IEnumerable items = (IEnumerable)(member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                IEnumerable items = compiledGetter(obj);
                crawler.writer.WritePreparedString(fieldName);
                PrepareUnexpectedValue(crawler, false, items.GetType());
                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                int index = 0;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    if (item != null && !item.GetType().IsPrimitive && !(item is string))
                    {
                        SerializeValue(item, collectionType, crawler.NewCollectionItem(item, member.Name, index));
                    }
                    else
                    {
                        SerializeValue(item, collectionType, crawler);
                    }
                    index++;
                }
                crawler.writer.CloseCollection();
                FinishUnexpectedValue(crawler, false);
            };

            return writer;
        }

        private static string PrepareFieldName(MemberInfo member, Settings settings)
        {
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields && 
                member.Name.StartsWith('<') && 
                member.Name.EndsWith(">k__BackingField")) return $"\"{member.Name.Substring("<", ">")}\":";

            return $"\"{member.Name}\":";
        }

        public static StringBuilder WriteEscapedString(this StringBuilder sb, string str)
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

            int iterations = 3_000_000;

            //var testDto = new TestDto(99, new MyEmbedded1());
            var testDto = new TestDto2();
            Type testDtoType = testDto.GetType();
            //string json;
            byte[] json;

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

            tk.Restart();
            for (int i = 0; i < iterations; i++)
            {
                //json = FeatureLoom.Serialization.Json.SerializeToJson(testDto).ToByteArray();                
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
