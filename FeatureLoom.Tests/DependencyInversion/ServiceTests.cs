using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace FeatureLoom.DependencyInversion;

public class ServiceTests
{

    private interface ITestService
    {
        int I { get; }
    }

    private class TestService : ITestService
    {
        public int i = 0;
        public int I => i;
    }

    [Fact]
    public void NamedAndUnnamedServicesWorkIndependently()
    {
        TestHelper.PrepareTestContext();

        TestService unnamed = Service<TestService>.Get();
        unnamed.i = 123;
        TestService named = Service<TestService>.Get("Other");
        Assert.Equal(123, unnamed.i);
        Assert.Equal(0, named.i);

        named.i = 987;            
        ITestService iUnnamed = Service<ITestService>.Get();
        Assert.Equal(123, iUnnamed.I);
        ITestService iNamed = Service<ITestService>.Get("Other");
        Assert.Equal(987, iNamed.I);
        
        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void LocalServiceInstancesAreKeptInLogicalThread()
    {
        Service<TestService>.Init(_ => new TestService());
        Assert.Equal(0, Service<TestService>.Instance.i);
        Service<TestService>.Instance.i = 42;
        Assert.Equal(42, Service<TestService>.Instance.i);

        Task.Run(() =>
        {
            Service<TestService>.CreateLocalServiceInstance();
            Assert.Equal(0, Service<TestService>.Instance.i);
            Service<TestService>.Instance.i = 3;
            Assert.Equal(3, Service<TestService>.Instance.i);
        }).WaitFor();
        Assert.Equal(42, Service<TestService>.Instance.i);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async Task CopyContextInAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Service<TestService>.CreateLocalServiceInstance();
            Service<TestService>.Instance.i = 4;
            Assert.Equal(4, Service<TestService>.Instance.i);
        }
        CopyContextInAsync().WaitFor();
        Assert.Equal(42, Service<TestService>.Instance.i);

        void CopyContextInSync()
        {
            Service<TestService>.CreateLocalServiceInstance();
            Service<TestService>.Instance.i = 4;
            Assert.Equal(4, Service<TestService>.Instance.i);
        }
        CopyContextInSync();
        Assert.Equal(4, Service<TestService>.Instance.i);

        Task.Run(() =>
        {
            Service<TestService>.Instance.i = 5;
            Assert.Equal(5, Service<TestService>.Instance.i);
        }).Wait();
        Assert.Equal(5, Service<TestService>.Instance.i);
    }

    //[Fact]
    [Fact(Skip = "Cant run in parallel")]
    public void ServicesWithDefaultConstructorDontNeedInit()
    {
        TestHelper.PrepareTestContext();

        ServiceRegistry.AllowToSearchAssembly = false;            
        Assert.Throws<Exception>(() => Service<ITestService>.Instance);
        ServiceRegistry.AllowToSearchAssembly = true;
        Assert.NotNull(Service<ITestService>.Instance);

        TestService service = Service<TestService>.Instance;
        Assert.NotNull(service);
        Assert.NotNull(Service<ITestService>.Instance);
        Assert.NotEqual(service, Service<ITestService>.Instance);

        Service<ITestService>.Reset();
        Assert.Equal(service, Service<ITestService>.Instance);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void ResetRemovesAllInstances()
    {
        TestHelper.PrepareTestContext();

        Service<TestService>.Init(_ => new TestService());
        var instance1 = Service<TestService>.Get();
        Service<TestService>.Set("A", new TestService { i = 2 });
        var namedInstance1 = Service<TestService>.Get("A");

        Service<TestService>.Reset();

        var instance2 = Service<TestService>.Get();
        var namedInstance2 = Service<TestService>.Get("A");

        Assert.NotSame(instance1, instance2);
        Assert.NotSame(namedInstance1, namedInstance2);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void SetAndGetWorkForNamedAndUnnamed()
    {
        TestHelper.PrepareTestContext();

        var unnamed = new TestService { i = 10 };
        var named = new TestService { i = 20 };
        Service<TestService>.Set(unnamed);
        Service<TestService>.Set("X", named);

        Assert.Equal(10, Service<TestService>.Get().i);
        Assert.Equal(20, Service<TestService>.Get("X").i);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void DeleteRemovesService()
    {
        TestHelper.PrepareTestContext();

        var instance1 = new TestService { i = 5 };
        Service<TestService>.Set(instance1);
        Service<TestService>.Delete();
        var instance2 = Service<TestService>.Get();
        Assert.NotSame(instance1, instance2);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void DeleteNamedRemovesOnlyNamedService()
    {
        TestHelper.PrepareTestContext();

        var namedInstance1 = new TestService { i = 7 };
        Service<TestService>.Set("Y", namedInstance1);
        Service<TestService>.Delete("Y");
        var namedInstance2 = Service<TestService>.Get("Y");
        Assert.NotSame(namedInstance1, namedInstance2);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void MultipleNamedInstancesAreIndependent()
    {
        TestHelper.PrepareTestContext();

        Service<TestService>.Set("A", new TestService { i = 1 });
        Service<TestService>.Set("B", new TestService { i = 2 });

        Assert.Equal(1, Service<TestService>.Get("A").i);
        Assert.Equal(2, Service<TestService>.Get("B").i);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void SettingConcreteForInterfaceWorks()
    {
        TestHelper.PrepareTestContext();

        var concrete = new TestService { i = 42 };
        Service<ITestService>.Set(concrete);
        Assert.Equal(42, Service<ITestService>.Get().I);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void ThreadSafetyWithParallelAccess()
    {
        TestHelper.PrepareTestContext();

        Service<TestService>.Init(_ => new TestService());
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        var threads = new List<Thread>();

        for (int t = 0; t < 10; t++)
        {
            int value = t;
            var thread = new Thread(() =>
            {
                Service<TestService>.CreateLocalServiceInstance(new TestService { i = value });
                results.Add(Service<TestService>.Instance.i);
            });
            threads.Add(thread);
            thread.Start();
        }
        foreach (var thread in threads) thread.Join();

        var sortedResults = results.ToList();
        sortedResults.Sort();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, sortedResults[i]);
        }

        Assert.False(TestHelper.HasAnyLogError());
    }
}

public class ServiceDisposalTests
{
    private class DisposableTestService : IDisposable
    {
        public static int DisposeCount = 0;
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCount);
        }
    }

    //[Fact]
    [Fact(Skip = "Cant run in parallel")]
    public void ServiceInstance_IsAutomaticallyDisposed_WhenUnreferenced()
    {
        TestHelper.PrepareTestContext();

        // Arrange
        DisposableTestService.DisposeCount = 0;
        WeakReference weakRef = null;

        void CreateAndRelease()
        {
            var instance = new DisposableTestService();
            Service<DisposableTestService>.Set(instance);
            weakRef = new WeakReference(instance);
        }

        CreateAndRelease();

        // Remove strong references
        Service<DisposableTestService>.Delete();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert: WeakReference should be invalid, and Dispose should have been called
        Assert.False(weakRef.IsAlive);
        // Give some time for the finalizer queue to process
        Thread.Sleep(100);
        Assert.True(DisposableTestService.DisposeCount > 0);

        Assert.False(TestHelper.HasAnyLogError());
    }
}