using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization;

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

}