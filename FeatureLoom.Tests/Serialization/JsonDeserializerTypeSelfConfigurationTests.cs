using FeatureLoom.Helpers;
using System.Linq;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerTypeSelfConfigurationTests
    {
        public class SelfConfigured
        {
            public int A;
            public int B;

            [JsonTypeConfiguration]
            private static void ConfigureReading(JsonDeserializer.TypeSettings<SelfConfigured> s)
                => s.ConfigureMember<int>(nameof(B), m => m.SetIgnore());
        }

        public struct SelfConfiguredStruct
        {
            public int A;
            public int B;

            [JsonTypeConfiguration]
            static void ConfigureReading(JsonDeserializer.TypeSettings<SelfConfiguredStruct> s)
                => s.ConfigureMember<int>(nameof(B), m => m.SetIgnore());
        }

        public class DerivedFromSelfConfigured : SelfConfigured
        {
        }

        public class GenericSelfConfigured<T>
        {
            public T Value;
            public int Hidden;

            [JsonTypeConfiguration]
            static void ConfigureReading(JsonDeserializer.TypeSettings<GenericSelfConfigured<T>> s)
                => s.ConfigureMember<int>(nameof(Hidden), m => m.SetIgnore());
        }

        public class StructHolder
        {
            public SelfConfiguredStruct? Value;
        }

        const string Json = "{\"A\":1,\"B\":2}";

        static T Deserialize<T>(string json, TypeSelfConfigurationMode mode, System.Action<JsonDeserializer.Settings> configure = null)
        {
            var settings = new JsonDeserializer.Settings { typeSelfConfigurationMode = mode };
            configure?.Invoke(settings);
            Assert.True(new JsonDeserializer(settings).TryDeserialize(json, out T result));
            return result;
        }

        [Theory]
        [InlineData(TypeSelfConfigurationMode.Enabled)]
        [InlineData(TypeSelfConfigurationMode.EnabledKeepRefTrackingOff)]
        public void EnabledModes_ApplyTypeOwnConfiguration(TypeSelfConfigurationMode mode)
        {
            var result = Deserialize<SelfConfigured>(Json, mode);
            Assert.Equal(1, result.A);
            Assert.Equal(0, result.B);
        }

        [Theory]
        [InlineData(TypeSelfConfigurationMode.Ignore)]
        [InlineData(TypeSelfConfigurationMode.IgnoreButWarn)]
        public void IgnoreModes_DoNotApplyTypeOwnConfiguration(TypeSelfConfigurationMode mode)
        {
            Assert.Equal(2, Deserialize<SelfConfigured>(Json, mode).B);
        }

        [Fact]
        public void SettingsOverrideTypeOwnConfiguration()
        {
            var result = Deserialize<SelfConfigured>(Json, TypeSelfConfigurationMode.Enabled,
                s => s.ConfigureType<SelfConfigured>(t => t.ConfigureMember<int>(nameof(SelfConfigured.B), m => m.SetIgnore(false))));
            Assert.Equal(2, result.B);
        }

        [Fact]
        public void DerivedType_DoesNotInheritTypeOwnConfiguration()
        {
            Assert.Equal(2, Deserialize<DerivedFromSelfConfigured>(Json, TypeSelfConfigurationMode.Enabled).B);
        }

        [Fact]
        public void Struct_AndNullableStruct_ApplyTypeOwnConfiguration()
        {
            Assert.Equal(0, Deserialize<SelfConfiguredStruct>(Json, TypeSelfConfigurationMode.Enabled).B);
            var holder = Deserialize<StructHolder>("{\"Value\":" + Json + "}", TypeSelfConfigurationMode.Enabled);
            Assert.Equal(1, holder.Value.Value.A);
            Assert.Equal(0, holder.Value.Value.B);
        }

        [Fact]
        public void ClosedGenericType_AppliesTypeOwnConfiguration()
        {
            var result = Deserialize<GenericSelfConfigured<string>>("{\"Value\":\"x\",\"Hidden\":5}", TypeSelfConfigurationMode.Enabled);
            Assert.Equal("x", result.Value);
            Assert.Equal(0, result.Hidden);
        }

        [Fact]
        public void ApplyTypeSelfConfiguration_WorksWhenModeIsIgnore()
        {
            var result = Deserialize<SelfConfigured>(Json, TypeSelfConfigurationMode.Ignore, s => s.ApplyTypeSelfConfiguration<SelfConfigured>());
            Assert.Equal(0, result.B);
        }

        public class SelfConfiguredHolder
        {
            public SelfConfigured First;
            public SelfConfigured Second;
        }

        static int CountWarningsFor<T>(TestHelper.TestContext context)
        {
            string typeName = typeof(T).Name;
            using (context.contextLock.Lock())
            {
                return context.logWarnings.Count(m => m.message != null && m.message.Contains(typeName) && m.message.Contains("type-own"));
            }
        }

        [Theory]
        [InlineData(TypeSelfConfigurationMode.IgnoreButWarn, 1)]
        [InlineData(TypeSelfConfigurationMode.Ignore, 0)]
        [InlineData(TypeSelfConfigurationMode.Enabled, 0)]
        public void Warning_IsLoggedOncePerType_OnlyInIgnoreButWarn(TypeSelfConfigurationMode mode, int expectedWarnings)
        {
            using var testContext = TestHelper.PrepareTestContext();
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { typeSelfConfigurationMode = mode });

            Assert.True(deserializer.TryDeserialize(Json, out SelfConfigured _));
            Assert.True(deserializer.TryDeserialize(Json, out SelfConfigured _));
            Assert.True(deserializer.TryDeserialize("{\"First\":" + Json + ",\"Second\":" + Json + "}", out SelfConfiguredHolder _));

            Assert.Equal(expectedWarnings, CountWarningsFor<SelfConfigured>(testContext));
        }
    }
}
