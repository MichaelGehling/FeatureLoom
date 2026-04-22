using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerSettingsTests
    {
        private static MemoryStream Utf8Stream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

        [Fact]
        public void Settings_Strict_False_AllowsNumberInString()
        {
            var settings = new JsonDeserializer.Settings
            {
                strict = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"123\"", out int value));
            Assert.Equal(123, value);
        }

        [Fact]
        public void Settings_Strict_True_RejectsNumberInString()
        {
            var settings = new JsonDeserializer.Settings
            {
                strict = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("\"123\"", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Settings_RethrowExceptions_False_ReturnsFalseOnInvalidJson()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Settings_RethrowExceptions_True_ThrowsOnInvalidJson()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = true,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.ThrowsAny<System.Exception>(() => deserializer.TryDeserialize("{", out int _));
        }

        [Fact]
        public void Settings_InitialBufferSize_Small_WorksWithStreamAndBufferGrowth()
        {
            var settings = new JsonDeserializer.Settings
            {
                initialBufferSize = 8
            };
            var deserializer = new JsonDeserializer(settings);

            const string json = "{\"A\":123,\"B\":\"abcdefghijklmnopqrstuvwxyz\"}";
            using var stream = Utf8Stream(json);

            Assert.True(deserializer.TryDeserialize(stream, out BufferSample value));
            Assert.Equal(123, value.A);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz", value.B);
        }

        [Fact]
        public void Settings_TypeMapping_UsesMappedType()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddTypeMapping(typeof(MappedBase), typeof(MappedDerived));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out MappedBase value));
            Assert.IsType<MappedDerived>(value);
            Assert.Equal(5, value.X);
        }

        [Fact]
        public void Settings_GenericTypeMapping_CanOverrideIEnumerableMapping()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddTypeMapping(typeof(IEnumerable<>), typeof(Queue<>));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            var queue = Assert.IsType<Queue<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());
        }

        [Fact]
        public void Settings_AddConstructor_UsesConfiguredConstructor()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddConstructor(() => new NoDefaultCtor(42));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{}", out NoDefaultCtor value));
            Assert.Equal(42, value.X);
        }

        [Fact]
        public void Settings_AddConstructorWithParameter_UsesConfiguredConstructor()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddConstructorWithParameter<EnumerableWrapper, IEnumerable<int>>(values => new EnumerableWrapper(values));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[4,5,6]", out EnumerableWrapper value));
            Assert.Equal(new[] { 4, 5, 6 }, value.Items);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomReadType>(
                (api, item) =>
                {
                    Assert.True(api.TryReadStringValueOrNull(out string text));
                    item.Value = text;
                    return item;
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"custom\"", out CustomReadType value));
            Assert.Equal("custom", value.Value);
        }

        [Fact]
        public void Settings_Strict_False_AllowsEmptyStringForNullableInt()
        {
            var settings = new JsonDeserializer.Settings
            {
                strict = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"\"", out int? value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_Strict_True_RejectsEmptyStringForNullableInt()
        {
            var settings = new JsonDeserializer.Settings
            {
                strict = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("\"\"", out int? value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_MultiOptionTypeMapping_SelectsMatchingType()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(IMultiOption), typeof(MultiOptionA), typeof(MultiOptionB), typeof(MultiOptionDict));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"FieldB\":7}", out IMultiOption value));
            var selected = Assert.IsType<MultiOptionB>(value);
            Assert.Equal(7, selected.FieldB);
        }

        [Fact]
        public void Settings_MultiOptionTypeMapping_FallsBackToDictionaryOption()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(IMultiOption), typeof(MultiOptionA), typeof(MultiOptionB), typeof(MultiOptionDict));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Unknown\":9}", out IMultiOption value));
            var dict = Assert.IsType<MultiOptionDict>(value);
            Assert.Equal(9, (int)dict["Unknown"]);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ObjectCategory_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomObjectReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomObjectReadType { Raw = raw };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomObjectReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ArrayCategory_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomArrayReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomArrayReadType { Raw = raw };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomArrayReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadBoolValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomBoolReadType>(
                api =>
                {
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomBoolReadType { Value = b };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("true", out CustomBoolReadType value));
            Assert.True(value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadSignedIntegerValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomLongReadType>(
                api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    return new CustomLongReadType { Value = n };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-123", out CustomLongReadType value));
            Assert.Equal(-123L, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadFloatingPointValue_ForSpecialNumber()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomDoubleReadType>(
                api =>
                {
                    Assert.True(api.TryReadFloatingPointValue(out double n));
                    return new CustomDoubleReadType { Value = n };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"NaN\"", out CustomDoubleReadType value));
            Assert.True(double.IsNaN(value.Value));
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadUnsignedIntegerValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomUlongReadType>(
                api =>
                {
                    Assert.True(api.TryReadUnsignedIntegerValue(out ulong n));
                    return new CustomUlongReadType { Success = true, Value = n };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("123", out CustomUlongReadType value));
            Assert.True(value.Success);
            Assert.Equal(123UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadUnsignedIntegerValue_FailsForNegative()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomUlongReadType>(
                api =>
                {
                    bool success = api.TryReadUnsignedIntegerValue(out ulong n);
                    return new CustomUlongReadType { Success = success, Value = n };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-1", out CustomUlongReadType value));
            Assert.False(value.Success);
            Assert.Equal(0UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadNullValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomNullReadType>(
                api => new CustomNullReadType { IsNull = api.TryReadNullValue() });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("null", out CustomNullReadType value));
            Assert.True(value.IsNull);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForObject()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawBytesReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomRawBytesReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForArray()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawBytesReadType>(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomRawBytesReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadObjectValue_DictionaryOverload()
        {
            var settings = new JsonDeserializer.Settings();
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

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"A\":7,\"B\":\"x\"}", out CustomObjectValueReadType value));
            Assert.True(value.Success);
            Assert.Equal(2, value.Count);
            Assert.Equal(7, value.A);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadArrayValue_ListObjectOverload()
        {
            var settings = new JsonDeserializer.Settings();
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

            var deserializer = new JsonDeserializer(settings);

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

        private class NoDefaultCtorReadonlyField
        {
            public readonly int X;

            public NoDefaultCtorReadonlyField(int x)
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
            var settings = new JsonDeserializer.Settings();
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

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("12", out CustomTryNextByteReadType value));
            Assert.Equal((byte)'1', value.First);
            Assert.True(value.Moved);
            Assert.Equal((byte)'2', value.Second);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesSkipNextValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomSkipValueReadType>(
                api =>
                {
                    api.SkipNextValue();
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomSkipValueReadType { TailBool = b };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2] false", out CustomSkipValueReadType value));
            Assert.False(value.TailBool);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadRawJsonValue_FailurePath_ReturnsFalse()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomRawTryResultType>(
                api =>
                {
                    bool success = api.TryReadRawJsonValue(out string raw);
                    return new CustomRawTryResultType { Success = success, Raw = raw };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{", out CustomRawTryResultType value));
            Assert.False(value.Success);
            Assert.Null(value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadStringValueOrNull_FailsOnNumber()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddCustomTypeReader<CustomStringTryResultType>(
                api =>
                {
                    bool success = api.TryReadStringValueOrNull(out string text);
                    return new CustomStringTryResultType { Success = success, Text = text };
                });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("123", out CustomStringTryResultType value));
            Assert.False(value.Success);
            Assert.Null(value.Text);
        }

        [Fact]
        public void Settings_AllowUninitializedObjectCreation_PublicAndPrivateFields_CanCreateTypeWithoutDefaultConstructor()
        {
            var settings = new JsonDeserializer.Settings
            {
                allowUninitializedObjectCreation = true,
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":7}", out NoDefaultCtorReadonlyField value));
            Assert.Equal(7, value.X);
        }

        [Fact]
        public void Settings_AllowUninitializedObjectCreation_PublicFieldsAndProperties_IsDisallowed()
        {
            var settings = new JsonDeserializer.Settings
            {
                allowUninitializedObjectCreation = true,
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"X\":7}", out NoDefaultCtorReadonlyField value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_DirectType_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddForbiddenType(typeof(ForbiddenType));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"Value\":1}", out ForbiddenType value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_AsMemberType_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddForbiddenType(typeof(ForbiddenType));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"Child\":{\"Value\":1}}", out HasForbiddenMember value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_AsGenericTypeArgument_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddForbiddenType(typeof(ForbiddenType));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"Item\":{\"Value\":1}}", out GenericHolder<ForbiddenType> value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_ForbiddenGenericTypeDefinition_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddForbiddenType(typeof(List<>));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("[1,2,3]", out List<int> value));
            Assert.Null(value);
        }
        private class ForbiddenType
        {
            public int Value;
        }

        private class HasForbiddenMember
        {
            public ForbiddenType Child;
        }

        private class GenericHolder<T>
        {
            public T Item;
        }

        [Fact]
        public void Settings_ForbiddenTypes_DelegateBase_BlocksFuncType()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ClearForbiddenTypes();
            settings.AddForbiddenType(typeof(Delegate));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("null", out Func<int> value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_DelegateBase_BlocksFuncMemberType()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ClearForbiddenTypes();
            settings.AddForbiddenType(typeof(Delegate));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"Factory\":null}", out HasFuncMember value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_ExpressionBase_BlocksExpressionDerivedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ClearForbiddenTypes();
            settings.AddForbiddenType(typeof(System.Linq.Expressions.Expression));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("null", out System.Linq.Expressions.Expression<Func<int>> value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_BaseType_BlocksDerivedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ClearForbiddenTypes();
            settings.AddForbiddenType(typeof(System.IO.FileSystemInfo));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("null", out System.IO.FileInfo value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ForbiddenTypes_GenericInterfaceDefinition_BlocksImplementingConcreteType()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false,
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore
            };
            settings.ClearForbiddenTypes();
            settings.AddForbiddenType(typeof(IEnumerable<>));

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("[1,2,3]", out List<int> value));
            Assert.Null(value);
        }

        private class HasFuncMember
        {
            public Func<int> Factory;
        }

        [Fact]
        public void Settings_Whitelist_ForProposedTypesOnly_NonWhitelistedProposedWithoutValue_FallsBackToExpectedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForProposedTypesOnly,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };

            var deserializer = new JsonDeserializer(settings);
            string blockedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(WhitelistBlockedDerived));
            string json = $"{{\"$type\":\"{blockedTypeName}\",\"A\":7,\"B\":11}}";

            Assert.True(deserializer.TryDeserialize(json, out WhitelistExpectedConcrete value));
            Assert.NotNull(value);
            Assert.Equal(7, value.A);
            Assert.IsType<WhitelistExpectedConcrete>(value);
        }

        [Fact]
        public void Settings_Whitelist_ForProposedTypesOnly_NonWhitelistedProposedWithValue_FallsBackToExpectedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForProposedTypesOnly,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };

            var deserializer = new JsonDeserializer(settings);
            string blockedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(WhitelistBlockedDerived));
            string json = $"{{\"$type\":\"{blockedTypeName}\",\"$value\":{{\"A\":9,\"B\":12}},\"ignored\":1}}";

            Assert.True(deserializer.TryDeserialize(json, out WhitelistExpectedConcrete value));
            Assert.NotNull(value);
            Assert.Equal(9, value.A);
            Assert.IsType<WhitelistExpectedConcrete>(value);
        }

        [Fact]
        public void Settings_Whitelist_ForProposedTypesOnly_NonWhitelistedProposedForInterface_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForProposedTypesOnly,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };

            var deserializer = new JsonDeserializer(settings);
            string blockedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(WhitelistBlockedDerived));
            string json = $"{{\"$type\":\"{blockedTypeName}\",\"A\":7,\"B\":11}}";

            Assert.False(deserializer.TryDeserialize(json, out IWhitelistExpected value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_Whitelist_ForAllNonIntrinsicTypes_AllowedNormalType_Deserializes()
        {
            var settings = new JsonDeserializer.Settings
            {
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddAllowedType<WhitelistAllowedType>();

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"A\":42}", out WhitelistAllowedType value));
            Assert.NotNull(value);
            Assert.Equal(42, value.A);
        }

        [Fact]
        public void Settings_Whitelist_ForAllNonIntrinsicTypes_AllowedGenericTypeDefinition_Deserializes()
        {
            var settings = new JsonDeserializer.Settings
            {
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddAllowedType(typeof(WhitelistGenericBox<>));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Item\":7}", out WhitelistGenericBox<int> value));
            Assert.NotNull(value);
            Assert.Equal(7, value.Item);
        }

        [Fact]
        public void Settings_Whitelist_ForAllNonIntrinsicTypes_AllowedNamespacePrefix_Deserializes()
        {
            var settings = new JsonDeserializer.Settings
            {
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddAllowedNamespacePrefix("FeatureLoom.Serialization");

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"A\":99}", out WhitelistNamespaceAllowedType value));
            Assert.NotNull(value);
            Assert.Equal(99, value.A);
        }

        private interface IWhitelistExpected
        {
            int A { get; set; }
        }

        private class WhitelistExpectedConcrete : IWhitelistExpected
        {
            public int A;
            int IWhitelistExpected.A
            {
                get => A;
                set => A = value;
            }
        }

        private class WhitelistBlockedDerived : WhitelistExpectedConcrete
        {
            public int B;
        }

        private class WhitelistAllowedType
        {
            public int A;
        }

        private class WhitelistGenericBox<T>
        {
            public T Item;
        }

        private class WhitelistNamespaceAllowedType
        {
            public int A;
        }
    }
}