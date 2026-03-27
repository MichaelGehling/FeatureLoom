using FeatureLoom.Collections;
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
                api =>
                {
                    Assert.True(api.TryReadStringValueOrNull(out string text));
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
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomArrayReadType { Raw = raw };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomArrayReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadBoolValue()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomBoolReadType>(
                api =>
                {
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomBoolReadType { Value = b };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("true", out CustomBoolReadType value));
            Assert.True(value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadSignedIntegerValue()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomLongReadType>(
                api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    return new CustomLongReadType { Value = n };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-123", out CustomLongReadType value));
            Assert.Equal(-123L, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadFloatingPointValue_ForSpecialNumber()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomDoubleReadType>(
                api =>
                {
                    Assert.True(api.TryReadFloatingPointValue(out double n));
                    return new CustomDoubleReadType { Value = n };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"NaN\"", out CustomDoubleReadType value));
            Assert.True(double.IsNaN(value.Value));
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadUnsignedIntegerValue()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomUlongReadType>(
                api =>
                {
                    Assert.True(api.TryReadUnsignedIntegerValue(out ulong n));
                    return new CustomUlongReadType { Success = true, Value = n };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("123", out CustomUlongReadType value));
            Assert.True(value.Success);
            Assert.Equal(123UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadUnsignedIntegerValue_FailsForNegative()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomUlongReadType>(
                api =>
                {
                    bool success = api.TryReadUnsignedIntegerValue(out ulong n);
                    return new CustomUlongReadType { Success = success, Value = n };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-1", out CustomUlongReadType value));
            Assert.False(value.Success);
            Assert.Equal(0UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadNullValue()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomNullReadType>(
                api => new CustomNullReadType { IsNull = api.TryReadNullValue() });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("null", out CustomNullReadType value));
            Assert.True(value.IsNull);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForObject()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawBytesReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomRawBytesReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForArray()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawBytesReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomRawBytesReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadObjectValue_DictionaryOverload()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomObjectValueReadType>(
                api =>
                {
                    bool success = api.TryReadObjectValue(out Dictionary<string, object> obj, default);
                    return new CustomObjectValueReadType
                    {
                        Success = success,
                        Count = success ? obj.Count : 0,
                        A = success ? (int)obj["A"] : 0
                    };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"A\":7,\"B\":\"x\"}", out CustomObjectValueReadType value));
            Assert.True(value.Success);
            Assert.Equal(2, value.Count);
            Assert.Equal(7, value.A);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadArrayValue_ListObjectOverload()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomArrayValueReadType>(
                api =>
                {
                    bool success = api.TryReadArrayValue(out List<object> array, default);
                    return new CustomArrayValueReadType
                    {
                        Success = success,
                        Count = success ? array.Count : 0,
                        First = success ? (int)array[0] : 0
                    };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[5,\"x\",null]", out CustomArrayValueReadType value));
            Assert.True(value.Success);
            Assert.Equal(3, value.Count);
            Assert.Equal(5, value.First);
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

        private class CustomBoolReadType
        {
            public bool Value;
        }

        private class CustomLongReadType
        {
            public long Value;
        }

        private class CustomDoubleReadType
        {
            public double Value;
        }

        private class CustomUlongReadType
        {
            public bool Success;
            public ulong Value;
        }

        private struct CustomNullReadType
        {
            public bool IsNull;
        }

        private class CustomRawBytesReadType
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
        private class CustomObjectValueReadType
        {
            public bool Success;
            public int Count;
            public int A;
        }

        private class CustomArrayValueReadType
        {
            public bool Success;
            public int Count;
            public int First;
        }

        private class CustomCursorReadType
        {
            public byte FirstByte;
            public byte FirstNonWhitespaceByte;
            public bool BoolValue;
        }

        private class CustomTryNextByteReadType
        {
            public byte First;
            public byte Second;
            public bool Moved;
        }

        private class CustomSkipValueReadType
        {
            public bool TailBool;
        }

        private class CustomRawTryResultType
        {
            public bool Success;
            public string Raw;
        }

        private class CustomStringTryResultType
        {
            public bool Success;
            public string Text;
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryNextByte()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomTryNextByteReadType>(
                api =>
                {
                    byte first = api.GetCurrentByte();
                    bool moved = api.TryNextByte();
                    byte second = api.GetCurrentByte();
                    return new CustomTryNextByteReadType
                    {
                        First = first,
                        Moved = moved,
                        Second = second
                    };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("12", out CustomTryNextByteReadType value));
            Assert.Equal((byte)'1', value.First);
            Assert.True(value.Moved);
            Assert.Equal((byte)'2', value.Second);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesSkipNextValue()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomSkipValueReadType>(
                api =>
                {
                    api.SkipNextValue();
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomSkipValueReadType { TailBool = b };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2] false", out CustomSkipValueReadType value));
            Assert.False(value.TailBool);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadRawJsonValue_FailurePath_ReturnsFalse()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawTryResultType>(
                api =>
                {
                    bool success = api.TryReadRawJsonValue(out string raw);
                    return new CustomRawTryResultType { Success = success, Raw = raw };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{", out CustomRawTryResultType value));
            Assert.False(value.Success);
            Assert.Null(value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadStringValueOrNull_FailsOnNumber()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomStringTryResultType>(
                api =>
                {
                    bool success = api.TryReadStringValueOrNull(out string text);
                    return new CustomStringTryResultType { Success = success, Text = text };
                });

            var deserializer = new FeatureJsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("123", out CustomStringTryResultType value));
            Assert.False(value.Success);
            Assert.Null(value.Text);
        }
    }
}