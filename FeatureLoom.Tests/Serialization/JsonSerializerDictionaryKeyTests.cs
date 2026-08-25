using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    /// <summary>
    /// Covers the dictionary key writing path (CachedKeyWriter).
    /// Dictionary keys must always be written as JSON strings, independent of the key type.
    /// The tests are run both without reference checking (the key is written directly) and
    /// with reference checking enabled, because the latter additionally requires the written
    /// key to be copied out of the write buffer so it can be used as an item name.
    /// </summary>
    public class JsonSerializerDictionaryKeyTests
    {
        private static string Serialize<T>(T value, JsonSerializer.Settings settings = null)
        {
            var serializer = settings == null ? new JsonSerializer() : new JsonSerializer(settings);
            return serializer.Serialize(value);
        }

        /// <summary>
        /// Reference checking forces the "with copy" key writer path, because the key is needed
        /// as an item name for the $ref values.
        /// </summary>
        private static JsonSerializer.Settings RefSettings => new JsonSerializer.Settings
        {
            referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
        };

        public static IEnumerable<object[]> NoRefAndRefSettings()
        {
            yield return new object[] { null };
            yield return new object[] { RefSettings };
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_StringKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            Assert.Equal("{\"a\":1,\"b\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_StringKeys_RequiringEscaping(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int> { ["a\"b"] = 1, ["c\\d"] = 2, ["e\nf"] = 3 };
            Assert.Equal("{\"a\\\"b\":1,\"c\\\\d\":2,\"e\\nf\":3}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_StringKeys_NonAscii(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int> { ["äöü"] = 1, ["日本語"] = 2 };
            Assert.Equal("{\"äöü\":1,\"日本語\":2}", Serialize(dict, settings));
        }

        /// <summary>
        /// A long key exceeds the reserved buffer space, so the write buffer may be flushed
        /// while the key is being written. The copy path must still yield the correct output.
        /// </summary>
        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_VeryLongStringKey(JsonSerializer.Settings settings)
        {
            string longKey = new string('x', 5000);
            var dict = new Dictionary<string, int> { [longKey] = 1 };
            Assert.Equal("{\"" + longKey + "\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_EmptyStringKey(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int> { [""] = 1 };
            Assert.Equal("{\"\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_IntKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<int, string> { [1] = "a", [-2] = "b", [0] = "c" };
            Assert.Equal("{\"1\":\"a\",\"-2\":\"b\",\"0\":\"c\"}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_LongKeys_Extremes(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<long, int> { [long.MinValue] = 1, [long.MaxValue] = 2 };
            Assert.Equal("{\"" + long.MinValue + "\":1,\"" + long.MaxValue + "\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_ULongKeys_Extremes(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<ulong, int> { [ulong.MinValue] = 1, [ulong.MaxValue] = 2 };
            Assert.Equal("{\"0\":1,\"" + ulong.MaxValue + "\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_ByteAndSByteKeys(JsonSerializer.Settings settings)
        {
            var byteDict = new Dictionary<byte, int> { [0] = 1, [255] = 2 };
            Assert.Equal("{\"0\":1,\"255\":2}", Serialize(byteDict, settings));

            var sbyteDict = new Dictionary<sbyte, int> { [-128] = 1, [127] = 2 };
            Assert.Equal("{\"-128\":1,\"127\":2}", Serialize(sbyteDict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_ShortAndUShortKeys(JsonSerializer.Settings settings)
        {
            var shortDict = new Dictionary<short, int> { [short.MinValue] = 1, [short.MaxValue] = 2 };
            Assert.Equal("{\"" + short.MinValue + "\":1,\"" + short.MaxValue + "\":2}", Serialize(shortDict, settings));

            var ushortDict = new Dictionary<ushort, int> { [0] = 1, [ushort.MaxValue] = 2 };
            Assert.Equal("{\"0\":1,\"" + ushort.MaxValue + "\":2}", Serialize(ushortDict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_UIntKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<uint, int> { [0] = 1, [uint.MaxValue] = 2 };
            Assert.Equal("{\"0\":1,\"" + uint.MaxValue + "\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_BoolKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<bool, int> { [true] = 1, [false] = 2 };
            Assert.Equal("{\"true\":1,\"false\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_CharKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<char, int> { ['a'] = 1, ['"'] = 2 };
            Assert.Equal("{\"a\":1,\"\\\"\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_GuidKeys(JsonSerializer.Settings settings)
        {
            var guid = new Guid("12345678-1234-1234-1234-123456789abc");
            var dict = new Dictionary<Guid, int> { [guid] = 1 };
            Assert.Equal("{\"12345678-1234-1234-1234-123456789abc\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_EmptyDictionary(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int>();
            Assert.Equal("{}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_NullValues(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, string> { ["a"] = null, ["b"] = "x" };
            Assert.Equal("{\"a\":null,\"b\":\"x\"}", Serialize(dict, settings));
        }

        /// <summary>
        /// The declared type deviates from the actual type here, so type info is expected in the
        /// output. Only the key/value part is asserted.
        /// </summary>
        [Fact]
        public void Serialize_ReadOnlyDictionary()
        {
            IReadOnlyDictionary<string, int> dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            string json = Serialize(dict, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
            Assert.Equal("{\"a\":1,\"b\":2}", json);
        }

        /// <summary>
        /// Several keys in a row exercise the reuse of the temporary slice buffer used by the
        /// copy path. An earlier key must not be corrupted by a later one.
        /// </summary>
        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_ManyKeys_RemainStable(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<string, int>();
            var expected = new System.Text.StringBuilder("{");
            for (int i = 0; i < 200; i++)
            {
                dict["key" + i] = i;
                if (i > 0) expected.Append(',');
                expected.Append("\"key").Append(i).Append("\":").Append(i);
            }
            expected.Append('}');
            Assert.Equal(expected.ToString(), Serialize(dict, settings));
        }

        /// <summary>
        /// With reference checking, a repeated object value must be replaced by a $ref pointing
        /// at the dictionary key of its first occurrence. This is the actual purpose of copying
        /// the key out of the write buffer.
        /// </summary>
        [Fact]
        public void Serialize_RepeatedValue_UsesKeyAsReferenceName()
        {
            var shared = new Holder { Value = 42 };
            var dict = new Dictionary<string, Holder> { ["first"] = shared, ["second"] = shared };

            string json = Serialize(dict, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });

            Assert.Contains("\"first\"", json);
            Assert.Contains("$ref", json);
        }

        public class Holder
        {
            public int Value;
        }

        public enum Color { Red = 1, Green = 2 }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_EnumKeys_AsNumber(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<Color, int> { [Color.Red] = 1, [Color.Green] = 2 };
            Assert.Equal("{\"1\":1,\"2\":2}", Serialize(dict, settings));
        }

        [Fact]
        public void Serialize_EnumKeys_AsString()
        {
            var dict = new Dictionary<Color, int> { [Color.Red] = 1, [Color.Green] = 2 };
            string json = Serialize(dict, new JsonSerializer.Settings { enumAsString = true });
            Assert.Equal("{\"Red\":1,\"Green\":2}", json);
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_DateTimeKeys_UseInvariantRoundTripFormat(JsonSerializer.Settings settings)
        {
            var dt = new DateTime(2024, 3, 7, 14, 5, 6, DateTimeKind.Utc);
            var dict = new Dictionary<DateTime, int> { [dt] = 1 };
            Assert.Equal("{\"2024-03-07T14:05:06Z\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_DateTimeOffsetKeys(JsonSerializer.Settings settings)
        {
            var dto = new DateTimeOffset(2024, 3, 7, 14, 5, 6, TimeSpan.FromHours(2));
            var dict = new Dictionary<DateTimeOffset, int> { [dto] = 1 };
            Assert.Equal("{\"2024-03-07T14:05:06+02:00\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_TimeSpanKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<TimeSpan, int> { [new TimeSpan(1, 2, 3)] = 1 };
            Assert.Equal("{\"01:02:03\":1}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_DoubleKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<double, int> { [1.5] = 1, [-2.25] = 2 };
            Assert.Equal("{\"1.5\":1,\"-2.25\":2}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void Serialize_DecimalKeys(JsonSerializer.Settings settings)
        {
            var dict = new Dictionary<decimal, int> { [1.50m] = 1 };
            Assert.Equal("{\"1.50\":1}", Serialize(dict, settings));
        }

        /// <summary>
        /// Key formatting must not depend on the current culture, which would otherwise turn
        /// decimal points into commas and break round-tripping.
        /// </summary>
        [Fact]
        public void Serialize_Keys_AreCultureIndependent()
        {
            var original = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

                var doubleDict = new Dictionary<double, int> { [1.5] = 1 };
                Assert.Equal("{\"1.5\":1}", Serialize(doubleDict));

                var dt = new DateTime(2024, 3, 7, 14, 5, 6, DateTimeKind.Utc);
                var dateDict = new Dictionary<DateTime, int> { [dt] = 1 };
                Assert.Equal("{\"2024-03-07T14:05:06Z\":1}", Serialize(dateDict));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = original;
            }
        }

        public class ComplexKey
        {
            public int Id;
            public string Name;
        }

        /// <summary>
        /// Key types that cannot be written as a JSON property name fall back to the
        /// enumerable handler, which writes the dictionary as an array of key/value pairs.
        /// The deserializer accepts that shape, so such dictionaries still round-trip.
        /// </summary>
        [Fact]
        public void Serialize_ComplexKey_AsArrayOfKeyValuePairs()
        {
            var dict = new Dictionary<ComplexKey, int> { [new ComplexKey { Id = 1, Name = "a" }] = 42 };

            var json = Serialize(dict);

            Assert.Equal("[{\"key\":{\"Id\":1,\"Name\":\"a\"},\"value\":42}]", json);
        }

        [Fact]
        public void Serialize_ComplexKey_RoundTrips()
        {
            var key = new ComplexKey { Id = 1, Name = "a" };
            var dict = new Dictionary<ComplexKey, int> { [key] = 42 };

            var json = Serialize(dict);
            Assert.True(new JsonDeserializer().TryDeserialize(json, out Dictionary<ComplexKey, int> restored));

            Assert.Single(restored);
            foreach (var pair in restored)
            {
                Assert.Equal(1, pair.Key.Id);
                Assert.Equal("a", pair.Key.Name);
                Assert.Equal(42, pair.Value);
            }
        }

        /// <summary>
        /// IReadOnlyDictionary&lt;,&gt; does not inherit the non-generic IEnumerable, so it cannot
        /// rely on the enumerable handler and needs the explicit fallback.
        /// </summary>
        [Fact]
        public void Serialize_IReadOnlyDictionary_ComplexKey_RoundTrips()
        {
            IReadOnlyDictionary<ComplexKey, int> dict = new Dictionary<ComplexKey, int>
            {
                [new ComplexKey { Id = 1, Name = "a" }] = 42
            };

            var json = Serialize(dict);
            Assert.True(new JsonDeserializer().TryDeserialize(json, out IReadOnlyDictionary<ComplexKey, int> restored));

            Assert.Single(restored);
            foreach (var pair in restored)
            {
                Assert.Equal(1, pair.Key.Id);
                Assert.Equal("a", pair.Key.Name);
                Assert.Equal(42, pair.Value);
            }
        }

        private class RefKeyHolder
        {
            public Dictionary<ComplexKey, int> First;
            public Dictionary<ComplexKey, int> Second;
            public ComplexKey Loose;
        }

        /// <summary>
        /// With reference preservation enabled, a key object shared by several dictionaries is
        /// written out only once. Every further occurrence becomes a $ref pointing at the path of
        /// the first occurrence, which for the fallback representation is the "key" member of the
        /// corresponding array element.
        /// </summary>
        [Fact]
        public void Serialize_SharedComplexKey_IsWrittenOnceAndReferenced()
        {
            var shared = new ComplexKey { Id = 1, Name = "a" };
            var holder = new RefKeyHolder
            {
                First = new Dictionary<ComplexKey, int> { [shared] = 10 },
                Second = new Dictionary<ComplexKey, int> { [shared] = 20 },
                Loose = shared
            };

            var json = Serialize(holder, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });

            // written once...
            Assert.Contains("\"key\":{\"Id\":1,\"Name\":\"a\"}", json);
            // ...and referenced by its path afterwards.
            Assert.Contains("\"$ref\":\"$.First[0].key\"", json);
        }

        /// <summary>
        /// Duplicate (equal but distinct) keys must deserialize into separate instances, because a
        /// single instance cannot be the key of two entries.
        /// </summary>
        [Fact]
        public void Deserialize_DuplicateComplexKeys_YieldSeparateInstances()
        {
            const string json = "[{\"key\":{\"Id\":1,\"Name\":\"a\"},\"value\":10},{\"key\":{\"Id\":2,\"Name\":\"b\"},\"value\":20}]";

            Assert.True(new JsonDeserializer().TryDeserialize(json, out Dictionary<ComplexKey, int> restored));
            Assert.Equal(2, restored.Count);

            var keys = new List<ComplexKey>(restored.Keys);
            Assert.NotSame(keys[0], keys[1]);
        }

        /// <summary>
        /// A key formatter turns an otherwise unsupported key type into a property name, which
        /// makes the object shape available where the fallback would be used otherwise.
        /// </summary>
        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void ConfigureKey_WithStringFormatter_WritesDictionaryAsObject(JsonSerializer.Settings settings)
        {
            settings ??= new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<ComplexKey, int>>(ts =>
                ts.ConfigureKey<ComplexKey>(k => $"{k.Id}-{k.Name}"));

            var dict = new Dictionary<ComplexKey, int> { [new ComplexKey { Id = 1, Name = "a" }] = 10 };
            Assert.Equal("{\"1-a\":10}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void ConfigureKey_WithTextSegmentFormatter_WritesDictionaryAsObject(JsonSerializer.Settings settings)
        {
            settings ??= new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<ComplexKey, int>>(ts =>
                ts.ConfigureKey<ComplexKey>(k => (TextSegment)k.Name));

            var dict = new Dictionary<ComplexKey, int> { [new ComplexKey { Id = 2, Name = "b" }] = 20 };
            Assert.Equal("{\"b\":20}", Serialize(dict, settings));
        }

        [Theory]
        [MemberData(nameof(NoRefAndRefSettings))]
        public void ConfigureKey_WithSpanFormatter_WritesDictionaryAsObject(JsonSerializer.Settings settings)
        {
            settings ??= new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<ComplexKey, int>>(ts =>
                ts.ConfigureKey<ComplexKey>(new JsonSerializer.KeyToSpan<ComplexKey>(k => k.Name.AsSpan())));

            var dict = new Dictionary<ComplexKey, int> { [new ComplexKey { Id = 3, Name = "c" }] = 30 };
            Assert.Equal("{\"c\":30}", Serialize(dict, settings));
        }

        /// <summary>
        /// The pair-array shape can be selected explicitly even for keys that could become
        /// property names.
        /// </summary>
        [Fact]
        public void SetDictionaryShape_KeyValuePairArray_OverridesObjectShapeForStringKeys()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<string, int>>(ts =>
                ts.SetDictionaryShape(JsonSerializer.DictionaryShape.KeyValuePairArray));

            var dict = new Dictionary<string, int> { ["a"] = 1 };
            Assert.Equal("[{\"key\":\"a\",\"value\":1}]", Serialize(dict, settings));
        }

        [Fact]
        public void SetDictionaryShape_Auto_KeepsObjectShapeForStringKeys()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<string, int>>(ts =>
                ts.SetDictionaryShape(JsonSerializer.DictionaryShape.Auto));

            var dict = new Dictionary<string, int> { ["a"] = 1 };
            Assert.Equal("{\"a\":1}", Serialize(dict, settings));
        }

        /// <summary>
        /// An explicitly requested pair-array shape takes precedence over a configured key
        /// formatter, because the formatter only makes the object shape possible, not mandatory.
        /// </summary>
        [Fact]
        public void SetDictionaryShape_KeyValuePairArray_WinsOverConfiguredKeyFormatter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Dictionary<ComplexKey, int>>(ts =>
            {
                ts.ConfigureKey<ComplexKey>(k => k.Name);
                ts.SetDictionaryShape(JsonSerializer.DictionaryShape.KeyValuePairArray);
            });

            var dict = new Dictionary<ComplexKey, int> { [new ComplexKey { Id = 5, Name = "e" }] = 50 };
            var json = Serialize(dict, settings);

            Assert.StartsWith("[", json);
            Assert.Contains("\"key\"", json);
        }
    }
}
