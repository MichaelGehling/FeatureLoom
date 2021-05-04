using FeatureLoom.Helpers.Misc;
using FeatureLoom.Services.Serialization;
using Newtonsoft.Json;
using System.IO;
using System.Xml;

namespace FeatureLoom.Helpers.Extensions
{
    public static class SerializationExtensions
    {
        public static string ToJson<T>(this T obj, JsonSerializerSettings settings = null)
        {
            return Json.SerializeToJson(obj, settings);
        }

        public static T FromJson<T>(this string json, JsonSerializerSettings settings = null)
        {
            return Json.DeserializeFromJson<T>(json, settings);
        }

        public static void ToJson<T>(this T obj, Stream jsonStream, JsonSerializer serializer = null)
        {
            serializer = serializer ?? Json.Default_Serializer;
            using(var sw = new StreamWriter(jsonStream))
            using(var writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, obj);
            }
        }

        public static T FromJson<T>(this Stream jsonStream, JsonSerializer serializer = null)
        {
            serializer = serializer ?? Json.Default_Serializer;
            T obj;
            using(StreamReader sr = new StreamReader(jsonStream))
            using(JsonTextReader jsonReader = new JsonTextReader(sr))
            {
                obj = serializer.Deserialize<T>(jsonReader);
            }
            return obj;
        }

        public static void UpdateFromJson<T>(this T obj, string json, JsonSerializerSettings settings = null) where T : class
        {
            Json.UpdateFromJson(obj, json, settings);
        }

        public static XmlElement ToXmlElement(this string xml, XmlDocument xmlDoc)
        {
            if (xmlDoc == null) xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return xmlDoc.DocumentElement;
        }
    }
}