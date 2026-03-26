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

        [Fact]
        public void Deserialize_ReferenceResolution_SharedObjectAcrossStructs_AndSecondStructByRef()
        {
            const string json =
                "{\"First\":{\"Shared\":{\"Name\":\"shared\"},\"Other\":{\"Name\":\"first\"}}," +
                "\"Second\":{\"Shared\":{\"$ref\":\"$.First.Shared\"},\"Other\":{\"Name\":\"second\"}}";

            var value = Deserialize<StructReferenceContainer>(json);

            // Same object referenced in two different structs
            Assert.Same(value.First.Shared, value.Second.Shared);        }

        [Fact]
        public void Deserialize_ReferenceResolution_RefObject_WithAdditionalFields_StillResolves()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[0]\",\"Meta\":{\"x\":1}}]}";
            var value = Deserialize<NodeList>(json);

            Assert.Same(value.Items[0], value.Items[1]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_DeepPath_ArrayAndObjectSegments_Resolves()
        {
            const string json =
                "{\"Groups\":[{\"Items\":[{\"Name\":\"shared\"}]},{\"Items\":[{\"$ref\":\"$.Groups[0].Items[0]\"}]}]}";

            var value = Deserialize<NodeGroups>(json);

            Assert.Same(value.Groups[0].Items[0], value.Groups[1].Items[0]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ValidPathButIncompatibleType_ReturnsFalse()
        {
            const string json = "{\"Node\":{\"Name\":\"x\"},\"Container\":{\"$ref\":\"$.Node\"}}";

            Assert.False(TryDeserialize(json, out MixedTypeContainer value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_EmptyRefPath_ReturnsFalse()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"\"}}";
            Assert.False(TryDeserialize(json, out Node value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_RefPathWithoutRootPrefix_ReturnsFalse()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"Items[0]\"}]}";
            Assert.False(TryDeserialize(json, out NodeList value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_RefObject_NotFirstField_IsTreatedAsNormalObject()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"Name\":\"b\",\"$ref\":\"$.Items[0]\"}]}";
            var value = Deserialize<NodeList>(json);

            Assert.NotSame(value.Items[0], value.Items[1]);
            Assert.Equal("a", value.Items[0].Name);
            Assert.Equal("b", value.Items[1].Name);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_RefPathTrailingDot_ReturnsFalse()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[0].\"}]}";
            Assert.False(TryDeserialize(json, out NodeList value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_RefPathUnclosedIndexer_ReturnsFalse()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[0\"}]}";
            Assert.False(TryDeserialize(json, out NodeList value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_RootDotOnlyPath_ReturnsFalse()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$.\"}}";
            Assert.False(TryDeserialize(json, out Node value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ForwardReference_ReturnsFalse()
        {
            const string json = "{\"Child\":{\"$ref\":\"$.Other\"},\"Other\":{\"Name\":\"x\"}}";
            Assert.False(TryDeserialize(json, out NodeContainer value));
            Assert.Null(value);
        }

        private class StructReferenceContainer
        {
            public NodePair First;
            public NodePair Second;
        }

        private struct NodePair
        {
            public Node Shared;
            public Node Other;
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

        private class NodeGroups
        {
            public List<Group> Groups = new();
        }

        private class Group
        {
            public List<Node> Items = new();
        }

        private class MixedTypeContainer
        {
            public Node Node;
            public NodeContainer Container;
        }
    }
}