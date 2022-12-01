using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Serialization
{
    public class AdaptableJsonSerializer : ISerializer, IDeserializer
    {

        private bool TryJTokenToObj<T>(JToken jToken, out T obj)
        {
            var result = jToken.ToObject<T>();
            if (result != null)
            {
                obj = result;
                return true;
            }
            else
            {
                obj = default;
                return false;
            }
        }

        public bool TryDeserialize<T>(byte[] data, out T obj)
        {
            string jsonStr = data.GetString();
            return TryDeserialize(jsonStr, out obj);
        }

        public bool TryDeserialize<T>(string data, out T obj)
        {
            try
            {
                var jToken = JToken.Parse(data);
                return TryJTokenToObj(jToken, out obj);
            }
            catch(Exception e)
            {
                Log.WARNING(this.GetHandle(), $"Failed deserializing to JSON", e.ToString());
                obj = default;
                return false;
            }
        }

        public async Task<(bool, T)> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                using (TextReader textReader = new StreamReader(stream))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    var jToken = await JToken.LoadAsync(jsonReader, cancellationToken);
                    if (TryJTokenToObj(jToken, out T obj)) return (true, obj);
                    else return (false, default);
                }
            }
            catch (Exception e)
            {
                Log.WARNING(this.GetHandle(), $"Failed reading JSON from Stream", e.ToString());
                return (false, default);
            }
        }

        public bool TrySerialize<T>(T obj, out string data)
        {
            throw new NotImplementedException();
        }

        public bool TrySerialize<T>(T obj, out byte[] data)
        {
            throw new NotImplementedException();
        }

        public bool TrySerializeToStreamAsync<T>(T obj, Stream stream)
        {
            throw new NotImplementedException();
        }
#if NETSTANDARD2_1_OR_GREATER
        public bool TryDeserialize<T>(ReadOnlySpan<char> data, out T obj)
        {
            throw new NotImplementedException();
        }

        public bool TryDeserialize<T>(ReadOnlySpan<byte> data, out T obj)
        {
            throw new NotImplementedException();
        }
#endif
    }

    /*
    public class UnivarsalJsonSerializer
    {
        private JsonSerializer serializer;

        public UnivarsalJsonSerializer()
        {
            InitSerializer();
        }

        private void InitSerializer()
        {
            var settings = new JsonSerializerSettings();
            //TODO setup settings
            serializer = JsonSerializer.Create(settings);
        }

        public bool TryDeserialize<T>(byte[] data, out T obj)
        {
            try
            {
                string jsonString = Encoding.UTF8.GetString(data);
                obj = Json.DeserializeFromJson<T>(jsonString);
                return true;
            }
            catch (Exception e)
            {
                Log.WARNING(this.GetHandle(), $"GenericJsonSerializer failed to deserialize object from byte[]!", e.ToString());
                obj = default;
                return false;
            }
        }

        public bool TryDeserialize<T>(string data, out T obj)
        {
            try
            {
                obj = Json.DeserializeFromJson<T>(data);
                return true;
            }
            catch (Exception e)
            {
                Log.WARNING(this.GetHandle(), $"GenericJsonSerializer failed to deserialize object from string!", e.ToString());
                obj = default;
                return false;
            }
        }

        public bool TrySerialize<T>(T obj, out ISerializedObject serializedObject)
        {
            try
            {
                string json = Json.SerializeToJson(obj, Json.ComplexObjectsStructure_SerializerSettings);
                var data = Encoding.UTF8.GetBytes(json);
                serializedObject = new GenericSerializedObject(data, this);
                return true;
            }
            catch (Exception e)
            {
                Log.WARNING(this.GetHandle(), $"GenericJsonSerializer failed to serialize object from type {typeof(T)}!", e.ToString());
                serializedObject = null;
                return false;
            }
        }

        public async Task<bool> TrySerializeToStreamAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                string json = Json.SerializeToJson(obj, Json.ComplexObjectsStructure_SerializerSettings);
                var data = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                return true;
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Serializing of type {typeof(T)} to stream failed!", e.ToString());
                return false;
            }
        }

        public async Task<(bool, T)> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                using (TextReader textReader = new StreamReader(stream))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    JToken jObj = await JToken.LoadAsync(jsonReader, cancellationToken);
                    T obj = jObj.ToObject<T>();
                    return (true, obj);
                }
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Serializing of type {typeof(T)} to stream failed!", e.ToString());
                return (false, default);
            }
        }

        public async Task<AsyncOut<bool, ISerializedObject>> TryReadSerializedObjectFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                using (TextReader textReader = new StreamReader(stream))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    //TODO this is a little inefficient... Better directly read the chars from stream, but ensure finding correct JSON object end.
                    JToken jObj = await JToken.LoadAsync(jsonReader, cancellationToken);
                    string json = jObj.ToString();

                    var data = Encoding.UTF8.GetBytes(json);
                    var serializedObject = new GenericSerializedObject(data, this);
                    return (true, serializedObject);
                }
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Failed on reading serializedObject from stream!", e.ToString());
                return (false, null);
            }
        }
    }
    */
}