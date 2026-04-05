using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerReferenceTests
    {
        private static void AssertSerialized<T>(T value, string expected, JsonSerializer.Settings settings)
        {
            var serializer = new JsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByNull()
        {
            var root = new Node("root");
            root.Next = root;

            const string expected = "{\"Name\":\"root\",\"Next\":null,\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByNull
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByRef()
        {
            var root = new Node("root");
            root.Next = root;

            const string expected = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$\"},\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopThrowException()
        {
            var root = new Node("root");
            root.Next = root;

            Assert.Throws<Exception>(() =>
            {
                var serializer = new JsonSerializer(new JsonSerializer.Settings
                {
                    referenceCheck = JsonSerializer.ReferenceCheck.OnLoopThrowException
                });
                serializer.Serialize(root);
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef()
        {
            var shared = new Node("shared");
            var root = new Node("root")
            {
                Next = shared,
                Other = shared
            };

            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":{\"$ref\":\"$.Next\"}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByRef_InCollection()
        {
            var root = new NodeList();
            root.Items.Add(root);

            const string expected = "{\"Items\":[{\"$ref\":\"$\"}]}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_SharedInCollection()
        {
            var shared = new Leaf { Value = 1 };
            var root = new LeafList
            {
                Items = new List<Leaf> { shared, shared }
            };

            const string expected = "{\"Items\":[{\"Value\":1},{\"$ref\":\"$.Items[0]\"}]}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByNull_Deep()
        {
            var root = new DeepNode(1);
            root.Next = new DeepNode(2);
            root.Next.Next = root;

            const string expected = "{\"Id\":1,\"Next\":{\"Id\":2,\"Next\":null}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByNull
            });
        }

        private class Node
        {
            public string Name;
            public Node Next;
            public Node Other;

            public Node(string name)
            {
                Name = name;
            }
        }

        private class NodeList
        {
            public List<NodeList> Items = new();
        }

        private class Leaf
        {
            public int Value;
        }

        private class LeafList
        {
            public List<Leaf> Items;
        }

        private class DeepNode
        {
            public int Id;
            public DeepNode Next;

            public DeepNode(int id)
            {
                Id = id;
            }
        }
    }
}