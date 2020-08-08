namespace FeatureFlowFramework.Services.Serialization
{
    public interface ISerializer
    {
        bool TrySerialize<T>(T obj, out ISerializedObject serializedObject);
        ISerializedObject AsSerializedObject(byte[] data);
        ISerializedObject AsSerializedObject(string data);
        bool TryDeserialize<T>(byte[] data, out T obj);
        bool TryDeserialize<T>(string data, out T obj);
        int SerializerId { get; }
    }


}
