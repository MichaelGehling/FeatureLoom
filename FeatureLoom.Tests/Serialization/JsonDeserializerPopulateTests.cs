using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;
using System.Text.Json.Serialization;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerPopulateTests
    {
        private static void Populate<T>(string json, T item) where T : class
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);
            Assert.True(deserializer.TryPopulate(json, item));
        }

        private static void Populate<T>(string json, ref T item) where T : struct
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);
            Assert.True(deserializer.TryPopulate(json, ref item));
        }

        [Fact]
        public void Populate_Class_PreservesUnspecifiedMembers()
        {
            var item = new PopulateSample
            {
                A = 1,
                B = 2,
                Inner = new PopulateInner { X = 3, Y = 4 }
            };

            Populate("{\"A\":10,\"Inner\":{\"X\":30}}", item);

            Assert.Equal(10, item.A);
            Assert.Equal(2, item.B);
            Assert.Equal(30, item.Inner.X);
            Assert.Equal(4, item.Inner.Y);
        }

        [Fact]
        public void Populate_Class_NullNested_CreatesInstance()
        {
            var item = new PopulateSample
            {
                A = 1,
                B = 2,
                Inner = null
            };

            Populate("{\"Inner\":{\"X\":7,\"Y\":8}}", item);

            Assert.NotNull(item.Inner);
            Assert.Equal(7, item.Inner.X);
            Assert.Equal(8, item.Inner.Y);
        }

        [Fact]
        public void Populate_List_ReplacesContents()
        {
            var item = new PopulateListSample
            {
                Items = new List<int> { 1, 2, 3 }
            };

            Populate("{\"Items\":[4,5]}", item);

            Assert.Equal(new[] { 4, 5 }, item.Items);
        }

        [Fact]
        public void Populate_Dictionary_ReplacesContents()
        {
            var item = new PopulateDictionarySample
            {
                Map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }
            };

            Populate("{\"Map\":{\"b\":3,\"c\":4}}", item);

            Assert.Equal(2, item.Map.Count);
            Assert.Equal(3, item.Map["b"]);
            Assert.Equal(4, item.Map["c"]);
            Assert.False(item.Map.ContainsKey("a"));
        }

        [Fact]
        public void Populate_Struct_PreservesUnspecifiedMembers()
        {
            var item = new PopulateStruct
            {
                A = 1,
                B = 2,
                Inner = new PopulateInnerStruct { X = 3, Y = 4 }
            };

            Populate("{\"A\":10,\"Inner\":{\"X\":30}}", ref item);

            Assert.Equal(10, item.A);
            Assert.Equal(2, item.B);
            Assert.Equal(30, item.Inner.X);
            Assert.Equal(4, item.Inner.Y);
        }

        [Fact]
        public void Populate_JsonIgnore_PreservesExistingValue()
        {
            var item = new PopulateIgnoredSample
            {
                Visible = 1,
                Ignored = 5
            };

            Populate("{\"Visible\":2,\"Ignored\":9}", item);

            Assert.Equal(2, item.Visible);
            Assert.Equal(5, item.Ignored);
        }

        private class PopulateSample
        {
            public int A = 1;
            public int B = 2;
            public PopulateInner Inner = new PopulateInner();
        }

        private class PopulateInner
        {
            public int X = 3;
            public int Y = 4;
        }

        private class PopulateListSample
        {
            public List<int> Items = new();
        }

        private class PopulateDictionarySample
        {
            public Dictionary<string, int> Map = new();
        }

        private struct PopulateStruct
        {
            public int A;
            public int B;
            public PopulateInnerStruct Inner;
        }

        private struct PopulateInnerStruct
        {
            public int X;
            public int Y;
        }

        private class PopulateIgnoredSample
        {
            public int Visible = 1;

            [JsonIgnore]
            public int Ignored = 5;
        }
    }
}