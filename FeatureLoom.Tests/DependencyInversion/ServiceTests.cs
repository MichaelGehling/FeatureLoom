using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DependencyInversion
{
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
        //[Fact(Skip = "Cant run in parallel")]
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
        [Fact(Skip ="Cant run in parallel")]
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

    }
}