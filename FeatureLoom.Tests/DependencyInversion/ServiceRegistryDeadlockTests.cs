using FeatureLoom.Helpers;
using FeatureLoom.Storages;
using FeatureLoom.Time;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DependencyInversion;

public class ServiceRegistryDeadlockTests
{
    [Theory]
    [InlineData(nameof(ServiceRegistryTestProcess.ActivationResolvesNewDependency))]
    [InlineData(nameof(ServiceRegistryTestProcess.ActivationPreservesDependencyIdentity))]
    [InlineData(nameof(ServiceRegistryTestProcess.RegistrationDoesNotConstructSuppliedInstance))]
    [InlineData(nameof(ServiceRegistryTestProcess.CompatibleContainerResolvesOutsideRegistryLock))]
    [InlineData(nameof(ServiceRegistryTestProcess.NewServicesHonorLocalMode))]
    [InlineData(nameof(ServiceRegistryTestProcess.NamedLocalInstancesReceiveTheirName))]
    [InlineData(nameof(ServiceRegistryTestProcess.FailedLocalFactoryCanRetry))]
    [InlineData(nameof(ServiceRegistryTestProcess.ClearDuringActivationDiscardsPendingLocal))]
    [InlineData(nameof(ServiceRegistryTestProcess.PromotionDuringActivationKeepsGlobal))]
    [InlineData(nameof(ServiceRegistryTestProcess.RegistrationCanOverlapActivation))]
    [InlineData(nameof(ServiceRegistryTestProcess.ResetCanOverlapActivation))]
    [InlineData(nameof(ServiceRegistryTestProcess.ParallelActivationsKeepContextValues))]
    [InlineData(nameof(ServiceRegistryTestProcess.StorageCanPrepareTestContextWithColdClock))]
    [InlineData(nameof(ServiceRegistryTestProcess.ManualLocalOverridesRetainGlobalFallback))]
    [InlineData(nameof(ServiceRegistryTestProcess.CompletedLocalCanBecomeGlobal))]
    [InlineData(nameof(ServiceRegistryTestProcess.ClearedActivationCannotOverwriteReactivation))]
    [InlineData(nameof(ServiceRegistryTestProcess.LocalFactoryCycleThrowsAndCanRecover))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateUnnamedResolutionSharesScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateNamedResolutionSharesScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.ConcurrentLateResolutionSharesScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateAssignmentSharesScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateAliasSharesScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.SeparateScopesForLateRegistrations))]
    [InlineData(nameof(ServiceRegistryTestProcess.CurrentClearLeavesOtherScopes))]
    [InlineData(nameof(ServiceRegistryTestProcess.CurrentClearFromChildClosesSharedScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.PromotionLeavesOtherScopes))]
    [InlineData(nameof(ServiceRegistryTestProcess.ManualOverridesInsideScopeStayContextLocal))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateContainerRegisteredOutsideScope))]
    [InlineData(nameof(ServiceRegistryTestProcess.ExplicitAllContextsClearClosesOtherScopes))]
    [InlineData(nameof(ServiceRegistryTestProcess.ExplicitAllContextsPromotionUsesCallerValues))]
    [InlineData(nameof(ServiceRegistryTestProcess.ServiceClearLeavesOtherScopes))]
    [InlineData(nameof(ServiceRegistryTestProcess.ServiceClearAllContextsLeavesOtherServices))]
    [InlineData(nameof(ServiceRegistryTestProcess.LateFactoryFailureCanRetryInParent))]
    [InlineData(nameof(ServiceRegistryTestProcess.CurrentClearDiscardsLateFactoryResult))]
    [InlineData(nameof(ServiceRegistryTestProcess.ClearedAliasDoesNotPublishScopedSourceAsGlobal))]
    public async Task ScenarioCompletesInIsolatedProcess(string scenario)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), nameof(ServiceRegistryDeadlockTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        try
        {
            await RunScenarioInProcess(scenario, workingDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static async Task RunScenarioInProcess(string scenario, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            // Process isolation must include relative storage and log paths, not just static registry state.
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(typeof(ServiceRegistryDeadlockTests).Assembly.Location);
        startInfo.ArgumentList.Add("--service-registry-scenario");
        startInfo.ArgumentList.Add(scenario);
        using var process = Process.Start(startInfo);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(15)));
        if (completed != exited)
        {
            // A deadlocked registry must not poison the test host or leave a spinning worker behind.
            process.Kill(entireProcessTree: true);
            await exited;
            Assert.Fail($"{scenario} timed out.\n{await output}\n{await error}");
        }
        await exited;
        Assert.True(process.ExitCode == 0, $"{scenario} exited with {process.ExitCode}.\n{await output}\n{await error}");
    }
}

internal static partial class ServiceRegistryTestProcess
{
    private interface IConsumer { }
    private sealed class Consumer : IConsumer
    {
        public Consumer(Dependency dependency) => Dependency = dependency;
        public Dependency Dependency { get; }
    }
    private sealed class Dependency
    {
        public Dependency(string name) => Name = name;
        public string Name { get; }
    }

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--service-registry-scenario") return 2;
        try
        {
            Console.WriteLine($"Starting {args[1]}");
            ServiceRegistry.AllowToSearchAssembly = false;
            typeof(ServiceRegistryTestProcess).GetMethod(args[1], BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Console.WriteLine("Passed");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static void ActivationResolvesNewDependency()
    {
        bool resolve = false;
        Service<Dependency>.Init(name => new Dependency(name));
        Service<Consumer>.Init(_ => new Consumer(resolve ? Service<Dependency>.Get() : null));
        var global = Service<Consumer>.Get();
        resolve = true;

        ServiceRegistry.CreateLocalInstancesForAllServices();

        Assert.NotSame(global, Service<Consumer>.Get());
        Assert.Same(Service<Dependency>.Get(), Service<Consumer>.Get().Dependency);
        Assert.All(ServiceRegistry.GetAllRegisteredServices(), service => Assert.True(service.UsesLocalInstance));
    }

    internal static void ActivationPreservesDependencyIdentity()
    {
        bool resolve = false;
        int dependencyCreations = 0;
        Service<Consumer>.Init(_ => new Consumer(resolve ? Service<Dependency>.Get() : null));
        Service<Dependency>.Init(name => { dependencyCreations++; return new Dependency(name); });
        Service<Consumer>.Get();
        var globalDependency = Service<Dependency>.Get();
        resolve = true;

        ServiceRegistry.CreateLocalInstancesForAllServices();

        Assert.NotSame(globalDependency, Service<Dependency>.Get());
        Assert.Same(Service<Dependency>.Get(), Service<Consumer>.Get().Dependency);
        Assert.Equal(2, dependencyCreations);
    }

    internal static void RegistrationDoesNotConstructSuppliedInstance()
    {
        int creations = 0;
        ServiceRegistry.CreateLocalInstancesForAllServices();
        Service<Consumer>.Init(_ =>
        {
            creations++;
            ServiceRegistry.GetAllRegisteredServices();
            return new Consumer(null);
        });
        var supplied = new Consumer(null);

        Service<Consumer>.Set(supplied);

        Assert.Same(supplied, Service<Consumer>.Get());
        Assert.Equal(0, creations);
        Assert.True(Assert.Single(ServiceRegistry.GetAllRegisteredServices()).UsesLocalInstance);
    }

    internal static void CompatibleContainerResolvesOutsideRegistryLock()
    {
        Service<Consumer>.Init(_ =>
        {
            ServiceRegistry.GetAllRegisteredServices();
            return new Consumer(null);
        });
        Service<Consumer>.CreateLocalServiceInstance(new Consumer(null));

        RunWithoutContext(() =>
        {
            var alias = Service<IConsumer>.Get();
            Assert.Same(Service<Consumer>.Get(), alias);
            return alias;
        }).GetAwaiter().GetResult();
    }

    internal static void NewServicesHonorLocalMode()
    {
        ServiceRegistry.CreateLocalInstancesForAllServices();
        Service<Dependency>.Init(name => new Dependency(name));
        var local = Service<Dependency>.Get();
        Assert.True(Assert.Single(ServiceRegistry.GetAllRegisteredServices()).UsesLocalInstance);
        var otherContext = RunWithoutContext(() => Service<Dependency>.Get()).GetAwaiter().GetResult();
        Assert.NotSame(local, otherContext);

        ServiceRegistry.ClearAllLocalServiceInstances(false);

        Assert.NotSame(local, Service<Dependency>.Get());
        Assert.False(Assert.Single(ServiceRegistry.GetAllRegisteredServices()).UsesLocalInstance);
    }

    internal static void NamedLocalInstancesReceiveTheirName()
    {
        Service<Dependency>.Init(name => new Dependency(name));
        var global = Service<Dependency>.Get("named");
        ServiceRegistry.CreateLocalInstancesForAllServices();
        Assert.Equal("named", Service<Dependency>.Get("named").Name);
        Assert.NotSame(global, Service<Dependency>.Get("named"));
        Service<Dependency>.CreateLocalServiceInstance("named");
        Assert.Equal("named", Service<Dependency>.Get("named").Name);
    }

    internal static void FailedLocalFactoryCanRetry()
    {
        bool fail = false;
        Service<Dependency>.Init(name =>
        {
            if (fail)
            {
                fail = false;
                throw new InvalidOperationException("Factory failure");
            }
            return new Dependency(name);
        });
        var global = Service<Dependency>.Get();
        fail = true;

        Assert.Throws<InvalidOperationException>(() => ServiceRegistry.CreateLocalInstancesForAllServices());

        Assert.NotSame(global, Service<Dependency>.Get());
        Assert.NotNull(ServiceRegistry.GetAllRegisteredServices());
        ServiceRegistry.ClearAllLocalServiceInstances(false);
        Assert.Same(global, Service<Dependency>.Get());
    }

    internal static void ClearDuringActivationDiscardsPendingLocal() => ClearDuringActivation(false);
    internal static void PromotionDuringActivationKeepsGlobal() => ClearDuringActivation(true);

    private static void ClearDuringActivation(bool promote)
    {
        WithBlockedActivation((global, release) =>
        {
            ServiceRegistry.ClearAllLocalServiceInstances(promote, allContexts: true);
            release.Set();
            Assert.False(ServiceRegistry.LocalInstancesForAllServicesActive);
            Assert.False(Assert.Single(ServiceRegistry.GetAllRegisteredServices()).UsesLocalInstance);
            Assert.Same(global, Service<Dependency>.Get());
        });
        Assert.False(Assert.Single(ServiceRegistry.GetAllRegisteredServices()).UsesLocalInstance);
    }

    internal static void RegistrationCanOverlapActivation()
    {
        WithBlockedActivation((global, release) =>
        {
            Service<Consumer>.Init(_ => new Consumer(null));
            // Activation in another execution context no longer creates a local scope in this caller.
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = Service<Consumer>.Get();
            Assert.All(ServiceRegistry.GetAllRegisteredServices(), service => Assert.True(service.UsesLocalInstance));
            release.Set();
            Assert.Same(local, Service<Consumer>.Get());
        });
    }

    internal static void ResetCanOverlapActivation()
    {
        WithBlockedActivation((global, release) =>
        {
            Service<Dependency>.Reset();
            Assert.Empty(ServiceRegistry.GetAllRegisteredServices());
            release.Set();
        });
        Assert.Empty(ServiceRegistry.GetAllRegisteredServices());
    }

    internal static void ParallelActivationsKeepContextValues()
    {
        Service<Dependency>.Init(name => new Dependency(name));
        var global = Service<Dependency>.Get();
        using var barrier = new Barrier(2);
        Dependency Activate()
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            var local = Service<Dependency>.Get();
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            Assert.Same(local, Service<Dependency>.Get());
            return local;
        }
        var first = RunWithoutContext(Activate);
        var second = RunWithoutContext(Activate);
        Task.WaitAll(first, second);
        Assert.NotSame(first.Result, second.Result);
        ServiceRegistry.ClearAllLocalServiceInstances(false, allContexts: true);
        Assert.Same(global, Service<Dependency>.Get());
    }

    internal static void StorageCanPrepareTestContextWithColdClock()
    {
        ServiceRegistry.AllowToSearchAssembly = true;
        Service<StorageService>.Get();
        Service<IAppTime>.Reset();
        using var context = TestHelper.PrepareTestContext();
        Assert.NotEqual(default, AppTime.CoarseNow);
        Assert.NotNull(Storage.GetReader("config"));
    }

    internal static void ManualLocalOverridesRetainGlobalFallback()
    {
        Service<Dependency>.Init(name => new Dependency(name));
        var global = Service<Dependency>.Get();
        Service<Dependency>.CreateLocalServiceInstance();
        var local = Service<Dependency>.Get();
        Assert.NotSame(global, local);
        Assert.Same(global, RunWithoutContext(() => Service<Dependency>.Get()).GetAwaiter().GetResult());

        Task.Run(() =>
        {
            Assert.Same(local, Service<Dependency>.Get());
            Service<Dependency>.Set(null);
            Assert.Same(global, Service<Dependency>.Get());
        }).GetAwaiter().GetResult();
        Assert.Same(local, Service<Dependency>.Get());
        Service<Dependency>.ClearAllLocalServiceInstances(false);
        Assert.Same(global, Service<Dependency>.Get());
    }

    internal static void CompletedLocalCanBecomeGlobal()
    {
        Service<Dependency>.Init(name => new Dependency(name));
        var global = Service<Dependency>.Get();
        ServiceRegistry.CreateLocalInstancesForAllServices();
        var local = Service<Dependency>.Get();
        Assert.NotSame(global, local);

        ServiceRegistry.ClearAllLocalServiceInstances(true);

        Assert.Same(local, Service<Dependency>.Get());
        ServiceRegistry.CreateLocalInstancesForAllServices();
        Assert.NotSame(local, Service<Dependency>.Get());
        ServiceRegistry.ClearAllLocalServiceInstances(false);
        Assert.Same(local, Service<Dependency>.Get());
    }

    internal static void ClearedActivationCannotOverwriteReactivation()
    {
        Dependency replacement = null;
        WithBlockedActivation((global, release) =>
        {
            ServiceRegistry.ClearAllLocalServiceInstances(false, allContexts: true);
            ServiceRegistry.CreateLocalInstancesForAllServices();
            replacement = Service<Dependency>.Get();
            Assert.NotSame(global, replacement);
            release.Set();
        });
        Assert.Same(replacement, Service<Dependency>.Get());
        Assert.True(ServiceRegistry.LocalInstancesForAllServicesActive);
    }

    internal static void LocalFactoryCycleThrowsAndCanRecover()
    {
        bool cycle = false;
        Service<Dependency>.Init(name => cycle ? Service<Dependency>.Get() : new Dependency(name));
        var global = Service<Dependency>.Get();
        cycle = true;

        var exception = Assert.Throws<InvalidOperationException>(() => ServiceRegistry.CreateLocalInstancesForAllServices());

        Assert.Contains("Circular local service initialization", exception.Message);
        cycle = false;
        Assert.NotSame(global, Service<Dependency>.Get());
    }

    private static void WithBlockedActivation(Action<Dependency, ManualResetEventSlim> action)
    {
        int block = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Service<Dependency>.Init(name =>
        {
            if (Interlocked.Exchange(ref block, 0) != 0)
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "Factory was not released");
            }
            return new Dependency(name);
        });
        var global = Service<Dependency>.Get();
        Volatile.Write(ref block, 1);
        var activation = RunWithoutContext(() => { ServiceRegistry.CreateLocalInstancesForAllServices(); return true; });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Factory was not entered");
            action(global, release);
        }
        finally
        {
            release.Set();
            activation.GetAwaiter().GetResult();
        }
    }

    private static Task<T> RunWithoutContext<T>(Func<T> action)
    {
        using (ExecutionContext.SuppressFlow()) return Task.Run(action);
    }
}
