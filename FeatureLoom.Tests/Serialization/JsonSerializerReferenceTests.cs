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

        private class TwoLists
        {
            public List<Leaf> A;
            public List<Leaf> B;
        }

        private class TwoStrings
        {
            public string A;
            public string B;
        }

        [Fact]
        public void Serialize_ReferenceCheck_NoRefCheck_SharedReference_WrittenTwice()
        {
            var shared = new Leaf { Value = 9 };
            var root = new LeafList { Items = new List<Leaf> { shared, shared } };

            const string expected = "{\"Items\":[{\"Value\":9},{\"Value\":9}]}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.NoRefCheck
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByRef_NullField_NotTracked()
        {
            var root = new Node("root");
            root.Next = null;

            const string expected = "{\"Name\":\"root\",\"Next\":null,\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_ChainedShared()
        {
            var shared = new DeepNode(42);
            var root = new Node("root");
            var a = new Node("a") { Next = null, Other = null };
            var b = new Node("b") { Next = null, Other = null };
            root.Next = a;
            root.Other = a;

            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"a\",\"Next\":null,\"Other\":null},\"Other\":{\"$ref\":\"$.Next\"}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByRef_SharedNonLoop_WrittenTwice()
        {
            var shared = new Node("shared");
            var root = new Node("root")
            {
                Next = shared,
                Other = shared
            };

            // OnLoop modes only detect cycles; shared non-loop references are written in full both times
            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByNull_SharedNonLoop_WrittenTwice()
        {
            var shared = new Node("shared");
            var root = new Node("root")
            {
                Next = shared,
                Other = shared
            };

            // OnLoop modes only detect cycles; shared non-loop references are written in full both times
            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByNull
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_TripleSharedReference()
        {
            var shared = new Leaf { Value = 7 };
            var root = new LeafList
            {
                Items = new List<Leaf> { shared, shared, shared }
            };

            // 2nd and 3rd occurrence are both replaced by $ref to the first
            const string expected = "{\"Items\":[{\"Value\":7},{\"$ref\":\"$.Items[0]\"},{\"$ref\":\"$.Items[0]\"}]}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_DeeplyNestedRefPath()
        {
            var shared = new Node("shared");
            var mid = new Node("mid") { Next = shared, Other = null };
            var root = new Node("root") { Next = mid, Other = shared };

            // shared first appears at $.Next.Next, so Other should ref that deep path
            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"mid\",\"Next\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":null},\"Other\":{\"$ref\":\"$.Next.Next\"}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopThrowException_NoLoop_DoesNotThrow()
        {
            var root = new Node("root")
            {
                Next = new Node("child"),
                Other = null
            };

            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"child\",\"Next\":null,\"Other\":null},\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopThrowException
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_SelfLoop()
        {
            var root = new Node("root");
            root.Next = root;

            const string expected = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$\"},\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByNull_InCollection()
        {
            var root = new NodeList();
            root.Items.Add(root);

            const string expected = "{\"Items\":[null]}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByNull
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_OnLoopReplaceByRef_LoopBackToNonRoot()
        {
            var root = new Node("root");
            var mid = new Node("mid");
            var leaf = new Node("leaf");
            root.Next = mid;
            mid.Next = leaf;
            leaf.Next = mid; // loop back to mid, not root

            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"mid\",\"Next\":{\"Name\":\"leaf\",\"Next\":{\"$ref\":\"$.Next\"},\"Other\":null},\"Other\":null},\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_SharedCollection()
        {
            var sharedList = new List<Leaf> { new Leaf { Value = 5 } };
            var root = new TwoLists { A = sharedList, B = sharedList };

            const string expected = "{\"A\":[{\"Value\":5}],\"B\":{\"$ref\":\"$.A\"}}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_ChildReferencesRoot()
        {
            var root = new Node("root");
            var child = new Node("child");
            root.Next = child;
            child.Next = root; // grandchild points back to root

            const string expected = "{\"Name\":\"root\",\"Next\":{\"Name\":\"child\",\"Next\":{\"$ref\":\"$\"},\"Other\":null},\"Other\":null}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }

        [Fact]
        public void Serialize_ReferenceCheck_AlwaysReplaceByRef_SharedString_WrittenTwice()
        {
            var s = "hello";
            var root = new TwoStrings { A = s, B = s };

            // Strings use the primitive writer path; reference tracking does not apply
            const string expected = "{\"A\":\"hello\",\"B\":\"hello\"}";

            AssertSerialized(root, expected, new JsonSerializer.Settings
            {
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            });
        }
    }
}