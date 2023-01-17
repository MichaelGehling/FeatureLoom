using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization
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
            using (var sw = new StreamWriter(jsonStream))
            using (var writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, obj);
            }
        }

        public static T FromJson<T>(this Stream jsonStream, JsonSerializer serializer = null)
        {
            serializer = serializer ?? Json.Default_Serializer;
            T obj;
            using (StreamReader sr = new StreamReader(jsonStream))
            using (JsonTextReader jsonReader = new JsonTextReader(sr))
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

        public static bool TryDeserializeXml<T>(this Stream stream, out T xmlObject)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    xmlObject = (T)serializer.Deserialize(reader);
                }
                return true;
            }
            catch
            {
                xmlObject = default;
                return false;
            }
        }

        public static bool TrySerializeToXmlElement<T>(this T obj, out XmlElement xmlElement)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                XmlDocument doc = new XmlDocument();

                using (XmlWriter writer = doc.CreateNavigator().AppendChild())
                {
                    serializer.Serialize(writer, obj);
                }

                xmlElement = doc.DocumentElement;
                return true;
            }
            catch
            {
                xmlElement = null;
                return false;
            }
        }
    }
}