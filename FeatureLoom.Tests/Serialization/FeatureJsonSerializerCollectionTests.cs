using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonSerializerCollectionTests
    {
        private static void AssertSerialized<T>(T value, string expected)
        {
            var serializer = new FeatureJsonSerializer();
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        private static void AssertSerialized<T>(T value, string expected, FeatureJsonSerializer.Settings settings)
        {
            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialize_IntArray()
        {
            var value = new[] { 1, -2, 3 };
            const string expected = "[1,-2,3]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_StringArray_WithNull()
        {
            var value = new[] { "a", null, "b" };
            const string expected = "[\"a\",null,\"b\"]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_ListOfInt()
        {
            var value = new List<int> { 1, 2, 3 };
            const string expected = "[1,2,3]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_ListOfNullableInt()
        {
            var value = new List<int?> { 1, null, 2 };
            const string expected = "[1,null,2]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_IReadOnlyList()
        {
            IReadOnlyList<string> value = new List<string> { "a", "b" };
            const string expected = "[\"a\",\"b\"]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        [Fact]
        public void Serialize_IEnumerableOfInt()
        {
            IEnumerable<int> value = new List<int> { 1, 2 };
            const string expected = "[1,2]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        [Fact]
        public void Serialize_Dictionary_String_Int()
        {
            var value = new SortedDictionary<string, int>
            {
                ["a"] = 1,
                ["b"] = 2
            };
            const string expected = "{\"a\":1,\"b\":2}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Dictionary_String_String_WithNull()
        {
            var value = new SortedDictionary<string, string>
            {
                ["a"] = "x",
                ["b"] = null
            };
            const string expected = "{\"a\":\"x\",\"b\":null}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Dictionary_Int_String()
        {
            var value = new SortedDictionary<int, string>
            {
                [2] = "b",
                [1] = "a"
            };
            const string expected = "{\"1\":\"a\",\"2\":\"b\"}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_ReadOnlyDictionary_String_Int()
        {
            var source = new SortedDictionary<string, int>
            {
                ["a"] = 1,
                ["b"] = 2
            };
            var value = new ReadOnlyDictionary<string, int>(source);
            const string expected = "{\"a\":1,\"b\":2}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_ByteArray_AsBase64()
        {
            var value = new byte[] { 1, 2, 3, 4 };
            string expected = $"\"{Convert.ToBase64String(value)}\"";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = true });
        }

        [Fact]
        public void Serialize_ByteArray_AsArray()
        {
            var value = new byte[] { 1, 2, 3, 4 };
            const string expected = "[1,2,3,4]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = false });
        }

        [Fact]
        public void Serialize_ByteSegment_AsBase64()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            var value = new ByteSegment(bytes);
            string expected = $"\"{Convert.ToBase64String(bytes)}\"";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = true });
        }

        [Fact]
        public void Serialize_ByteSegment_AsArray()
        {
            var value = new ByteSegment(new byte[] { 1, 2, 3, 4 });
            const string expected = "[1,2,3,4]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = false });
        }

        [Fact]
        public void Serialize_ArraySegmentOfByte_AsBase64()
        {
            var source = new byte[] { 1, 2, 3, 4 };
            var value = new ArraySegment<byte>(source, 1, 2);
            string expected = $"\"{Convert.ToBase64String(new byte[] { 2, 3 })}\"";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = true });
        }

        [Fact]
        public void Serialize_ArraySegmentOfByte_AsArray()
        {
            var source = new byte[] { 1, 2, 3, 4 };
            var value = new ArraySegment<byte>(source, 1, 2);
            const string expected = "[2,3]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { writeByteArrayAsBase64String = false });
        }

        [Fact]
        public void Serialize_ArraySegmentOfInt()
        {
            var source = new[] { 1, 2, 3, 4 };
            var value = new ArraySegment<int>(source, 1, 2);
            const string expected = "[2,3]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_QueueOfInt()
        {
            var value = new Queue<int>(new[] { 1, 2, 3 });
            const string expected = "[1,2,3]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_StackOfInt()
        {
            var value = new Stack<int>(new[] { 1, 2, 3 });
            const string expected = "[3,2,1]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_SortedSetOfInt()
        {
            var value = new SortedSet<int> { 3, 1, 2 };
            const string expected = "[1,2,3]";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_ISet_Interface()
        {
            ISet<int> value = new SortedSet<int> { 3, 1, 2 };
            const string expected = "[1,2,3]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        [Fact]
        public void Serialize_ICollection_Interface()
        {
            ICollection<int> value = new List<int> { 1, 2 };
            const string expected = "[1,2]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        [Fact]
        public void Serialize_IEnumerable_NonGeneric_ArrayList()
        {
            IEnumerable value = new ArrayList { 1, "a", null };
            const string expected = "[1,\"a\",null]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        [Fact]
        public void Serialize_TreatEnumerablesAsCollections_False_SerializesAsObject()
        {
            var value = new NonCollectionEnumerable();
            const string expected = "{\"Data\":[1,2]}";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { treatEnumerablesAsCollections = false });
        }

        [Fact]
        public void Serialize_ConcurrentQueue()
        {
            var value = new ConcurrentQueue<int>(new[] { 1, 2, 3 });
            const string expected = "[1,2,3]" ;

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_IProducerConsumerCollection_Interface()
        {
            IProducerConsumerCollection<int> value = new ConcurrentQueue<int>(new[] { 1, 2 });
            const string expected = "[1,2]";

            AssertSerialized(value, expected, new FeatureJsonSerializer.Settings { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo });
        }

        private class NonCollectionEnumerable : IEnumerable<int>
        {
            public int[] Data = { 1, 2 };

            public IEnumerator<int> GetEnumerator()
            {
                foreach (int value in Data) yield return value;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}