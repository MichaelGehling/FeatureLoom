using System;

namespace FeatureLoom.DependencyInversion
{
    public interface IServiceInstanceContainer
    {
        void CreateLocalServiceInstance();
        void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal);
        Type ServiceType { get; }
        object Instance { get; }
        object GlobalInstance { get; }
        bool UsesLocalInstance { get; }
        string ServiceInstanceName { get; }
        IServiceInstanceCreator ServiceInstanceCreator { get; }
    }    
}