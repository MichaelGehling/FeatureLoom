using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Helpers
{
    public class TypeNameHelperTests
    {
        public static IEnumerable<object[]> TestTypes =>
            new List<object[]>
            {
                new object[] { typeof(int) },
                new object[] { typeof(string) },
                new object[] { typeof(DateTime) },
                new object[] { typeof(int[]) },
                new object[] { typeof(string[]) },
                new object[] { typeof(List<int>) },
                new object[] { typeof(List<string>) },
                new object[] { typeof(Dictionary<string, object>) },
                new object[] { typeof(Dictionary<int, List<string>>) },
                new object[] { typeof(List<int>[]) },
                new object[] { typeof(Dictionary<string, object>[]) },
            };

        [Theory]
        [MemberData(nameof(TestTypes))]
        public void GetSimplifiedTypeName_And_GetTypeFromSimplifiedName_ShouldBeSymmetric(Type type)
        {
            TypeNameHelper typeNameHelper = new TypeNameHelper();

            string simplifiedName = TypeNameHelper.Shared.GetSimplifiedTypeName(type);
            Assert.NotNull(simplifiedName);

            Type resolvedType = typeNameHelper.GetTypeFromSimplifiedName(simplifiedName);
            Assert.Equal(type, resolvedType);
        }

        [Fact]
        public void GetSimplifiedTypeName_ShouldReturnCorrectNames()
        {
            TypeNameHelper typeNameHelper = new TypeNameHelper();

            Assert.Equal("System.Int32", TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(int)));
            Assert.Equal("System.Collections.Generic.List<System.Int32>", TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<int>)));
            Assert.Equal("System.Collections.Generic.Dictionary<System.String, System.Object>", TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Dictionary<string, object>)));
            Assert.Equal("System.Int32[]", TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(int[])));
            Assert.Equal("System.Collections.Generic.List<System.String>[]", TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<string>[])));
        }

        [Fact]
        public void GetTypeFromSimplifiedName_ShouldReturnCorrectTypes()
        {
            TypeNameHelper typeNameHelper = new TypeNameHelper();

            Assert.Equal(typeof(int), typeNameHelper.GetTypeFromSimplifiedName("System.Int32"));
            Assert.Equal(typeof(List<int>), typeNameHelper.GetTypeFromSimplifiedName("System.Collections.Generic.List<System.Int32>"));
            Assert.Equal(typeof(Dictionary<string, object>), typeNameHelper.GetTypeFromSimplifiedName("System.Collections.Generic.Dictionary<System.String, System.Object>"));
            Assert.Equal(typeof(int[]), typeNameHelper.GetTypeFromSimplifiedName("System.Int32[]"));
            Assert.Equal(typeof(List<string>[]), typeNameHelper.GetTypeFromSimplifiedName("System.Collections.Generic.List<System.String>[]"));
        }

        [Fact]
        public void GetTypeFromSimplifiedName_ShouldReturnNullForUnknownType()
        {
            TypeNameHelper typeNameHelper = new TypeNameHelper();

            Assert.Null(typeNameHelper.GetTypeFromSimplifiedName("Non.Existent.Type.Name"));
            Assert.Null(typeNameHelper.GetTypeFromSimplifiedName("Non.Existent.Generic<System.Int32>"));
        }

        [Fact]
        public void MethodsShouldBeThreadSafe()
        {
            TypeNameHelper typeNameHelper = new TypeNameHelper();

            var types = new List<Type>
            {
                typeof(int), typeof(string), typeof(List<double>), typeof(Dictionary<string, TypeNameHelperTests>)
            };

            var tasks = new List<System.Threading.Tasks.Task>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var type in types)
                    {
                        string name = TypeNameHelper.Shared.GetSimplifiedTypeName(type);
                        Type resolvedType = typeNameHelper.GetTypeFromSimplifiedName(name);
                        Assert.Equal(type, resolvedType);
                    }
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
        }
    }
}