using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System.Linq;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerTypeSelfConfigurationTests
    {
        public class SelfConfigured
        {
            public int A = 1;
            public int B = 2;

            [JsonTypeConfiguration]
            private static void ConfigureWriting(JsonSerializer.TypeWriteSettings<SelfConfigured> s)
            {
                s.ConfigureMember<int>(nameof(B), m => m.SetIgnore());
                s.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo);
                s.SetCustomTypeName("SC");
            }
        }

        public struct SelfConfiguredStruct
        {
            public int A;
            public int B;

            [JsonTypeConfiguration]
            static void ConfigureWriting(JsonSerializer.TypeWriteSettings<SelfConfiguredStruct> s)
                => s.ConfigureMember<int>(nameof(B), m => m.SetIgnore());
        }

        public class DerivedFromSelfConfigured : SelfConfigured
        {
        }

        public class GenericSelfConfigured<T>
        {
            public T Value;
            public int Hidden = 3;

            [JsonTypeConfiguration]
            static void ConfigureWriting(JsonSerializer.TypeWriteSettings<GenericSelfConfigured<T>> s)
                => s.ConfigureMember<int>(nameof(Hidden), m => m.SetIgnore());
        }

        public class InvalidSignature
        {
            public int A;

            [JsonTypeConfiguration]
            static void ConfigureWriting(JsonSerializer.TypeWriteSettings<SelfConfigured> s) { }
        }

        public class StructHolder
        {
            public SelfConfiguredStruct? Value;
        }

        static string Serialize<T>(T value, TypeSelfConfigurationMode mode, System.Action<JsonSerializer.Settings> configure = null)
        {
            var settings = new JsonSerializer.Settings { typeSelfConfigurationMode = mode };
            configure?.Invoke(settings);
            return new JsonSerializer(settings).Serialize(value);
        }

        [Fact]
        public void Enabled_AppliesTypeOwnConfiguration()
        {
            Assert.Equal("{\"$type\":\"SC\",\"A\":1}", Serialize(new SelfConfigured(), TypeSelfConfigurationMode.Enabled));
        }

        [Theory]
        [InlineData(TypeSelfConfigurationMode.Ignore)]
        [InlineData(TypeSelfConfigurationMode.IgnoreButWarn)]
        public void IgnoreModes_DoNotApplyTypeOwnConfiguration(TypeSelfConfigurationMode mode)
        {
            Assert.Equal("{\"A\":1,\"B\":2}", Serialize(new SelfConfigured(), mode));
        }

        [Fact]
        public void Default_IsIgnoreButWarn()
        {
            Assert.Equal(TypeSelfConfigurationMode.IgnoreButWarn, new JsonSerializer.Settings().typeSelfConfigurationMode);
        }

        [Fact]
        public void SettingsEntry_OverridesTypeOwnConfiguration_PerOption()
        {
            string json = Serialize(new SelfConfigured(), TypeSelfConfigurationMode.Enabled,
                s => s.ConfigureType<SelfConfigured>(t => t.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)));

            // Type info handling from the settings wins, the ignored member from the type-own configuration still applies.
            Assert.Equal("{\"A\":1}", json);
        }

        [Fact]
        public void SettingsMemberEntry_OverridesTypeOwnMemberConfiguration()
        {
            string json = Serialize(new SelfConfigured(), TypeSelfConfigurationMode.Enabled,
                s => s.ConfigureType<SelfConfigured>(t => t.ConfigureMember<int>(nameof(SelfConfigured.B), m => m.SetIgnore(false))));

            Assert.Equal("{\"$type\":\"SC\",\"A\":1,\"B\":2}", json);
        }

        [Fact]
        public void EnabledKeepRefTrackingOff_BehavesLikeEnabledOnSerializer()
        {
            Assert.Equal("{\"$type\":\"SC\",\"A\":1}", Serialize(new SelfConfigured(), TypeSelfConfigurationMode.EnabledKeepRefTrackingOff));
        }

        [Fact]
        public void Struct_AndNullableStruct_ApplyTypeOwnConfiguration()
        {
            var value = new SelfConfiguredStruct { A = 1, B = 2 };
            Assert.Equal("{\"A\":1}", Serialize(value, TypeSelfConfigurationMode.Enabled));
            Assert.Equal("{\"Value\":{\"A\":1}}", Serialize(new StructHolder { Value = value }, TypeSelfConfigurationMode.Enabled));
        }

        [Fact]
        public void DerivedType_DoesNotInheritTypeOwnConfiguration()
        {
            Assert.Equal("{\"A\":1,\"B\":2}", Serialize(new DerivedFromSelfConfigured(), TypeSelfConfigurationMode.Enabled));
        }

        [Fact]
        public void GenericType_AppliesTypeOwnConfigurationOfClosedType()
        {
            Assert.Equal("{\"Value\":\"x\"}", Serialize(new GenericSelfConfigured<string> { Value = "x" }, TypeSelfConfigurationMode.Enabled));
        }

        [Fact]
        public void InvalidSignature_Throws()
        {
            Assert.ThrowsAny<System.Exception>(() => Serialize(new InvalidSignature(), TypeSelfConfigurationMode.Enabled));
        }

        [Fact]
        public void ApplyTypeSelfConfiguration_WorksWithIgnoreMode()
        {
            string json = Serialize(new SelfConfigured(), TypeSelfConfigurationMode.Ignore, s => s.ApplyTypeSelfConfiguration<SelfConfigured>());
            Assert.Equal("{\"$type\":\"SC\",\"A\":1}", json);
        }

        public class SelfConfiguredHolder
        {
            public SelfConfigured First = new SelfConfigured();
            public SelfConfigured Second = new SelfConfigured();
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
            var serializer = new JsonSerializer(new JsonSerializer.Settings { typeSelfConfigurationMode = mode });

            serializer.Serialize(new SelfConfigured());
            serializer.Serialize(new SelfConfigured());
            serializer.Serialize(new SelfConfiguredHolder());

            Assert.Equal(expectedWarnings, CountWarningsFor<SelfConfigured>(testContext));
        }
    }
}
