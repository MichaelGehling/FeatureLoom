using FeatureLoom.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerSettingsTests
    {
        private static MemoryStream Utf8Stream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

        [Fact]
        public void Settings_Strict_False_AllowsNumberInString()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                strict = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"123\"", out int value));
            Assert.Equal(123, value);
        }

        [Fact]
        public void Settings_Strict_True_RejectsNumberInString()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                strict = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("\"123\"", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Settings_RethrowExceptions_False_ReturnsFalseOnInvalidJson()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Settings_RethrowExceptions_True_ThrowsOnInvalidJson()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                rethrowExceptions = true,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.ThrowsAny<System.Exception>(() => deserializer.TryDeserialize("{", out int _));
        }

        [Fact]
        public void Settings_InitialBufferSize_Small_WorksWithStreamAndBufferGrowth()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                initialBufferSize = 8
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            const string json = "{\"A\":123,\"B\":\"abcdefghijklmnopqrstuvwxyz\"}";
            using var stream = Utf8Stream(json);

            Assert.True(deserializer.TryDeserialize(stream, out BufferSample value));
            Assert.Equal(123, value.A);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz", value.B);
        }

        [Fact]
        public void Settings_TypeMapping_UsesMappedType()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddTypeMapping(typeof(MappedBase), typeof(MappedDerived));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out MappedBase value));
            Assert.IsType<MappedDerived>(value);
            Assert.Equal(5, value.X);
        }

        [Fact]
        public void Settings_GenericTypeMapping_CanOverrideIEnumerableMapping()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddGenericTypeMapping(typeof(IEnumerable<>), typeof(Queue<>));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            var queue = Assert.IsType<Queue<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());
        }

        [Fact]
        public void Settings_AddConstructor_UsesConfiguredConstructor()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddConstructor(() => new NoDefaultCtor(42));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{}", out NoDefaultCtor value));
            Assert.Equal(42, value.X);
        }

        [Fact]
        public void Settings_AddConstructorWithParameter_UsesConfiguredConstructor()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddConstructorWithParameter<EnumerableWrapper, IEnumerable<int>>(values => new EnumerableWrapper(values));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[4,5,6]", out EnumerableWrapper value));
            Assert.Equal(new[] { 4, 5, 6 }, value.Items);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesCustomReader()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomReadType>(
                JsonDataTypeCategory.Primitive,
                StringRepresentation.Yes,
                api =>
                {
                    Assert.True(api.TryReadStringValue(out string text));
                    return new CustomReadType { Value = text };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"custom\"", out CustomReadType value));
            Assert.Equal("custom", value.Value);
        }

        [Fact]
        public void Settings_Strict_False_AllowsEmptyStringForNullableInt()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                strict = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"\"", out int? value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_Strict_True_RejectsEmptyStringForNullableInt()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                strict = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("\"\"", out int? value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_MultiOptionTypeMapping_SelectsMatchingType()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(IMultiOption), typeof(MultiOptionA), typeof(MultiOptionB), typeof(MultiOptionDict));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"FieldB\":7}", out IMultiOption value));
            var selected = Assert.IsType<MultiOptionB>(value);
            Assert.Equal(7, selected.FieldB);
        }

        [Fact]
        public void Settings_MultiOptionTypeMapping_FallsBackToDictionaryOption()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(IMultiOption), typeof(MultiOptionA), typeof(MultiOptionB), typeof(MultiOptionDict));

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Unknown\":9}", out IMultiOption value));
            var dict = Assert.IsType<MultiOptionDict>(value);
            Assert.Equal(9, (int)dict["Unknown"]);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ObjectCategory_UsesCustomReader()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomObjectReadType>(
                JsonDataTypeCategory.Object,
                StringRepresentation.No,
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomObjectReadType { Raw = raw };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomObjectReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ArrayCategory_UsesCustomReader()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomArrayReadType>(
                JsonDataTypeCategory.Array,
                StringRepresentation.No,
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomArrayReadType { Raw = raw };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomArrayReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        private class BufferSample
        {
            public int A;
            public string B;
        }

        private class MappedBase
        {
            public int X;
        }

        private class MappedDerived : MappedBase
        {
        }

        private class NoDefaultCtor
        {
            public int X;

            private NoDefaultCtor()
            {
            }

            public NoDefaultCtor(int x)
            {
                X = x;
            }
        }

        private class EnumerableWrapper : IEnumerable<int>
        {
            public List<int> Items { get; }

            private EnumerableWrapper()
            {
                Items = new List<int>();
            }

            public EnumerableWrapper(IEnumerable<int> values)
            {
                Items = values.ToList();
            }

            public IEnumerator<int> GetEnumerator() => Items.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class CustomReadType
        {
            public string Value;
        }

        private class CustomObjectReadType
        {
            public string Raw;
        }

        private class CustomArrayReadType
        {
            public string Raw;
        }

        private interface IMultiOption
        {
        }

        private class MultiOptionA : IMultiOption
        {
            public int FieldA;
        }

        private class MultiOptionB : IMultiOption
        {
            public int FieldB;
        }

        private class MultiOptionDict : Dictionary<string, object>, IMultiOption
        {
        }
    }
}