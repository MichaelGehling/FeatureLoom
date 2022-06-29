using System;

namespace FeatureLoom.Services
{
    public interface IServiceInstanceContainer
    {
        void CreateLocalServiceInstance();
        void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal);
        Type ServiceType { get; }
        object Instance { get; }
        bool TryGetCreateServiceAction<T>(out Func<T> createServiceAction);
    }
}