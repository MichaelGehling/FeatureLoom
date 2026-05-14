using FeatureLoom.Serialization;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerReferenceTests
    {
        private static T Deserialize<T>(string json)
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault
            };
            var deserializer = new JsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);
            return deserializer.TryDeserialize(json, out value);
        }

        private static bool TryPopulate<T>(string json, T value) where T : class
        {
            var settings = new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);
            return deserializer.TryPopulate(json, value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_SelfReference()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$\"}}";
            var value = Deserialize<Node>(json);

            Assert.Same(value, value.Next);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_NestedSelfReference()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"Name\":\"nested\",\"Next\":{\"$ref\":\"$.Next\"}}}";
            var value = Deserialize<Node>(json);

            Assert.Same(value.Next, value.Next.Next);
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
            Assert.Same(value.First.Shared, value.Second.Shared);
        }

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

        [Fact]
        public void Populate_ReferenceResolution_SelfReferenceToRoot_Resolves()
        {
            var value = new Node { Name = "root" };

            const string json = "{\"Next\":{\"$ref\":\"$\"}}";
            Assert.True(TryPopulate(json, value));

            Assert.Same(value, value.Next);
        }

        [Fact]
        public void Populate_ReferenceResolution_RefToPopulatedNestedObject_Resolves()
        {
            var value = new NodeContainer
            {
                Child = new Node { Name = "old-child" },
                Other = null
            };

            const string json = "{\"Child\":{\"Name\":\"new-child\"},\"Other\":{\"$ref\":\"$.Child\"}}";
            Assert.True(TryPopulate(json, value));

            Assert.Equal("new-child", value.Child.Name);
            Assert.Same(value.Child, value.Other);
        }

        [Fact]
        public void Populate_ReferenceResolution_RefToPopulatedStructMemberObject_Resolves()
        {
            var value = new StructReferenceContainer
            {
                First = new NodePair
                {
                    Shared = new Node { Name = "old-shared" },
                    Other = new Node { Name = "first-other" }
                },
                Second = new NodePair
                {
                    Shared = null,
                    Other = new Node { Name = "second-other" }
                }
            };

            const string json =
                "{\"First\":{\"Shared\":{\"Name\":\"new-shared\"}}," +
                "\"Second\":{\"Shared\":{\"$ref\":\"$.First.Shared\"}}}";

            Assert.True(TryPopulate(json, value));
            Assert.Equal("new-shared", value.First.Shared.Name);
            Assert.Same(value.First.Shared, value.Second.Shared);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_GenericListElementCanReferenceContainingList()
        {
            const string json = "[{\"$ref\":\"$\"}]";
            var value = Deserialize<List<object>>(json);

            Assert.Same(value, value[0]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_NonGenericEnumerableElementCanReferenceContainingCollection()
        {
            const string json = "[{\"$ref\":\"$\"}]";
            var value = Deserialize<ArrayList>(json);

            Assert.Same(value, value[0]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ListMemberElementCanReferenceContainingListMember()
        {
            const string json = "{\"Items\":[{\"$ref\":\"$.Items\"}]}";
            var value = Deserialize<ListHolder>(json);

            Assert.Same(value.Items, value.Items[0]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ArrayListMemberElementCanReferenceContainingArrayListMember()
        {
            const string json = "{\"Items\":[{\"$ref\":\"$.Items\"}]}";
            var value = Deserialize<ArrayListHolder>(json);

            Assert.Same(value.Items, value.Items[0]);
        }

        // -------- New red tests for collection self-reference --------
        /*
        [Fact]
        public void Deserialize_ReferenceResolution_ArrayElementCanReferenceContainingArray()
        {
            const string json = "[{\"$ref\":\"$\"}]";
            var value = Deserialize<object[]>(json);

            Assert.Same(value, value[0]);
        }
        */


        /*
        [Fact]
        public void Deserialize_ReferenceResolution_ArrayMemberElementCanReferenceContainingArrayMember()
        {
            const string json = "{\"Items\":[{\"$ref\":\"$.Items\"}]}";
            var value = Deserialize<ArrayHolder>(json);

            Assert.Same(value.Items, value.Items[0]);
        }
        */

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_SelfReference_Works()
        {
            const string json = "{\"$id\":\"root\",\"Name\":\"root\",\"Next\":{\"$ref\":\"root\"}}";
            var value = Deserialize<Node>(json);

            Assert.Same(value, value.Next);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_SharedInCollection_Works()
        {
            const string json = "{\"Items\":[{\"$id\":\"n1\",\"Name\":\"a\"},{\"$ref\":\"n1\"}]}";
            var value = Deserialize<NodeList>(json);

            Assert.Same(value.Items[0], value.Items[1]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_And_JsonPath_CanBeMixed()
        {
            const string json =
                "{\"Child\":{\"$id\":\"n1\",\"Name\":\"a\"}," +
                "\"Other\":{\"$ref\":\"n1\"}," +
                "\"Ref\":{\"$ref\":\"$.Child\"}}";

            var value = Deserialize<ThreeNodeContainer>(json);

            Assert.Same(value.Child, value.Other);
            Assert.Same(value.Child, value.Ref);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_IsPreferred_OverJsonPath_WhenBothMatchPattern()
        {
            const string json =
                "{\"Child\":{\"$id\":\"$.Other\",\"Name\":\"id-target\"}," +
                "\"Other\":{\"Name\":\"path-target\"}," +
                "\"Ref\":{\"$ref\":\"$.Other\"}}";

            var value = Deserialize<ThreeNodeContainer>(json);

            // If $id/$ref is tried first, this resolves to Child (id-target), not Other (path-target)
            Assert.Same(value.Child, value.Ref);
            Assert.NotSame(value.Other, value.Ref);
            Assert.Equal("id-target", value.Ref.Name);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_SameObjectReferencedMultipleTimes_Works()
        {
            const string json =
                "{\"Root\":{\"$id\":\"n1\",\"Name\":\"shared\"}," +
                "\"A\":{\"$ref\":\"n1\"}," +
                "\"B\":{\"$ref\":\"n1\"}," +
                "\"C\":{\"$ref\":\"n1\"}}";

            var value = Deserialize<MultiReferenceContainer>(json);

            Assert.Same(value.Root, value.A);
            Assert.Same(value.Root, value.B);
            Assert.Same(value.Root, value.C);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_SameObjectReferencedMultipleTimes_InCollection_Works()
        {
            const string json =
                "{\"Items\":[{\"$id\":\"n1\",\"Name\":\"shared\"},{\"$ref\":\"n1\"},{\"$ref\":\"n1\"},{\"$ref\":\"n1\"}]}";

            var value = Deserialize<NodeList>(json);

            Assert.Same(value.Items[0], value.Items[1]);
            Assert.Same(value.Items[0], value.Items[2]);
            Assert.Same(value.Items[0], value.Items[3]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_OneObjectReferencedByPathAndId_Works()
        {
            const string json =
                "{\"Root\":{\"$id\":\"n1\",\"Name\":\"shared\"}," +
                "\"ViaId\":{\"$ref\":\"n1\"}," +
                "\"ViaPath\":{\"$ref\":\"$.Root\"}}";

            var value = Deserialize<MixedReferenceContainer>(json);

            Assert.Same(value.Root, value.ViaId);
            Assert.Same(value.Root, value.ViaPath);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_OneObjectReferencedByPathAndIdAndRepeated_Works()
        {
            const string json =
                "{\"Root\":{\"$id\":\"n1\",\"Name\":\"shared\"}," +
                "\"ViaId\":{\"$ref\":\"n1\"}," +
                "\"ViaPath\":{\"$ref\":\"$.Root\"}," +
                "\"ViaIdAgain\":{\"$ref\":\"n1\"}}";

            var value = Deserialize<MixedReferenceContainer>(json);

            Assert.Same(value.Root, value.ViaId);
            Assert.Same(value.Root, value.ViaPath);
            Assert.Same(value.Root, value.ViaIdAgain);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_ForwardReference_ReturnsFalse()
        {
            const string json = "{\"A\":{\"$ref\":\"n1\"},\"Root\":{\"$id\":\"n1\",\"Name\":\"x\"}}";

            Assert.False(TryDeserialize(json, out MultiReferenceContainer value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_DuplicateId_LastMatchWins()
        {
            const string json =
                "{\"Child\":{\"$id\":\"n1\",\"Name\":\"first\"}," +
                "\"Other\":{\"$id\":\"n1\",\"Name\":\"second\"}," +
                "\"Ref\":{\"$ref\":\"n1\"}}";

            var value = Deserialize<ThreeNodeContainer>(json);

            Assert.Same(value.Other, value.Ref);
            Assert.NotSame(value.Child, value.Ref);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_ValidIdButIncompatibleType_ReturnsFalse()
        {
            const string json = "{\"Node\":{\"$id\":\"n1\",\"Name\":\"x\"},\"Container\":{\"$ref\":\"n1\"}}";

            Assert.False(TryDeserialize(json, out MixedTypeContainer value));
            Assert.Null(value);
        }

        [Fact]
        public void Populate_ReferenceResolution_IdRef_Resolves()
        {
            var value = new NodeContainer
            {
                Child = new Node { Name = "old-child" },
                Other = null
            };

            const string json = "{\"Child\":{\"$id\":\"n1\",\"Name\":\"new-child\"},\"Other\":{\"$ref\":\"n1\"}}";
            Assert.True(TryPopulate(json, value));

            Assert.Equal("new-child", value.Child.Name);
            Assert.Same(value.Child, value.Other);
        }

        [Fact]
        public void Populate_ReferenceResolution_IdRef_And_Path_Mixed_Resolves()
        {
            var value = new MixedReferenceContainer
            {
                Root = new Node { Name = "old-root" },
                ViaId = null,
                ViaPath = null,
                ViaIdAgain = null
            };

            const string json =
                "{\"Root\":{\"$id\":\"n1\",\"Name\":\"new-root\"}," +
                "\"ViaId\":{\"$ref\":\"n1\"}," +
                "\"ViaPath\":{\"$ref\":\"$.Root\"}," +
                "\"ViaIdAgain\":{\"$ref\":\"n1\"}}";

            Assert.True(TryPopulate(json, value));

            Assert.Equal("new-root", value.Root.Name);
            Assert.Same(value.Root, value.ViaId);
            Assert.Same(value.Root, value.ViaPath);
            Assert.Same(value.Root, value.ViaIdAgain);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdRef_IdFieldNotFirst_DoesNotRegister_ReturnsFalse()
        {
            const string json = "{\"Child\":{\"Name\":\"x\",\"$id\":\"n1\"},\"Other\":{\"$ref\":\"n1\"}}";

            Assert.False(TryDeserialize(json, out NodeContainer value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ObjectWithRefThenId_ResolvesByRef_AndSkipsRemainingFields()
        {
            const string json = "{\"Child\":{\"$id\":\"n1\",\"Name\":\"x\"},\"Other\":{\"$ref\":\"n1\",\"$id\":\"n2\"}}";
            var value = Deserialize<NodeContainer>(json);

            Assert.Same(value.Child, value.Other);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_ObjectWithIdThenRef_IsTreatedAsNormalObject()
        {
            const string json = "{\"Child\":{\"$id\":\"n1\",\"Name\":\"x\"},\"Other\":{\"$id\":\"n2\",\"$ref\":\"n1\",\"Name\":\"y\"}}";
            var value = Deserialize<NodeContainer>(json);

            Assert.NotSame(value.Child, value.Other);
            Assert.Equal("y", value.Other.Name);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdAndValues_ListObjectCanBeReferencedById()
        {
            const string json =
                "{\"Items\":{\"$id\":\"list1\",\"$values\":[{\"Name\":\"a\"}]}," +
                "\"Ref\":{\"$ref\":\"list1\"}}";

            var value = Deserialize<ListReferenceContainer>(json);

            Assert.NotNull(value.Items);
            Assert.Single(value.Items);
            Assert.Same(value.Items, value.Ref);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdAndValues_ListElementsCanUsePathRefs()
        {
            const string json =
                "{\"Items\":{\"$id\":\"list1\",\"$values\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[0]\"}]}}";

            var value = Deserialize<NodeList>(json);

            Assert.Equal(2, value.Items.Count);
            Assert.Same(value.Items[0], value.Items[1]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdAndValues_ListCanContainSelfReferenceById()
        {
            const string json = "{\"Items\":{\"$id\":\"list1\",\"$values\":[{\"$ref\":\"list1\"}]}}";

            var value = Deserialize<ListHolder>(json);

            Assert.Single(value.Items);
            Assert.Same(value.Items, value.Items[0]);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_IdAndValues_ForwardRefInsideValues_ReturnsFalse()
        {
            const string json =
                "{\"Items\":{\"$id\":\"list1\",\"$values\":[{\"$ref\":\"n1\"},{\"$id\":\"n1\",\"Name\":\"x\"}]}}";

            Assert.False(TryDeserialize(json, out NodeList value));
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

        private class ArrayHolder
        {
            public object[] Items;
        }

        private class ListHolder
        {
            public List<object> Items;
        }

        private class ArrayListHolder
        {
            public ArrayList Items;
        }

        private class ThreeNodeContainer
        {
            public Node Child;
            public Node Other;
            public Node Ref;
        }

        private class MultiReferenceContainer
        {
            public Node Root;
            public Node A;
            public Node B;
            public Node C;
        }

        private class MixedReferenceContainer
        {
            public Node Root;
            public Node ViaId;
            public Node ViaPath;
            public Node ViaIdAgain;
        }

        private class ListReferenceContainer
        {
            public List<Node> Items;
            public object Ref;
        }
    }
}