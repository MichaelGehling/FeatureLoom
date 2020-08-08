namespace FeatureFlowFramework.Services.Serialization
{
    public interface ISerializedObject
    {
        byte[] AsBytes();
        string AsString();
        bool TryDeserialize<T>(out T obj);
    }


}
