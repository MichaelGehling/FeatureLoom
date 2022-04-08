using FeatureLoom.Services;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Services
{
    public class FactoryTests
    {

        public interface ITest
        {
            string TestString { get; }
        }

        public class A : ITest
        {
            public string TestString => "A";
        }

        public class B : ITest
        {
            string s = "B";

            public B()
            {
            }

            public B(string s)
            {
                this.s = s;
            }

            public string TestString => s;
        }


        [Fact]
        public void FactoryCanBeOverriden()
        {
            ITest testObj = Factory.Create<ITest>(() => new A());
            Assert.NotNull(testObj);
            Assert.Equal("A", testObj.TestString);

            Factory.OverrideCreate<ITest>(() => new B());
            testObj = Factory.Create<ITest>(() => new A());
            Assert.NotNull(testObj);
            Assert.Equal("B", testObj.TestString);
        }

        [Fact]
        public void FactoryCanUseDefaultConstructor()
        {
            B testObj = Factory.Create<B>();
            Assert.NotNull(testObj);
            Assert.Equal("B", testObj.TestString);

            Factory.OverrideCreate<B>(() => new B("X"));
            testObj = Factory.Create<B>(() => new B());
            Assert.NotNull(testObj);
            Assert.Equal("X", testObj.TestString);
        }

    }
}