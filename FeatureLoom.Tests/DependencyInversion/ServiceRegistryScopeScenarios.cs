using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DependencyInversion;

internal static partial class ServiceRegistryTestProcess
{
    private interface IScopedComponent { }

    private sealed class ScopedComponent : IScopedComponent
    {
        public string Name;
        public bool Initialized;
        public bool Disposed;
    }

    private static async Task<T> ResolveAfterYield<T>(string name = "") where T : class
    {
        await Task.Yield();
        return Service<T>.Get(name);
    }

    internal static void LateUnnamedResolutionSharesScope() => LateResolutionSharesScope("").GetAwaiter().GetResult();
    internal static void LateNamedResolutionSharesScope() => LateResolutionSharesScope("component").GetAwaiter().GetResult();

    private static async Task LateResolutionSharesScope(string name)
    {
        ServiceRegistry.CreateLocalInstancesForAllServices();
        Service<ScopedComponent>.Init(key => new ScopedComponent { Name = key });
        async Task<ScopedComponent> InitAsync()
        {
            var component = await ResolveAfterYield<ScopedComponent>(name);
            component.Initialized = true;
            return component;
        }
        async Task<ScopedComponent> RunAsync()
        {
            var component = await ResolveAfterYield<ScopedComponent>(name);
            Assert.True(component.Initialized);
            Assert.False(component.Disposed);
            return component;
        }
        async Task<ScopedComponent> DisposeAsync()
        {
            var component = await ResolveAfterYield<ScopedComponent>(name);
            component.Disposed = true;
            return component;
        }

        var initialized = await InitAsync();
        Assert.Same(initialized, Service<ScopedComponent>.Get(name));
        Assert.Same(initialized, await RunAsync());
        Assert.Same(initialized, await DisposeAsync());
        Assert.True(Service<ScopedComponent>.Get(name).Disposed);
        Assert.Equal(name, initialized.Name);
    }

    internal static void ConcurrentLateResolutionSharesScope()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            int creations = 0;
            Service<ScopedComponent>.Init(name =>
            {
                Interlocked.Increment(ref creations);
                ServiceRegistry.GetAllRegisteredServices();
                return new ScopedComponent { Name = name, Initialized = true };
            });
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var children = Enumerable.Range(0, 16).Select(async _ =>
            {
                await start.Task;
                return Service<ScopedComponent>.Get("parallel");
            }).ToArray();
            start.SetResult(true);
            var results = await Task.WhenAll(children);
            Assert.Equal(1, creations);
            Assert.All(results, component => Assert.Same(results[0], component));
            Assert.Same(results[0], Service<ScopedComponent>.Get("parallel"));
            Assert.True(results[0].Initialized);
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void LateAssignmentSharesScope()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            int creations = 0;
            Service<ScopedComponent>.Init(_ => { creations++; return new ScopedComponent(); });
            var supplied = new ScopedComponent { Initialized = true };
            async Task Assign()
            {
                await Task.Yield();
                Service<ScopedComponent>.Set(supplied);
            }
            await Assign();
            Assert.Same(supplied, Service<ScopedComponent>.Get());
            Assert.Same(supplied, await ResolveAfterYield<ScopedComponent>());
            Assert.Equal(0, creations);
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void LateAliasSharesScope()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            var concrete = await ResolveAfterYield<ScopedComponent>();
            var alias = await ResolveAfterYield<IScopedComponent>();
            Assert.Same(concrete, alias);
            Assert.Same(alias, Service<IScopedComponent>.Get());
            Assert.Same(concrete, Service<ScopedComponent>.Get());
            async Task OtherScope()
            {
                ServiceRegistry.CreateLocalInstancesForAllServices();
                var other = await ResolveAfterYield<IScopedComponent>();
                Assert.Same(other, Service<ScopedComponent>.Get());
                Assert.NotSame(alias, other);
            }
            await OtherScope();
            Assert.Same(alias, Service<IScopedComponent>.Get());
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void SeparateScopesForLateRegistrations()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<ScopedComponent> OtherScope()
            {
                ServiceRegistry.CreateLocalInstancesForAllServices();
                await registered.Task;
                var childValue = await ResolveAfterYield<ScopedComponent>();
                Assert.Same(childValue, Service<ScopedComponent>.Get());
                return childValue;
            }
            var other = OtherScope();
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            registered.SetResult(true);
            var local = await ResolveAfterYield<ScopedComponent>();
            Assert.NotSame(local, await other);
            Assert.Same(local, Service<ScopedComponent>.Get());
            Assert.True(ServiceRegistry.LocalInstancesForAllServicesActive);
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void CurrentClearLeavesOtherScopes() => ClearOtherScope(false).GetAwaiter().GetResult();
    internal static void PromotionLeavesOtherScopes() => ClearOtherScope(true).GetAwaiter().GetResult();

    private static async Task ClearOtherScope(bool promote)
    {
        Service<ScopedComponent>.Init(_ => new ScopedComponent());
        var global = Service<ScopedComponent>.Get();
        ServiceRegistry.CreateLocalInstancesForAllServices();
        var parent = Service<ScopedComponent>.Get();
        async Task<ScopedComponent> OtherScope()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = await ResolveAfterYield<ScopedComponent>();
            Assert.NotSame(parent, local);
            ServiceRegistry.ClearAllLocalServiceInstances(promote);
            Assert.False(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(promote ? local : global, Service<ScopedComponent>.Get());
            return local;
        }
        var other = await OtherScope();
        Assert.True(ServiceRegistry.LocalInstancesForAllServicesActive);
        Assert.Same(parent, Service<ScopedComponent>.Get());
        Assert.Same(parent, await ResolveAfterYield<ScopedComponent>());
        Assert.Same(promote ? other : global, await RunWithoutContext(() => Service<ScopedComponent>.Get()));
    }

    internal static void CurrentClearFromChildClosesSharedScope()
    {
        async Task Run()
        {
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            var global = Service<ScopedComponent>.Get();
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = Service<ScopedComponent>.Get();
            async Task Clear()
            {
                await Task.Yield();
                ServiceRegistry.ClearAllLocalServiceInstances(false);
            }
            await Clear();
            Assert.False(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(global, Service<ScopedComponent>.Get());
            Assert.Same(global, await ResolveAfterYield<ScopedComponent>());
            Assert.NotSame(local, global);
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void ManualOverridesInsideScopeStayContextLocal()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            var original = await ResolveAfterYield<ScopedComponent>();
            async Task Override()
            {
                await Task.Yield();
                Service<ScopedComponent>.CreateLocalServiceInstance();
                var local = Service<ScopedComponent>.Get();
                Assert.NotSame(original, local);
                Assert.Same(local, await ResolveAfterYield<ScopedComponent>());
            }
            await Override();
            Assert.Same(original, Service<ScopedComponent>.Get());
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void LateContainerRegisteredOutsideScope()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            var global = await RunWithoutContext(() => Service<ScopedComponent>.Get());
            var local = await ResolveAfterYield<ScopedComponent>();
            Assert.NotSame(global, local);
            Assert.Same(local, Service<ScopedComponent>.Get());
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void ExplicitAllContextsClearClosesOtherScopes() => ClearEveryScope(false).GetAwaiter().GetResult();
    internal static void ExplicitAllContextsPromotionUsesCallerValues() => ClearEveryScope(true).GetAwaiter().GetResult();

    private static async Task ClearEveryScope(bool promote)
    {
        Service<ScopedComponent>.Init(_ => new ScopedComponent());
        var global = Service<ScopedComponent>.Get();
        ServiceRegistry.CreateLocalInstancesForAllServices();
        var parent = Service<ScopedComponent>.Get();
        var cleared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task OtherScope()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = Service<ScopedComponent>.Get();
            Assert.NotSame(parent, local);
            await cleared.Task;
            Assert.False(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(promote ? parent : global, Service<ScopedComponent>.Get());
        }
        var other = OtherScope();
        try
        {
            ServiceRegistry.ClearAllLocalServiceInstances(promote, allContexts: true);
            Assert.False(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(promote ? parent : global, Service<ScopedComponent>.Get());
        }
        finally
        {
            cleared.SetResult(true);
            await other;
        }
    }

    internal static void ServiceClearLeavesOtherScopes() => ClearOneService(false).GetAwaiter().GetResult();
    internal static void ServiceClearAllContextsLeavesOtherServices() => ClearOneService(true).GetAwaiter().GetResult();

    private static async Task ClearOneService(bool allContexts)
    {
        Service<ScopedComponent>.Init(name => new ScopedComponent { Name = name });
        Service<Dependency>.Init(name => new Dependency(name));
        var global = Service<ScopedComponent>.Get();
        var namedGlobal = Service<ScopedComponent>.Get("named");
        Service<Dependency>.Get();
        ServiceRegistry.CreateLocalInstancesForAllServices();
        var parentDependency = Service<Dependency>.Get();
        var cleared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task OtherScope()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = Service<ScopedComponent>.Get();
            var namedLocal = Service<ScopedComponent>.Get("named");
            var dependency = Service<Dependency>.Get();
            await cleared.Task;
            Assert.True(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(allContexts ? global : local, Service<ScopedComponent>.Get());
            Assert.Same(allContexts ? namedGlobal : namedLocal, Service<ScopedComponent>.Get("named"));
            Assert.Same(dependency, Service<Dependency>.Get());
        }
        var other = OtherScope();
        try
        {
            if (allContexts) Service<ScopedComponent>.ClearAllLocalServiceInstances(false, allContexts: true);
            else Service<ScopedComponent>.ClearAllLocalServiceInstances(false);
            Assert.True(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.Same(global, await ResolveAfterYield<ScopedComponent>());
            Assert.Same(namedGlobal, await ResolveAfterYield<ScopedComponent>("named"));
            Assert.Same(parentDependency, Service<Dependency>.Get());
        }
        finally
        {
            cleared.SetResult(true);
            await other;
        }
    }

    internal static void LateFactoryFailureCanRetryInParent()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            int attempts = 0;
            Service<ScopedComponent>.Init(_ =>
            {
                if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("First attempt");
                return new ScopedComponent { Initialized = true };
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => ResolveAfterYield<ScopedComponent>());
            var local = Service<ScopedComponent>.Get();
            Assert.True(local.Initialized);
            Assert.Same(local, await ResolveAfterYield<ScopedComponent>());
            Assert.Equal(2, attempts);
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void ClearedAliasDoesNotPublishScopedSourceAsGlobal()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            Service<ScopedComponent>.Init(_ => new ScopedComponent());
            var local = await ResolveAfterYield<ScopedComponent>();
            Assert.Same(local, await ResolveAfterYield<IScopedComponent>());
            Service<IScopedComponent>.ClearAllLocalServiceInstances(false);

            var globalAlias = Service<IScopedComponent>.Get();

            Assert.NotSame(local, globalAlias);
            Assert.Same(local, Service<ScopedComponent>.Get());
            Assert.Same(globalAlias, await RunWithoutContext(() => Service<ScopedComponent>.Get()));
        }
        Run().GetAwaiter().GetResult();
    }

    internal static void CurrentClearDiscardsLateFactoryResult()
    {
        async Task Run()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int block = 1;
            Service<ScopedComponent>.Init(_ =>
            {
                if (Interlocked.Exchange(ref block, 0) == 1)
                {
                    entered.Set();
                    Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                }
                return new ScopedComponent();
            });
            var child = Task.Run(() =>
            {
                var local = Service<ScopedComponent>.Get();
                return (local, afterClear: Service<ScopedComponent>.Get());
            });
            ScopedComponent global = null;
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
                ServiceRegistry.ClearAllLocalServiceInstances(false);
                global = Service<ScopedComponent>.Get();
            }
            finally
            {
                release.Set();
                await child;
            }
            var result = await child;
            Assert.NotSame(global, result.local);
            Assert.Same(global, result.afterClear);
            Assert.Same(global, await ResolveAfterYield<ScopedComponent>());
        }
        Run().GetAwaiter().GetResult();
    }
}
