using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerReferenceTests
    {
        private static T Deserialize<T>(string json)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                enableReferenceResolution = true
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                enableReferenceResolution = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            return deserializer.TryDeserialize(json, out value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_SelfReference()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$\"}}";
            var value = Deserialize<Node>(json);

            Assert.Same(value, value.Next);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_SharedInCollection()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[0]\"}]}";
            var value = Deserialize<NodeList>(json);

            Assert.Same(value.Items[0], value.Items[1]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_NestedObject()
        {
            const string json = "{\"Child\":{\"Name\":\"x\"},\"Other\":{\"$ref\":\"$.Child\"}}";
            var value = Deserialize<NodeContainer>(json);

            Assert.Same(value.Child, value.Other);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_DictionaryEntry()
        {
            const string json = "{\"Map\":{\"a\":{\"Name\":\"x\"},\"b\":{\"$ref\":\"$.Map.a\"}}}";
            var value = Deserialize<NodeDictionary>(json);

            Assert.Same(value.Map["a"], value.Map["b"]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_InvalidRefPath_ReturnsFalse()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$.Missing\"}}";
            Assert.False(TryDeserialize(json, out Node value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_MalformedRefPath_ReturnsFalse()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[x]\"}]}";
            Assert.False(TryDeserialize(json, out NodeList value));
            Assert.Null(value);
        }

        private class Node
        {
            public string Name;
            public Node Next;
        }

        private class NodeList
        {
            public List<Node> Items = new();
        }

        private class NodeContainer
        {
            public Node Child;
            public Node Other;
        }

        private class NodeDictionary
        {
            public Dictionary<string, Node> Map = new();
        }
    }
}