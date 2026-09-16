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

    // Preparation only changes metadata; the returned action may execute a service factory.
    internal interface IPreparedServiceInstanceContainer : IServiceInstanceContainer
    {
        void EnableLocalServiceInstances();
        Action PrepareLocalServiceInstance();
    }
}