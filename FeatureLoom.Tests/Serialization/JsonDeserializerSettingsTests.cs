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
            settings.ConfigureType<MappedBase>(typeSettings => typeSettings.SetInstanceTypeMapping<MappedDerived>());            

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out MappedBase value));
            Assert.IsType<MappedDerived>(value);
            Assert.Equal(5, value.X);
        }

        [Fact]
        public void Settings_GenericTypeMapping_CanOverrideIEnumerableMapping()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureGenericType(typeof(IEnumerable<>), typeSettings => typeSettings.SetInstanceTypeMapping(typeof(Queue<>)));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            var queue = Assert.IsType<Queue<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());
        }

        [Fact]
        public void Settings_AddConstructor_UsesConfiguredConstructor()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<NoDefaultCtor>(typeSettings => typeSettings.AddConstructor(() => new NoDefaultCtor(42)));            

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{}", out NoDefaultCtor value));
            Assert.Equal(42, value.X);
        }

        [Fact]
        public void Settings_AddConstructorWithParameter_UsesConfiguredConstructor()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<EnumerableWrapper>(typeSettings => typeSettings.AddCollectionConstructor<int>(values => new EnumerableWrapper(values)));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[4,5,6]", out EnumerableWrapper value));
            Assert.Equal(new[] { 4, 5, 6 }, value.Items);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                (api, item) =>
                {
                    Assert.True(api.TryReadStringValueOrNull(out string text));
                    item.Value = text;
                    return item;
                }));

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
            settings.ConfigureType<IMultiOption>(typeSettings =>
            {
                typeSettings.AddInstanceTypeMappingOption<MultiOptionA>();
                typeSettings.AddInstanceTypeMappingOption<MultiOptionB>();
                typeSettings.AddInstanceTypeMappingOption<MultiOptionDict>();
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"FieldB\":7}", out IMultiOption value));
            var selected = Assert.IsType<MultiOptionB>(value);
            Assert.Equal(7, selected.FieldB);
        }

        [Fact]
        public void Settings_MultiOptionTypeMapping_FallsBackToDictionaryOption()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<IMultiOption>(typeSettings =>
            {
                typeSettings.AddInstanceTypeMappingOption<MultiOptionA>();
                typeSettings.AddInstanceTypeMappingOption<MultiOptionB>();
                typeSettings.AddInstanceTypeMappingOption<MultiOptionDict>();
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Unknown\":9}", out IMultiOption value));
            var dict = Assert.IsType<MultiOptionDict>(value);
            Assert.Equal(9, (int)dict["Unknown"]);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ObjectCategory_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomObjectReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomObjectReadType { Raw = raw };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomObjectReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_ArrayCategory_UsesCustomReader()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomArrayReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out string raw));
                    return new CustomArrayReadType { Raw = raw };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomArrayReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadBoolValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomBoolReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomBoolReadType { Value = b };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("true", out CustomBoolReadType value));
            Assert.True(value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadSignedIntegerValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomLongReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    return new CustomLongReadType { Value = n };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-123", out CustomLongReadType value));
            Assert.Equal(-123L, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadFloatingPointValue_ForSpecialNumber()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomDoubleReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadFloatingPointValue(out double n));
                    return new CustomDoubleReadType { Value = n };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("\"NaN\"", out CustomDoubleReadType value));
            Assert.True(double.IsNaN(value.Value));
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadUnsignedIntegerValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomUlongReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadUnsignedIntegerValue(out ulong n));
                    return new CustomUlongReadType { Success = true, Value = n };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("123", out CustomUlongReadType value));
            Assert.True(value.Success);
            Assert.Equal(123UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadUnsignedIntegerValue_FailsForNegative()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomUlongReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    bool success = api.TryReadUnsignedIntegerValue(out ulong n);
                    return new CustomUlongReadType { Success = success, Value = n };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("-1", out CustomUlongReadType value));
            Assert.False(value.Success);
            Assert.Equal(0UL, value.Value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadNullValue()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomNullReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api => new CustomNullReadType { IsNull = api.TryReadNullValue() }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("null", out CustomNullReadType value));
            Assert.True(value.IsNull);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForObject()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomRawBytesReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"a\":1}", out CustomRawBytesReadType value));
            Assert.Equal("{\"a\":1}", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadRawJsonValue_ByteSegment_ForArray()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomRawBytesReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadRawJsonValue(out ByteSegment rawBytes));
                    return new CustomRawBytesReadType { Raw = api.DecodeUtf8Bytes(rawBytes.AsArraySegment) };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out CustomRawBytesReadType value));
            Assert.Equal("[1,2,3]", value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_UsesTryReadObjectValue_DictionaryOverload()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomObjectValueReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    bool success = api.TryReadObjectValue(out Dictionary<string, object> obj, default);
                    return new CustomObjectValueReadType
                    {
                        Success = success,
                        Count = success ? obj.Count : 0,
                        A = success ? (int)obj["A"] : 0
                    };
                }));

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
            settings.ConfigureType<CustomArrayValueReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    bool success = api.TryReadArrayValue(out List<object> array, default);
                    return new CustomArrayValueReadType
                    {
                        Success = success,
                        Count = success ? array.Count : 0,
                        First = success ? (int)array[0] : 0
                    };
                }));

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
            settings.ConfigureType<CustomTryNextByteReadType>(typeSettings => typeSettings.SetCustomTypeReader(
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
                }));

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
            settings.ConfigureType<CustomSkipValueReadType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    api.SkipNextValue();
                    Assert.True(api.TryReadBoolValue(out bool b));
                    return new CustomSkipValueReadType { TailBool = b };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2] false", out CustomSkipValueReadType value));
            Assert.False(value.TailBool);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadRawJsonValue_FailurePath_ReturnsFalse()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomRawTryResultType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    bool success = api.TryReadRawJsonValue(out string raw);
                    return new CustomRawTryResultType { Success = success, Raw = raw };
                }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{", out CustomRawTryResultType value));
            Assert.False(value.Success);
            Assert.Null(value.Raw);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_TryReadStringValueOrNull_FailsOnNumber()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomStringTryResultType>(typeSettings => typeSettings.SetCustomTypeReader(
                api =>
                {
                    bool success = api.TryReadStringValueOrNull(out string text);
                    return new CustomStringTryResultType { Success = success, Text = text };
                }));

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

        [Fact]
        public void Settings_ConfigureMember_OverrideName_IsUsedForDeserialization()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberConfigPayload>(ts =>
            {
                ts.ConfigureMember<int>(nameof(MemberConfigPayload.Value), ms => ms.OverrideName("renamed"));
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"renamed\":7}", out MemberConfigPayload value));
            Assert.NotNull(value);
            Assert.Equal(7, value.Value);
        }

        [Fact]
        public void Settings_ConfigureMember_SetIgnore_IgnoresIncomingField()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberConfigPayload>(ts =>
            {
                ts.ConfigureMember<int>(nameof(MemberConfigPayload.Ignored), ms => ms.SetIgnore());
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Value\":3,\"Ignored\":99}", out MemberConfigPayload value));
            Assert.NotNull(value);
            Assert.Equal(3, value.Value);
            Assert.Equal(0, value.Ignored);
        }

        [Fact]
        public void Settings_ConfigureMember_SetInstanceTypeMapping_MapsOnlyThatMember()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberMappedContainer>(ts =>
            {
                ts.ConfigureMember<IMemberMapped>(nameof(MemberMappedContainer.Item), ms =>
                {
                    ms.SetInstanceTypeMapping<MemberMappedA>();
                });
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Item\":{\"A\":11}}", out MemberMappedContainer value));
            Assert.NotNull(value);
            var item = Assert.IsType<MemberMappedA>(value.Item);
            Assert.Equal(11, item.A);
        }

        [Fact]
        public void Settings_ConfigureMember_SetInstanceTypeMapping_WithNestedSettings_AppliesNestedSettings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberMappedContainer>(ts =>
            {
                ts.ConfigureMember<IMemberMapped>(nameof(MemberMappedContainer.Item), ms =>
                {
                    ms.SetInstanceTypeMapping<MemberMappedA>(mapped =>
                    {
                        mapped.ConfigureMember<int>(nameof(MemberMappedA.A), m => m.OverrideName("renamedA"));
                    });
                });
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Item\":{\"renamedA\":21}}", out MemberMappedContainer value));
            Assert.NotNull(value);
            var item = Assert.IsType<MemberMappedA>(value.Item);
            Assert.Equal(21, item.A);
        }

        [Fact]
        public void Settings_ConfigureMember_AddInstanceTypeMappingOption_WithNestedSettings_SelectsMatchingOption()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberChoiceContainer>(ts =>
            {
                ts.ConfigureMember<IMemberChoice>(nameof(MemberChoiceContainer.Choice), ms =>
                {
                    ms.AddInstanceTypeMappingOption<MemberChoiceA>(opt =>
                    {
                        opt.ConfigureMember<int>(nameof(MemberChoiceA.A), m => m.OverrideName("x"));
                    });
                    ms.AddInstanceTypeMappingOption<MemberChoiceB>(opt =>
                    {
                        opt.ConfigureMember<int>(nameof(MemberChoiceB.B), m => m.OverrideName("y"));
                    });
                });
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Choice\":{\"y\":5}}", out MemberChoiceContainer value));
            Assert.NotNull(value);
            var selected = Assert.IsType<MemberChoiceB>(value.Choice);
            Assert.Equal(5, selected.B);
        }

        [Fact]
        public void Settings_AddInstanceTypeMappingOption_ClearsSingleMapping()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<IClearMapping>(ts =>
            {
                ts.SetInstanceTypeMapping<ClearMappingA>();
                ts.AddInstanceTypeMappingOption<ClearMappingB>();
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"B\":2}", out IClearMapping value));
            Assert.IsType<ClearMappingB>(value);
        }

        [Fact]
        public void Settings_SetInstanceTypeMapping_ClearsMultiOptionMappings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<IClearMapping>(ts =>
            {
                ts.AddInstanceTypeMappingOption<ClearMappingA>();
                ts.AddInstanceTypeMappingOption<ClearMappingB>();
                ts.SetInstanceTypeMapping<ClearMappingA>();
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"B\":2}", out IClearMapping value));
            Assert.IsType<ClearMappingA>(value);
        }

        private class MemberConfigPayload
        {
            public int Value;
            public int Ignored;
        }

        private interface IMemberMapped
        {
        }

        private class MemberMappedA : IMemberMapped
        {
            public int A;
        }

        private class MemberMappedContainer
        {
            public IMemberMapped Item;
        }

        private interface IMemberChoice
        {
        }

        private class MemberChoiceA : IMemberChoice
        {
            public int A;
        }

        private class MemberChoiceB : IMemberChoice
        {
            public int B;
        }

        private class MemberChoiceContainer
        {
            public IMemberChoice Choice;
        }

        private interface IClearMapping
        {
        }

        private class ClearMappingA : IClearMapping
        {
            public int A;
        }

        private class ClearMappingB : IClearMapping
        {
            public int B;
        }

        [Fact]
        public void Settings_ConfigureType_Null_RemovesTypeSettings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MappedBase>(ts => ts.SetInstanceTypeMapping<MappedDerived>());
            settings.ConfigureType<MappedBase>(null);

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out MappedBase value));
            Assert.IsType<MappedBase>(value);
            Assert.Equal(5, value.X);
        }

        [Fact]
        public void Settings_ConfigureMember_Null_RemovesMemberSettings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<MemberConfigPayload>(ts =>
            {
                ts.ConfigureMember<int>(nameof(MemberConfigPayload.Value), ms => ms.OverrideName("renamed"));
                ts.ConfigureMember<int>(nameof(MemberConfigPayload.Value), null);
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Value\":13}", out MemberConfigPayload value1));
            Assert.Equal(13, value1.Value);

            Assert.True(deserializer.TryDeserialize("{\"renamed\":13}", out MemberConfigPayload value2));
            Assert.Equal(0, value2.Value);
        }

        [Fact]
        public void Settings_CustomTypeNames_AddCaseVariants_True_AllowsLowerCaseAliasForProposedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                addCaseVariantsForCustomTypeNames = true
            };
            settings.AddCustomTypeName("MyCaseAlias", typeof(CaseDerived));

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"$type\":\"mycasealias\",\"V\":4}";

            Assert.True(deserializer.TryDeserialize(json, out ICaseBase value));
            var typed = Assert.IsType<CaseDerived>(value);
            Assert.Equal(4, typed.V);
        }

        [Fact]
        public void Settings_CustomTypeNames_AddCaseVariants_False_DoesNotAllowLowerCaseAliasForProposedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                addCaseVariantsForCustomTypeNames = false,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddCustomTypeName("MyCaseAlias", typeof(CaseDerived));

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"$type\":\"mycasealias\",\"V\":4}";

            Assert.False(deserializer.TryDeserialize(json, out ICaseBase value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ClearCustomTypeNames_RemovesPreviouslyAddedAlias()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.AddCustomTypeName("AliasToRemove", typeof(CaseDerived));
            settings.ClearCustomTypeNames();

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"$type\":\"AliasToRemove\",\"V\":10}";

            Assert.False(deserializer.TryDeserialize(json, out ICaseBase value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_TypeWhitelist_ForAllNonIntrinsicTypes_NonWhitelistedType_IsRejected()
        {
            var settings = new JsonDeserializer.Settings
            {
                typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"A\":1}", out NonWhitelistedType value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_WithPreparationApiCreator_UsesPreparedReader()
        {
            bool prepared = false;

            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<PreparedReaderType>(ts => ts.SetCustomTypeReader(prep =>
            {
                prepared = true;
                return (api, item) =>
                {
                    item.Value = 77;
                    return item;
                };
            }));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{}", out PreparedReaderType value));
            Assert.True(prepared);
            Assert.Equal(77, value.Value);
        }

        private interface ICaseBase
        {
        }

        private class CaseDerived : ICaseBase
        {
            public int V;
        }

        private class NonWhitelistedType
        {
            public int A;
        }

        private class PreparedReaderType
        {
            public int Value;
        }

        [Fact]
        public void Settings_BackingFieldMode_TryBothNames_AcceptsPropertyNameAndBackingFieldName()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields,
                backingFieldMode = JsonDeserializer.Settings.BackingFieldMode.TryBothNames
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Auto\":3}", out BackingFieldSample fromPropertyName));
            Assert.Equal(3, fromPropertyName.Auto);

            Assert.True(deserializer.TryDeserialize("{\"<Auto>k__BackingField\":4}", out BackingFieldSample fromBackingFieldName));
            Assert.Equal(4, fromBackingFieldName.Auto);
        }

        [Fact]
        public void Settings_BackingFieldMode_TryPropertyNameOnly_IgnoresBackingFieldName()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields,
                backingFieldMode = JsonDeserializer.Settings.BackingFieldMode.TryPropertyNameOnly
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Auto\":5}", out BackingFieldSample fromPropertyName));
            Assert.Equal(5, fromPropertyName.Auto);

            Assert.True(deserializer.TryDeserialize("{\"<Auto>k__BackingField\":6}", out BackingFieldSample fromBackingFieldName));
            Assert.Equal(0, fromBackingFieldName.Auto);
        }

        [Fact]
        public void Settings_BackingFieldMode_TryBackingFieldNameOnly_IgnoresPropertyName()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields,
                backingFieldMode = JsonDeserializer.Settings.BackingFieldMode.TryBackingFieldNameOnly
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Auto\":7}", out BackingFieldSample fromPropertyName));
            Assert.Equal(0, fromPropertyName.Auto);

            Assert.True(deserializer.TryDeserialize("{\"<Auto>k__BackingField\":8}", out BackingFieldSample fromBackingFieldName));
            Assert.Equal(8, fromBackingFieldName.Auto);
        }

        [Fact]
        public void Settings_BackingFieldMode_TypeSetting_OverridesGlobalSetting()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields,
                backingFieldMode = JsonDeserializer.Settings.BackingFieldMode.TryPropertyNameOnly
            };
            settings.ConfigureType<BackingFieldSample>(ts =>
            {
                ts.SetBackingFieldMode(JsonDeserializer.Settings.BackingFieldMode.TryBackingFieldNameOnly);
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Auto\":9}", out BackingFieldSample fromPropertyName));
            Assert.Equal(0, fromPropertyName.Auto);

            Assert.True(deserializer.TryDeserialize("{\"<Auto>k__BackingField\":10}", out BackingFieldSample fromBackingFieldName));
            Assert.Equal(10, fromBackingFieldName.Auto);
        }

        private class BackingFieldSample
        {
            public int Auto { get; set; }
        }

        [Fact]
        public void Settings_ReferenceResolution_OnlyPerType_WithoutEnabledType_DoesNotResolveRef()
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.DisabledByDefault,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"A\":{\"Name\":\"alpha\"},\"B\":{\"$ref\":\"$.A\"}}";

            Assert.True(deserializer.TryDeserialize(json, out RefContainer value));
            Assert.NotNull(value);
            Assert.NotNull(value.A);
            Assert.NotNull(value.B);
            Assert.NotSame(value.A, value.B);
            Assert.Equal("alpha", value.A.Name);
            Assert.Null(value.B.Name);
        }

        [Fact]
        public void Settings_ReferenceResolution_OnlyPerType_WithEnabledType_ResolvesRef()
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.DisabledByDefault
            };
            settings.ConfigureType<RefNode>(ts => ts.SetReferenceResolution(true));

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"A\":{\"Name\":\"alpha\"},\"B\":{\"$ref\":\"$.A\"}}";

            Assert.True(deserializer.TryDeserialize(json, out RefContainer value));
            Assert.NotNull(value);
            Assert.NotNull(value.A);
            Assert.NotNull(value.B);
            Assert.Same(value.A, value.B);
            Assert.Equal("alpha", value.B.Name);
        }

        [Fact]
        public void Settings_ReferenceResolution_EnabledByDefault_ResolvesRef()
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault
            };

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"A\":{\"Name\":\"alpha\"},\"B\":{\"$ref\":\"$.A\"}}";

            Assert.True(deserializer.TryDeserialize(json, out RefContainer value));
            Assert.NotNull(value);
            Assert.NotNull(value.A);
            Assert.NotNull(value.B);
            Assert.Same(value.A, value.B);
        }

        [Fact]
        public void Settings_ReferenceResolution_EnabledByDefault_TypeOverrideFalse_DisablesRefForThatType()
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ConfigureType<RefNode>(ts => ts.SetReferenceResolution(false));

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"A\":{\"Name\":\"alpha\"},\"B\":{\"$ref\":\"$.A\"}}";

            Assert.True(deserializer.TryDeserialize(json, out RefContainer value));
            Assert.NotNull(value);
            Assert.NotNull(value.A);
            Assert.NotNull(value.B);
            Assert.NotSame(value.A, value.B);
            Assert.Null(value.B.Name);
        }

        private class RefContainer
        {
            public RefNode A;
            public RefNode B;
        }

        private class RefNode
        {
            public string Name;
        }

        [Fact]
        public void Settings_PopulateExistingMembers_True_PopulatesNestedMemberInsteadOfReplacing()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);

            var root = new PopulateRoot
            {
                Child = new PopulateChild { A = 1, B = 2 }
            };
            var originalChild = root.Child;

            Assert.True(deserializer.TryPopulate("{\"Child\":{\"A\":9}}", root));

            Assert.Same(originalChild, root.Child);
            Assert.Equal(9, root.Child.A);
            Assert.Equal(2, root.Child.B);
        }

        [Fact]
        public void Settings_PopulateExistingMembers_False_ReplacesNestedMember_DuringTryDeserialize()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Child\":{\"A\":9}}", out PopulateRoot value));

            Assert.NotNull(value);
            Assert.NotNull(value.Child);
            Assert.Equal(9, value.Child.A);
            Assert.Equal(0, value.Child.B);
        }

        [Fact]
        public void Settings_TypeSetting_SetPopulateAsMember_False_ReplacesNestedMemberEvenWhenGlobalPopulateTrue()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            settings.ConfigureType<PopulateChild>(ts => ts.SetPopulateAsMember(false));

            var deserializer = new JsonDeserializer(settings);

            var root = new PopulateRoot
            {
                Child = new PopulateChild { A = 1, B = 2 }
            };
            var originalChild = root.Child;

            Assert.True(deserializer.TryPopulate("{\"Child\":{\"A\":9}}", root));

            Assert.NotSame(originalChild, root.Child);
            Assert.Equal(9, root.Child.A);
            Assert.Equal(0, root.Child.B);
        }

        [Fact]
        public void Settings_MemberSetting_SetPopulateAsMember_False_ReplacesOnlyThatMember()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            settings.ConfigureType<PopulateDualRoot>(ts =>
            {
                ts.ConfigureMember<PopulateChild>(nameof(PopulateDualRoot.Child1), ms => ms.SetPopulateAsMember(false));
            });

            var deserializer = new JsonDeserializer(settings);

            var root = new PopulateDualRoot
            {
                Child1 = new PopulateChild { A = 1, B = 10 },
                Child2 = new PopulateChild { A = 2, B = 20 }
            };

            var child1Before = root.Child1;
            var child2Before = root.Child2;

            Assert.True(deserializer.TryPopulate("{\"Child1\":{\"A\":11},\"Child2\":{\"A\":22}}", root));

            Assert.NotSame(child1Before, root.Child1);
            Assert.Equal(11, root.Child1.A);
            Assert.Equal(0, root.Child1.B);

            Assert.Same(child2Before, root.Child2);
            Assert.Equal(22, root.Child2.A);
            Assert.Equal(20, root.Child2.B);
        }

        [Fact]
        public void Settings_TryDeserialize_UsesGlobalPopulateExistingMembers_ForSubMembers()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Child\":{\"A\":7}}", out PopulateRoot value));

            Assert.NotNull(value);
            Assert.NotNull(value.Child);
            Assert.Equal(7, value.Child.A);
            Assert.Equal(0, value.Child.B);
        }

        private class PopulateRoot
        {
            public PopulateChild Child = new PopulateChild { A = 100, B = 200 };
        }

        private class PopulateDualRoot
        {
            public PopulateChild Child1 = new PopulateChild { A = 100, B = 200 };
            public PopulateChild Child2 = new PopulateChild { A = 300, B = 400 };
        }

        private class PopulateChild
        {
            public int A;
            public int B;
        }

        [Fact]
        public void Settings_AddCustomTypeReader_WithPopulateDelegate_TryPopulate_UsesExistingInstance()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomPopulateItem>(ts => ts.SetCustomTypeReader(
                (api, item) =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    item.Value = (int)n;
                    item.PopulateCalls++;
                    return item;
                }));

            var deserializer = new JsonDeserializer(settings);

            var root = new CustomPopulateRoot
            {
                Item = new CustomPopulateItem { Value = 1, PopulateCalls = 0 }
            };
            var original = root.Item;

            Assert.True(deserializer.TryPopulate("{\"Item\":5}", root));

            Assert.Same(original, root.Item);
            Assert.Equal(5, root.Item.Value);
            Assert.Equal(1, root.Item.PopulateCalls);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_WithoutPopulateDelegate_TryPopulate_ReplacesInstance()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomPopulateItem>(ts => ts.SetCustomTypeReader(
                api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    return new CustomPopulateItem { Value = (int)n, PopulateCalls = 100 };
                }));

            var deserializer = new JsonDeserializer(settings);

            var root = new CustomPopulateRoot
            {
                Item = new CustomPopulateItem { Value = 1, PopulateCalls = 0 }
            };
            var original = root.Item;

            Assert.True(deserializer.TryPopulate("{\"Item\":5}", root));

            Assert.NotSame(original, root.Item);
            Assert.Equal(5, root.Item.Value);
            Assert.Equal(100, root.Item.PopulateCalls);
        }

        [Fact]
        public void Settings_AddCustomTypeReader_WithPopulateDelegate()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<CustomPopulateItem>(ts => ts.SetCustomTypeReader(
                (api, item) =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long n));
                    item.Value = (int)n;
                    item.PopulateCalls++;
                    return item;
                }));

            var deserializer = new JsonDeserializer(settings);

            var root = new CustomPopulateRoot
            {
                Item = new CustomPopulateItem { Value = 1, PopulateCalls = 0 }
            };
            var original = root.Item;

            Assert.True(deserializer.TryPopulate("{\"Item\":5}", root));

            Assert.Same(original, root.Item);
            Assert.Equal(5, root.Item.Value);
        }

        private class CustomPopulateRoot
        {
            public CustomPopulateItem Item;
        }

        private class CustomPopulateItem
        {
            public int Value;
            public int PopulateCalls;
        }

        [Fact]
        public void Settings_MemberSetUseStringCache_False_DisablesCache_ForThatMember_WhenGlobalTrue()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = true
            };
            settings.ConfigureType<StringCacheMemberSample>(ts =>
            {
                ts.ConfigureMember<string>(nameof(StringCacheMemberSample.NonCached), ms => ms.SetUseStringCache(false));
            });

            var deserializer = new JsonDeserializer(settings);
            const string json = "{\"CachedByGlobal\":\"value-1234567890-abcdef\",\"NonCached\":\"value-1234567890-abcdef\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.Same(first.CachedByGlobal, second.CachedByGlobal);
            Assert.NotSame(first.NonCached, second.NonCached);
        }

        [Fact]
        public void Settings_MemberSetUseStringCache_True_EnablesCache_ForThatMember_WhenGlobalFalse()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = false
            };
            settings.ConfigureType<StringCacheMemberSample>(ts =>
            {
                ts.ConfigureMember<string>(nameof(StringCacheMemberSample.CachedByMember), ms => ms.SetUseStringCache(true));
            });

            var deserializer = new JsonDeserializer(settings);
            const string json = "{\"CachedByMember\":\"value-1234567890-abcdef\",\"NonCached\":\"value-1234567890-abcdef\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.Same(first.CachedByMember, second.CachedByMember);
            Assert.NotSame(first.NonCached, second.NonCached);
        }

        [Fact]
        public void Settings_MemberSetUseStringCache_True_AlsoWorksForNestedMemberSetting()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = false
            };
            settings.ConfigureType<StringCacheNestedRoot>(ts =>
            {
                ts.ConfigureMember<StringCacheNestedChild>(nameof(StringCacheNestedRoot.Child), ms =>
                {
                    ms.ConfigureMember<string>(nameof(StringCacheNestedChild.Text), childMember => childMember.SetUseStringCache(true));
                });
            });

            var deserializer = new JsonDeserializer(settings);
            const string json = "{\"Child\":{\"Text\":\"value-1234567890-abcdef\"}}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheNestedRoot first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheNestedRoot second));

            Assert.Same(first.Child.Text, second.Child.Text);
        }

        private class StringCacheMemberSample
        {
            public string CachedByGlobal;
            public string CachedByMember;
            public string NonCached;
        }

        private class StringCacheNestedRoot
        {
            public StringCacheNestedChild Child;
        }

        private class StringCacheNestedChild
        {
            public string Text;
        }

        [Fact]
        public void Settings_StringCacheMaxLength_ShortString_IsCached()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = true,
                stringCacheMaxLength = 8
            };
            var deserializer = new JsonDeserializer(settings);

            const string json = "{\"CachedByGlobal\":\"short\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.Same(first.CachedByGlobal, second.CachedByGlobal);
        }

        [Fact]
        public void Settings_StringCacheMaxLength_LongString_IsNotCached()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = true,
                stringCacheMaxLength = 8
            };
            var deserializer = new JsonDeserializer(settings);

            const string json = "{\"CachedByGlobal\":\"this-is-definitely-longer-than-eight\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.NotSame(first.CachedByGlobal, second.CachedByGlobal);
        }

        [Fact]
        public void Settings_MemberSetUseStringCache_True_DoesNotBypassMaxLength()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = false,
                stringCacheMaxLength = 8
            };
            settings.ConfigureType<StringCacheMemberSample>(ts =>
            {
                ts.ConfigureMember<string>(nameof(StringCacheMemberSample.CachedByMember), ms => ms.SetUseStringCache(true));
            });

            var deserializer = new JsonDeserializer(settings);

            const string json = "{\"CachedByMember\":\"this-is-definitely-longer-than-eight\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.NotSame(first.CachedByMember, second.CachedByMember);
        }

        [Fact]
        public void Settings_Build_WithNullAction_ReturnsDefaults()
        {
            var settings = JsonDeserializer.Settings.Build(null);
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, list);
        }

        [Fact]
        public void Settings_Build_ActionCanOverrideSimpleFlags()
        {
            var settings = JsonDeserializer.Settings.Build(s =>
            {
                s.strict = true;
                s.rethrowExceptions = false;
                s.logCatchedExceptions = false;
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("\"123\"", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Settings_Build_ActionCanConfigureTypeMapping()
        {
            var settings = JsonDeserializer.Settings.Build(s =>
            {
                s.ConfigureType<MappedBase>(ts => ts.SetInstanceTypeMapping<MappedDerived>());
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":12}", out MappedBase value));
            var mapped = Assert.IsType<MappedDerived>(value);
            Assert.Equal(12, mapped.X);
        }

        [Fact]
        public void Settings_Build_ActionCanConfigureMemberSettings()
        {
            var settings = JsonDeserializer.Settings.Build(s =>
            {
                s.ConfigureType<MemberConfigPayload>(ts =>
                {
                    ts.ConfigureMember<int>(nameof(MemberConfigPayload.Value), ms => ms.OverrideName("renamed"));
                });
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"renamed\":33}", out MemberConfigPayload value));
            Assert.NotNull(value);
            Assert.Equal(33, value.Value);
        }

        [Fact]
        public void Settings_ConfigureGenericType_Null_RemovesGenericTypeSettings()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureGenericType(typeof(IEnumerable<>), ts => ts.SetInstanceTypeMapping(typeof(Queue<>)));
            settings.ConfigureGenericType(typeof(IEnumerable<>), null);

            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            Assert.Null(value);
        }

        [Fact]
        public void Settings_ConfigureGenericType_Reconfigure_ReusesAndAppliesNewSettings()
        {
            var settings = new JsonDeserializer.Settings();

            settings.ConfigureGenericType(typeof(IEnumerable<>), ts => ts.SetInstanceTypeMapping(typeof(Queue<>)));
            settings.ConfigureGenericType(typeof(IEnumerable<>), ts => ts.SetInstanceTypeMapping(typeof(List<>)));

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2,3]", out IEnumerable<int> value));
            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, list);
        }

        [Fact]
        public void Settings_ConfigureType_Reconfigure_ReusesAndAppliesNewSettings()
        {
            var settings = new JsonDeserializer.Settings();

            settings.ConfigureType<MappedBase>(ts => ts.SetInstanceTypeMapping<MappedDerived>());
            settings.ConfigureType<MappedBase>(ts => ts.SetInstanceTypeMapping<MappedBase2>());

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"X\":5}", out MappedBase value));
            var mapped = Assert.IsType<MappedBase2>(value);
            Assert.Equal(5, mapped.X);
        }

        private class MappedBase2 : MappedBase
        {
        }

        [Fact]
        public void Settings_ConfigureMember_TypeSettings_MissingMember_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureType<MemberConfigPayload>(ts =>
                    ts.ConfigureMember<int>("DoesNotExist", _ => { })));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Settings_ConfigureMember_TypeSettings_WrongPropertyType_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureType<MemberWithProperty>(ts =>
                    ts.ConfigureMember<string>(nameof(MemberWithProperty.Number), _ => { })));

            Assert.Contains("is of type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Settings_ConfigureMember_TypeSettings_WrongFieldType_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureType<MemberConfigPayload>(ts =>
                    ts.ConfigureMember<string>(nameof(MemberConfigPayload.Value), _ => { })));

            Assert.Contains("is of type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Settings_ConfigureMember_GenericTypeSettings_MissingMember_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureGenericType(typeof(List<>), ts =>
                    ts.ConfigureMember<int>("DoesNotExist", _ => { })));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Settings_ConfigureMember_GenericTypeSettings_WrongMemberType_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureGenericType(typeof(List<>), ts =>
                    ts.ConfigureMember<string>("Capacity", _ => { })));

            Assert.Contains("is of type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private class MemberWithProperty
        {
            public int Number { get; set; }
        }

        [Fact]
        public void Settings_CastObjectArrayToCommonTypeArray_True_CastsToCommonArrayType()
        {
            var settings = new JsonDeserializer.Settings
            {
                castObjectArrayToCommonTypeArray = true
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2]", out object value));
            var array = Assert.IsType<int[]>(value);
            Assert.Equal(new[] { 1, 2 }, array);
        }

        [Fact]
        public void Settings_CastObjectArrayToCommonTypeArray_False_StaysObjectArray()
        {
            var settings = new JsonDeserializer.Settings
            {
                castObjectArrayToCommonTypeArray = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,2]", out object value));
            var array = Assert.IsType<object[]>(value);
            Assert.Equal(new object[] { 1, 2 }, array);
        }

        [Fact]
        public void Settings_StringCacheBitSize_BelowMinimum_StillCaches()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = true,
                stringCacheBitSize = 1,
                stringCacheMaxLength = 128
            };
            var deserializer = new JsonDeserializer(settings);
            const string json = "{\"CachedByGlobal\":\"cache-key-small\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.Same(first.CachedByGlobal, second.CachedByGlobal);
        }

        [Fact]
        public void Settings_StringCacheBitSize_AboveMaximum_StillCaches()
        {
            var settings = new JsonDeserializer.Settings
            {
                useStringCache = true,
                stringCacheBitSize = 99,
                stringCacheMaxLength = 128
            };
            var deserializer = new JsonDeserializer(settings);
            const string json = "{\"CachedByGlobal\":\"cache-key-large\"}";

            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample first));
            Assert.True(deserializer.TryDeserialize(json, out StringCacheMemberSample second));

            Assert.Same(first.CachedByGlobal, second.CachedByGlobal);
        }

        [Fact]
        public void Settings_ReferenceResolution_ForceDisabled_IgnoresTypeLevelEnable()
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            settings.ConfigureType<RefNode>(ts => ts.SetReferenceResolution(true));

            var deserializer = new JsonDeserializer(settings);
            string json = "{\"A\":{\"Name\":\"alpha\"},\"B\":{\"$ref\":\"$.A\"}}";

            Assert.True(deserializer.TryDeserialize(json, out RefContainer value));
            Assert.NotNull(value);
            Assert.NotNull(value.A);
            Assert.NotNull(value.B);
            Assert.NotSame(value.A, value.B);
            Assert.Null(value.B.Name);
        }

        [Fact]
        public void Settings_SetDataAccess_TypeSetting_OverridesGlobalSetting()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            };
            settings.ConfigureType<PrivateFieldDataAccessSample>(ts =>
            {
                ts.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields);
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"hidden\":7}", out PrivateFieldDataAccessSample value));
            Assert.Equal(7, value.Hidden);
        }

        [Fact]
        public void Settings_SetDataAccess_GenericTypeSetting_OverridesGlobalSetting()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            };
            settings.ConfigureGenericType(typeof(GenericPrivateFieldBox<>), ts =>
            {
                ts.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields);
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"item\":11}", out GenericPrivateFieldBox<int> value));
            Assert.Equal(11, value.Item);
        }

        [Fact]
        public void Settings_SetProposedTypeHandling_TypeSetting_False_IgnoresProposedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            settings.ConfigureType<IProposedBase>(ts =>
            {
                ts.SetProposedTypeHandling(false);
                ts.SetInstanceTypeMapping<ProposedFallback>(mapped => mapped.SetProposedTypeHandling(false));
            });

            string proposedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedDerived));
            string json = $"{{\"$type\":\"{proposedTypeName}\",\"A\":5,\"B\":9}}";

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize(json, out IProposedBase value));
            var typed = Assert.IsType<ProposedFallback>(value);
            Assert.Equal(5, typed.A);
        }

        [Fact]
        public void Settings_SetProposedTypeHandling_GenericTypeSetting_False_IgnoresProposedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            settings.ConfigureGenericType(typeof(GenericProposedBase<>), ts =>
            {
                ts.SetProposedTypeHandling(false);
                ts.SetInstanceTypeMapping(typeof(GenericProposedFallback<>), mapped => mapped.SetProposedTypeHandling(false));
            });

            string proposedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(GenericProposedDerived<int>));
            string json = $"{{\"$type\":\"{proposedTypeName}\",\"A\":3,\"B\":8}}";

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize(json, out GenericProposedBase<int> value));
            var typed = Assert.IsType<GenericProposedFallback<int>>(value);
            Assert.Equal(3, typed.A);
        }

        [Fact]
        public void Settings_AddUntypedCollectionConstructor_UsesConfiguredConstructor()
        {
            var settings = new JsonDeserializer.Settings();
            settings.ConfigureType<UntypedCollectionWrapper>(ts =>
            {
                ts.AddUntypedCollectionConstructor(values => new UntypedCollectionWrapper(values));
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("[1,\"x\",null]", out UntypedCollectionWrapper value));
            Assert.Equal(3, value.Items.Count);
            Assert.Equal(1, value.Items[0]);
            Assert.Equal("x", value.Items[1]);
            Assert.Null(value.Items[2]);
        }

        [Fact]
        public void Settings_AddUntypedCollectionConstructor_NonEnumerableType_Throws()
        {
            var settings = new JsonDeserializer.Settings();

            var ex = Assert.Throws<Exception>(() =>
                settings.ConfigureType<NotEnumerableCtorTarget>(ts =>
                    ts.AddUntypedCollectionConstructor(_ => new NotEnumerableCtorTarget())));

            Assert.Contains("not valid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Settings_AddDefaultCustomTypeNames_AfterClear_AddsClrAliases()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            settings.ClearCustomTypeNames();
            settings.AddDefaultCustomTypeNames();

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"$type\":\"Int32\",\"$value\":7}", out object value));
            Assert.IsType<int>(value);
            Assert.Equal(7, (int)value);
        }

        [Fact]
        public void Settings_AddCSharpKeywordTypeNames_AfterClear_AddsKeywordAliases()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            settings.ClearCustomTypeNames();
            settings.AddCSharpKeywordTypeNames();

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"$type\":\"int\",\"$value\":9}", out object value));
            Assert.IsType<int>(value);
            Assert.Equal(9, (int)value);
        }

        [Fact]
        public void Settings_AddCommonCrossLanguageTypeNames_AfterClear_AddsCrossLanguageAliases()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            settings.ClearCustomTypeNames();
            settings.AddCommonCrossLanguageTypeNames();

            Guid guid = Guid.NewGuid();
            string json = $"{{\"$type\":\"uuid\",\"$value\":\"{guid:D}\"}}";
            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize(json, out object value));
            Assert.IsType<Guid>(value);
            Assert.Equal(guid, (Guid)value);
        }

        private class StringRefContainer
        {
            public string A;
            public string B;
        }

        private class PrivateFieldDataAccessSample
        {
            private int hidden;
            public int Hidden => hidden;
        }

        private class GenericPrivateFieldBox<T>
        {
            private T item;
            public T Item => item;
        }

        private interface IProposedBase
        {
            int A { get; }
        }

        private class ProposedFallback : IProposedBase
        {
            public int A;
            int IProposedBase.A => A;
        }

        private class ProposedDerived : ProposedFallback
        {
            public int B;
        }

        private abstract class GenericProposedBase<T>
        {
            public T A;
        }

        private class GenericProposedFallback<T> : GenericProposedBase<T>
        {
        }

        private class GenericProposedDerived<T> : GenericProposedBase<T>
        {
            public T B;
        }

        private class UntypedCollectionWrapper : IEnumerable
        {
            public List<object> Items { get; }

            public UntypedCollectionWrapper(IEnumerable values)
            {
                Items = values.Cast<object>().ToList();
            }

            public IEnumerator GetEnumerator() => Items.GetEnumerator();
        }

        private class NotEnumerableCtorTarget
        {
        }

        [Fact]
        public void Settings_ProposedTypeMode_DefaultCheckWhereReasonable_UsesProposedTypeForInterfaceTarget()
        {
            var settings = new JsonDeserializer.Settings(); // default: CheckWhereReasonable
            var deserializer = new JsonDeserializer(settings);

            string proposedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedDerived));
            string json = $"{{\"$type\":\"{proposedTypeName}\",\"A\":6,\"B\":12}}";

            Assert.True(deserializer.TryDeserialize(json, out IProposedBase value));
            var typed = Assert.IsType<ProposedDerived>(value);
            Assert.Equal(6, typed.A);
            Assert.Equal(12, typed.B);
        }

        [Fact]
        public void Settings_SetBackingFieldMode_GenericTypeSetting_OverridesGlobalSetting()
        {
            var settings = new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields,
                backingFieldMode = JsonDeserializer.Settings.BackingFieldMode.TryPropertyNameOnly
            };
            settings.ConfigureGenericType(typeof(GenericBackingFieldSample<>), ts =>
            {
                ts.SetBackingFieldMode(JsonDeserializer.Settings.BackingFieldMode.TryBackingFieldNameOnly);
            });

            var deserializer = new JsonDeserializer(settings);

            Assert.True(deserializer.TryDeserialize("{\"Auto\":7}", out GenericBackingFieldSample<int> fromPropertyName));
            Assert.Equal(0, fromPropertyName.Auto);

            Assert.True(deserializer.TryDeserialize("{\"<Auto>k__BackingField\":8}", out GenericBackingFieldSample<int> fromBackingFieldName));
            Assert.Equal(8, fromBackingFieldName.Auto);
        }

        private class GenericBackingFieldSample<T>
        {
            public T Auto { get; set; }
        }
    }
}