using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FeatureLoom.Serialization
{
    public interface IJsonSerializationSupport
    {
        void PrepareSerialization();

        void FinishDeserialization();

        void PrepareUpdate();

        void FinishUpdate();

        JsonSerializerSettings JsonSerializerSettings { get; }
    }

    public static class Json
    {
        private static JsonSerializerSettings default_SerializerSettings;
        private static JsonSerializerSettings complexObjectsStructure_SerializerSettings;

        public static JsonSerializerSettings Default_SerializerSettings
        {
            get
            {
                if (Json.default_SerializerSettings == null)
                {
                    default_SerializerSettings = new JsonSerializerSettings();
                    //convert Enums to Strings (instead of Integer) globally
                    default_SerializerSettings.Converters.Add(new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() });
                    //default_SerializerSettings.ContractResolver = new MyContractResolver();
                    default_SerializerSettings.Formatting = Formatting.Indented;
                    default_SerializerSettings.TypeNameHandling = TypeNameHandling.None;
                    default_SerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    default_SerializerSettings.ContractResolver = new MyContractResolver();
                    /*default_SerializerSettings.Error = (sender, args) =>
                    {
                        Log.WARNING($"Serializing or deserializing caused an error: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    };*/
                }
                return Json.default_SerializerSettings;
            }

            set => default_SerializerSettings = value;
        }

        private static JsonSerializer default_Serializer;

        public static JsonSerializer Default_Serializer
        {
            get
            {
                if (Json.default_Serializer == null)
                {
                    default_Serializer = JsonSerializer.Create(Default_SerializerSettings);
                }
                return default_Serializer;
            }
        }

        private static JsonSerializer complexObjectsStructure_Serializer;

        public static JsonSerializer ComplexObjectsStructure_Serializer
        {
            get
            {
                if (Json.complexObjectsStructure_Serializer == null)
                {
                    complexObjectsStructure_Serializer = JsonSerializer.Create(ComplexObjectsStructure_SerializerSettings);
                }
                return complexObjectsStructure_Serializer;
            }
        }

        public static JsonSerializerSettings ComplexObjectsStructure_SerializerSettings
        {
            get
            {
                if (Json.complexObjectsStructure_SerializerSettings == null)
                {
                    complexObjectsStructure_SerializerSettings = new JsonSerializerSettings();
                    //convert Enums to Strings (instead of Integer) globally
                    complexObjectsStructure_SerializerSettings.Converters.Add(new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() });
                    complexObjectsStructure_SerializerSettings.ContractResolver = new MyContractResolver();
                    complexObjectsStructure_SerializerSettings.Formatting = Formatting.Indented;
                    complexObjectsStructure_SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.All;
                    complexObjectsStructure_SerializerSettings.TypeNameHandling = TypeNameHandling.Objects;
                    complexObjectsStructure_SerializerSettings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                    complexObjectsStructure_SerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    complexObjectsStructure_SerializerSettings.Error = (sender, args) =>
                    {
                        Log.WARNING($"Serializing or deserializing caused an error: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    };
                }
                return Json.complexObjectsStructure_SerializerSettings;
            }

            set => complexObjectsStructure_SerializerSettings = value;
        }

        public static JsonSerializer Clone(this JsonSerializer serializer)
        {
            var copiedSerializer = new JsonSerializer
            {
                Context = serializer.Context,
                Culture = serializer.Culture,
                ContractResolver = serializer.ContractResolver,
                ConstructorHandling = serializer.ConstructorHandling,
                CheckAdditionalContent = serializer.CheckAdditionalContent,
                DateFormatHandling = serializer.DateFormatHandling,
                DateFormatString = serializer.DateFormatString,
                DateParseHandling = serializer.DateParseHandling,
                DateTimeZoneHandling = serializer.DateTimeZoneHandling,
                DefaultValueHandling = serializer.DefaultValueHandling,
                EqualityComparer = serializer.EqualityComparer,
                FloatFormatHandling = serializer.FloatFormatHandling,
                Formatting = serializer.Formatting,
                FloatParseHandling = serializer.FloatParseHandling,
                MaxDepth = serializer.MaxDepth,
                MetadataPropertyHandling = serializer.MetadataPropertyHandling,
                MissingMemberHandling = serializer.MissingMemberHandling,
                NullValueHandling = serializer.NullValueHandling,
                ObjectCreationHandling = serializer.ObjectCreationHandling,
                PreserveReferencesHandling = serializer.PreserveReferencesHandling,
                ReferenceResolver = serializer.ReferenceResolver,
                ReferenceLoopHandling = serializer.ReferenceLoopHandling,
                StringEscapeHandling = serializer.StringEscapeHandling,
                TraceWriter = serializer.TraceWriter,
                TypeNameHandling = serializer.TypeNameHandling,
                SerializationBinder = serializer.SerializationBinder,
                TypeNameAssemblyFormatHandling = serializer.TypeNameAssemblyFormatHandling
            };
            foreach (var converter in serializer.Converters)
            {
                copiedSerializer.Converters.Add(converter);
            }
            return copiedSerializer;
        }

        public static string SerializeToJson(object obj, JsonSerializerSettings settings = null)
        {
            if (settings == null && obj is IJsonSerializationSupport) settings = (obj as IJsonSerializationSupport).JsonSerializerSettings;
            if (settings == null) settings = Default_SerializerSettings;
            Type type = obj.GetType();
            string json = JsonConvert.SerializeObject(obj, type, settings);
            return json;
        }

        public static T DeserializeFromJson<T>(string json, JsonSerializerSettings settings = null)
        {
            if (settings == null) settings = Default_SerializerSettings;
            T obj = JsonConvert.DeserializeObject<T>(json, settings);
            return obj;
        }

        public static object DeserializeFromJson(string json, Type type, JsonSerializerSettings settings = null)
        {
            if (settings == null) settings = Default_SerializerSettings;
            var obj = JsonConvert.DeserializeObject(json, type, settings);
            return obj;
        }

        public static void UpdateFromJson<T>(T obj, string json, JsonSerializerSettings settings = null) where T : class
        {
            if (settings == null && obj is IJsonSerializationSupport) settings = (obj as IJsonSerializationSupport).JsonSerializerSettings;
            if (settings == null) settings = Default_SerializerSettings;
            JsonConvert.PopulateObject(json, obj, settings);
        }

        public static string FixJsonStr(string str, bool forceArrayOfObjects = false)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string line in str.Split('\r', '\n'))
            {
                string myLine = line;
                myLine = myLine.Trim();
                if (myLine == "") continue;
                if (myLine.StartsWith("//")) continue;
                builder.Append(myLine);
                if (myLine != "{" &&
                    !myLine.EndsWith(",") &&
                    !myLine.EndsWith("[")) builder.Append(",");
                builder.Append(Environment.NewLine);
            }
            str = builder.ToString();
            str = str.Trim();
            str = str.TrimChar(',');
            if (!str.StartsWith("{") && !str.StartsWith("[")) str = "{" + Environment.NewLine + str;
            if (!str.EndsWith("}") && !str.EndsWith("]")) str = str + Environment.NewLine + "}";

            if (forceArrayOfObjects)
            {
                if (!str.StartsWith("[")) str = "[" + Environment.NewLine + str;
                if (!str.EndsWith("]")) str = str + Environment.NewLine + "]";
            }

            return str;
        }

        public static JsonSerializerSettings CreateSettingsWithTypeInfo(params Type[] allowedTypes)
        {
            var settings = new JsonSerializerSettings();
            //convert Enums to Strings (instead of Integer) globally
            settings.Converters.Add(new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() });
            settings.ContractResolver = new MyContractResolver();
            settings.Formatting = Formatting.Indented;
            settings.TypeNameHandling = TypeNameHandling.Objects;
            settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.SerializationBinder = new KnownTypesBinder
            {
                KnownTypes = new List<Type>(allowedTypes)
            };

            return settings;
        }

        public class KnownTypesBinder : ISerializationBinder
        {
            public IList<Type> KnownTypes { get; set; }

            public Type BindToType(string assemblyName, string typeName)
            {
                return KnownTypes.SingleOrDefault(t => t.Name == typeName);
            }

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                assemblyName = null;
                typeName = serializedType.Name;
            }
        }

        public static bool TryParseJson<T>(this string json, out T result)
        {
            /*
            bool success = true;
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) => { success = false; args.ErrorContext.Handled = true; },
                MissingMemberHandling = MissingMemberHandling.Error
            };
            result = JsonConvert.DeserializeObject<T>(json, settings);
            return success;
            */
            try
            {
                result = json.FromJson<T>();
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public class MyContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => base.CreateProperty(p, memberSerialization))
                            .Union(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => base.CreateProperty(f, memberSerialization)))
                            .ToList();
                props.ForEach(p => { p.Writable = true; p.Readable = true; });

                return props;
            }
        }
    }    
}