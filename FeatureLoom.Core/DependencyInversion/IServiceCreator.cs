using System;

namespace FeatureLoom.DependencyInversion
{
    public interface IServiceInstanceCreator
    {
        Type ServiceType { get; }
        S CreateServiceInstance<S>(string name = null) where S : class;
    }
}