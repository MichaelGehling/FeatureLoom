using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using System;

namespace FeatureLoom.DependencyInversion
{
    public static partial class Service<T> where T : class
    {
        /// <summary>
        /// Implements <see cref="IServiceInstanceCreator"/> for type <typeparamref name="T"/>.
        /// Responsible for creating service instances using a provided factory delegate.
        /// </summary>
        internal class ServiceInstanceCreator : IServiceInstanceCreator
        {
            // Delegate used to create instances of T, optionally using a name.
            private readonly Func<string, T> creatorAction;

            /// <summary>
            /// Initializes a new instance of the <see cref="ServiceInstanceCreator"/> class.
            /// </summary>
            /// <param name="creatorAction">Factory delegate to create instances of T.</param>
            public ServiceInstanceCreator(Func<string, T> creatorAction)
            {
                this.creatorAction = creatorAction;                
            }

            /// <summary>
            /// Gets the service type this creator is responsible for.
            /// </summary>
            public Type ServiceType => typeof(T);

            /// <summary>
            /// Creates a service instance of type <typeparamref name="S"/> using the factory delegate.
            /// Returns null if <typeparamref name="S"/> is not assignable from <typeparamref name="T"/>.
            /// </summary>
            /// <typeparam name="S">The requested service type.</typeparam>
            /// <param name="name">Optional name for the service instance.</param>
            /// <returns>A new instance of <typeparamref name="S"/>, or null if not compatible.</returns>
            public S CreateServiceInstance<S>(string name = null) where S : class
            {
                if (!typeof(S).IsAssignableFrom(typeof(T)))
                {                    
                    return null;
                }
                S service = creatorAction(name) as S;                
                if (service is IDisposable disposableService)
                {
                    disposableService.AttachDetructor(s => s.Dispose());                    
                }                
                return service;
            }
        }
    }    
}
