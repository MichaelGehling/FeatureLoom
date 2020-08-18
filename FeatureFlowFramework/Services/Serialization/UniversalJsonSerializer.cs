using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Serialization
{
    public class UnivarsalJsonSerializer : ISerializer
    {
        JsonSerializer serializer;        

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
            catch(Exception e)
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
            catch(Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Serializing of type {typeof(T)} to stream failed!", e.ToString());
                return false;
            }
        }

        public async Task<AsyncOut<bool, T>> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {            
            try
            {
                using(TextReader textReader = new StreamReader(stream))
                using(JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    JToken jObj = await JToken.LoadAsync(jsonReader, cancellationToken);
                    T obj = jObj.ToObject<T>();
                    return (true, obj);
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Serializing of type {typeof(T)} to stream failed!", e.ToString());
                return (false, default);
            }
            
        }

        public async Task<AsyncOut<bool, ISerializedObject>> TryReadSerializedObjectFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                using(TextReader textReader = new StreamReader(stream))
                using(JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    //TODO this is a little inefficient... Better directly read the chars from stream, but ensure finding correct JSON object end.
                    JToken jObj = await JToken.LoadAsync(jsonReader, cancellationToken);
                    string json = jObj.ToString();

                    var data = Encoding.UTF8.GetBytes(json);
                    var serializedObject = new GenericSerializedObject(data, this);
                    return (true, serializedObject);
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Failed on reading serializedObject from stream!", e.ToString());
                return (false, null);
            }            
        }
    }
}
