using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FeatureFlowFramework.Helpers;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Misc;

namespace FeatureFlowFramework.Helpers
{
    public class ServiceContextTests
    {
        class TestContextData : IServiceContextData
        {
            public int i = 0;
            public IServiceContextData Copy()
            {
                var newContext = new TestContextData()
                {
                    i = this.i
                };
                return newContext;
            }
        }

        [Fact]
        public void ContextCopiesAreKeptInLogicalThread()
        {
            ServiceContext<TestContextData> context = new ServiceContext<TestContextData>();
            Assert.Equal(0, context.Data.i);
            context.Data.i = 42;
            Assert.Equal(42, context.Data.i);

            Task.Run(() =>
            {
                context.UseCopy();
                context.Data.i = 3;
                Assert.Equal(3, context.Data.i);
            }).Wait();
            Assert.Equal(42, context.Data.i);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            async Task CopyContextInAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            {
                context.UseCopy();
                context.Data.i = 3;
                Assert.Equal(3, context.Data.i);                
            }
            CopyContextInAsync().Wait();
            Assert.Equal(42, context.Data.i);
            
            void CopyContextInSync()
            {
                context.UseCopy();
                context.Data.i = 4;
                Assert.Equal(4, context.Data.i);
            }
            CopyContextInSync();
            Assert.Equal(4, context.Data.i);

            Task.Run(() =>
            {
                context.Data.i = 5;
                Assert.Equal(5, context.Data.i);
            }).Wait();
            Assert.Equal(5, context.Data.i);
        }

        [Fact]
        public void NewContextsCanBeUsedInsteadOfCopies()
        {
            ServiceContext<TestContextData> context = new ServiceContext<TestContextData>();
            Assert.Equal(0, context.Data.i);
            context.Data.i = 42;
            Assert.Equal(42, context.Data.i);

            Task.Run(() =>
            {
                context.UseNew();
                Assert.Equal(0, context.Data.i);
                context.Data.i = 3;
                Assert.Equal(3, context.Data.i);
            }).Wait();
            Assert.Equal(42, context.Data.i);
        }

        [Fact]
        public void AllContextsCanBeSeperatedWithOneStaticCall()
        {
            ServiceContext<TestContextData> context1 = new ServiceContext<TestContextData>();
            ServiceContext<TestContextData> context2 = new ServiceContext<TestContextData>();
            context1.Data.i = 1;
            context2.Data.i = 2;

            Task.Run(() =>
            {
                ServiceContext.UseCopyOfContexts();
                Assert.Equal(1, context1.Data.i);
                Assert.Equal(2, context2.Data.i);
                context1.Data.i = 11;
                Assert.Equal(11, context1.Data.i);
                context2.Data.i = 22;
                Assert.Equal(22, context2.Data.i);
            }).Wait();

            Assert.Equal(1, context1.Data.i);
            Assert.Equal(2, context2.Data.i);

            Task.Run(() =>
            {
                ServiceContext.UseNewContexts();
                Assert.Equal(0, context1.Data.i);
                Assert.Equal(0, context2.Data.i);
                context1.Data.i = 11;
                Assert.Equal(11, context1.Data.i);
                context2.Data.i = 22;
                Assert.Equal(22, context2.Data.i);
            }).Wait();

            Assert.Equal(1, context1.Data.i);
            Assert.Equal(2, context2.Data.i);
        }

        [Fact(Skip = "NoContextSeperationPolicy will interfere other tests")]
        public void NoContextSeperationPolicyWillPreventCopyingContexts()
        {            
            ServiceContext.NoContextSeperationPolicy = true;
            
            ServiceContext<TestContextData> context1 = new ServiceContext<TestContextData>();
            ServiceContext<TestContextData> context2 = new ServiceContext<TestContextData>();
            context1.Data.i = 1;
            context2.Data.i = 2;

            Task.Run(() =>
            {
                ServiceContext.UseCopyOfContexts();
                context1.Data.i = 11;
                Assert.Equal(11, context1.Data.i);
                context2.Data.i = 22;
                Assert.Equal(22, context2.Data.i);
            }).Wait();

            Assert.Equal(11, context1.Data.i);
            Assert.Equal(22, context2.Data.i);

            Task.Run(() =>
            {
                ServiceContext.UseNewContexts();
                context1.Data.i = 111;
                Assert.Equal(111, context1.Data.i);
                context2.Data.i = 222;
                Assert.Equal(122, context2.Data.i);
            }).Wait();

            Assert.Equal(111, context1.Data.i);
            Assert.Equal(222, context2.Data.i);
        }

    }
}
