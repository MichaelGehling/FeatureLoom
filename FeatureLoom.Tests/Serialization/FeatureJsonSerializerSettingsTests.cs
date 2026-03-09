using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonSerializerSettingsTests
    {
        private static void AssertSerialized<T>(T value, string expected, FeatureJsonSerializer.Settings settings)
        {
            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialize_Indentation_Object_DefaultFactor()
        {
            var value = new IndentSample();
            var settings = new FeatureJsonSerializer.Settings { indent = true, indentationFactor = 2 };
            const string expected = "{\n  \"A\":1,\n  \"B\":\"x\"\n}";

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_Indentation_Object_CustomFactor()
        {
            var value = new IndentSample();
            var settings = new FeatureJsonSerializer.Settings { indent = true, indentationFactor = 4 };
            const string expected = "{\n    \"A\":1,\n    \"B\":\"x\"\n}";

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_Indentation_Array()
        {
            var value = new[] { 1, 2 };
            var settings = new FeatureJsonSerializer.Settings { indent = true, indentationFactor = 2 };
            const string expected = "[\n  1,\n  2\n]";

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_Indentation_MaxDepth_LimitsIndentation()
        {
            var value = new IndentNested();
            var settings = new FeatureJsonSerializer.Settings { indent = true, indentationFactor = 2, maxIndentationDepth = 1 };
            const string expected = "{\n  \"A\":1,\n  \"Inner\":{\n  \"B\":2,\n  \"Inner\":{\n  \"C\":3\n  }\n  }\n}";

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_SmallWriteBufferChunkSize_LargeString()
        {
            string value = new string('a', 200);
            string expected = $"\"{value}\"";
            var settings = new FeatureJsonSerializer.Settings { writeBufferChunkSize = 32 };

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_SmallTempBufferSize_LongDictionaryKey()
        {
            string key = new string('k', 40);
            var value = new SortedDictionary<string, int> { [key] = 1 };
            string expected = $"{{\"{key}\":1}}";
            var settings = new FeatureJsonSerializer.Settings { tempBufferSize = 8 };

            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_Indentation_MaxDepth_LimitsIndentation_Array()
        {
            var value = new[] { new[] { 1, 2 }, new[] { 3 } };
            var settings = new FeatureJsonSerializer.Settings { indent = true, indentationFactor = 2, maxIndentationDepth = 1 };
            const string expected = "[\n  [\n  1,\n  2\n  ],\n  [\n  3\n  ]\n]";

            AssertSerialized(value, expected, settings);
        }

        private class IndentSample
        {
            public int A = 1;
            public string B = "x";
        }

        private class IndentNested
        {
            public int A = 1;
            public IndentNestedLevel1 Inner = new IndentNestedLevel1();
        }

        private class IndentNestedLevel1
        {
            public int B = 2;
            public IndentNestedLevel2 Inner = new IndentNestedLevel2();
        }

        private class IndentNestedLevel2
        {
            public int C = 3;
        }
    }
}