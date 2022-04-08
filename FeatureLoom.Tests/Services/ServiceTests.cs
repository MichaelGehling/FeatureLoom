using FeatureLoom.Services;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Services
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
        public void LocalServiceInstancesAreKeptInLogicalThread()
        {
            Service<TestService>.Init(() => new TestService());
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

        [Fact]
        public void ServicesWithDefaultConstructorDontNeedInit()
        {            
            Assert.NotNull(Service<TestService>.Instance);

            Assert.Throws<Exception>(() => Service<ITestService>.Instance);

            Service<ITestService>.Init(() => new TestService());
            Assert.NotNull(Service<ITestService>.Instance);
        }

    }
}