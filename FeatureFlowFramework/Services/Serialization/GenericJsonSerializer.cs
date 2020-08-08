using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using Newtonsoft.Json;
using System;
using System.Text;

namespace FeatureFlowFramework.Services.Serialization
{
    public class GenericJsonSerializer : ISerializer
    {
        readonly int id;
        JsonSerializer serializer;        

        public GenericJsonSerializer(int id)
        {
            this.id = id;
            InitSerializer();
        }

        private void InitSerializer()
        {
            var settings = new JsonSerializerSettings();
            //TODO setup settings
            serializer = JsonSerializer.Create(settings);
        }

        public int SerializerId => id;

        public ISerializedObject AsSerializedObject(byte[] data)
        {
            return new GenericByteSerializedObject(data, this);
        }

        public ISerializedObject AsSerializedObject(string data)
        {
            return new GenericStringSerializedObject(data, this);
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
                Log.WARNING(this.GetHandle(), $"Serializer with id {id} failed to deserialize object from byte[]!", e.ToString());
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
                Log.WARNING(this.GetHandle(), $"Serializer with id {id} failed to deserialize object from string!", e.ToString());
                obj = default;
                return false;
            }
        }

        public bool TrySerialize<T>(T obj, out ISerializedObject serializedObject)
        {
            try
            {
                string json = Json.SerializeToJson(obj, Json.ComplexObjectsStructure_SerializerSettings);
                serializedObject = new GenericStringSerializedObject(json, this);
                return true;
            }
            catch(Exception e)
            {
                Log.WARNING(this.GetHandle(), $"Serializer with id {id} failed to serialize object from type {typeof(T)}!", e.ToString());
                serializedObject = null;
                return false;
            }          
        }
    }
}
