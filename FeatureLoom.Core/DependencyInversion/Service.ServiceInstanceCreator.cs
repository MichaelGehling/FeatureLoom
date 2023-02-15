using System;

namespace FeatureLoom.DependencyInversion
{
    public static partial class Service<T> where T:class
    {
        internal class ServiceInstanceCreator : IServiceInstanceCreator
        {
            Func<string, T> creatorAction;

            public ServiceInstanceCreator(Func<string, T> creatorAction)
            {
                this.creatorAction = creatorAction;
            }

            public Type ServiceType => typeof(T);

            public S CreateServiceInstance<S>(string name = null) where S : class
            {
                if (!typeof(S).IsAssignableFrom(typeof(T))) return null;
                return creatorAction(name) as S;
            }
        }
    }    
}
