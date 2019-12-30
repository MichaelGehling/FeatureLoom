namespace FeatureFlowFramework.Aspects
{
    public interface IAspectValueContainer
    {
        string CollectionName { get; }
        void Set<T>(string key, T value);
        bool Contains(string key);
        bool TryGet<T>(string key, out T value);
    }
}
