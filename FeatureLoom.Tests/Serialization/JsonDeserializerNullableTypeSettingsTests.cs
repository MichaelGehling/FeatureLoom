using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerNullableTypeSettingsTests
    {
        public struct NullableSettingsStruct
        {
            public int X;
            public int Y;
        }

        public class NullableSettingsHolder
        {
            public NullableSettingsStruct? Value;
        }

        private static JsonDeserializer.Settings CreateSettings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<NullableSettingsStruct>(ts => ts.AddConstructor(() => new NullableSettingsStruct { Y = 42 }));
            return settings;
        }

        [Fact]
        public void NullableStruct_WithoutSettings_Deserializes()
        {
            var deserializer = new JsonDeserializer();
            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out NullableSettingsStruct? value));
            Assert.True(value.HasValue);
            Assert.Equal(5, value.Value.X);
        }

        [Fact]
        public void TypeSettings_OfUnderlyingType_ApplyToNullableRoot()
        {
            var deserializer = new JsonDeserializer(CreateSettings());
            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out NullableSettingsStruct? value));

            Assert.True(value.HasValue);
            Assert.Equal(5, value.Value.X);
            Assert.Equal(42, value.Value.Y);
        }

        [Fact]
        public void TypeSettings_OfUnderlyingType_ApplyToNullableMember()
        {
            var deserializer = new JsonDeserializer(CreateSettings());
            Assert.True(deserializer.TryDeserialize("{\"Value\":{\"X\":5}}", out NullableSettingsHolder holder));

            Assert.True(holder.Value.HasValue);
            Assert.Equal(5, holder.Value.Value.X);
            Assert.Equal(42, holder.Value.Value.Y);
        }

        [Fact]
        public void TypeSettings_OfUnderlyingType_NullableNullStaysNull()
        {
            var deserializer = new JsonDeserializer(CreateSettings());
            Assert.True(deserializer.TryDeserialize("{\"Value\":null}", out NullableSettingsHolder holder));

            Assert.False(holder.Value.HasValue);
        }
    }
}
