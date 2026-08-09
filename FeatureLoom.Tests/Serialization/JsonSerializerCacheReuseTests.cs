using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    /// <summary>
    /// Characterization tests that lock in behavior which the existing serializer test suite
    /// does not cover, because every other test helper creates a fresh <see cref="JsonSerializer"/>
    /// and serializes exactly once.
    ///
    /// These tests deliberately exercise:
    /// - repeated serialization through one serializer instance (warm type writer cache),
    /// - the same type reached through different parents,
    /// - recursive types (the cached type writer is registered before it is built),
    /// - the nullable-struct complex handler under every DataSelection mode,
    /// - comma bookkeeping when members are skipped,
    /// - all three branches of CreateFieldValueWriter.
    ///
    /// They must stay green across the per-type/per-member settings refactoring.
    /// </summary>
    public class JsonSerializerCacheReuseTests
    {
        private static void AssertNoMalformedCommas(string json)
        {
            Assert.DoesNotContain(",,", json);
            Assert.DoesNotContain("{,", json);
            Assert.DoesNotContain(",}", json);
            Assert.DoesNotContain("[,", json);
            Assert.DoesNotContain(",]", json);
            Assert.DoesNotContain("{ ,", json);
            Assert.DoesNotContain(", }", json);
        }

        // --- Type writer cache reuse -------------------------------------------------

        [Fact]
        public void Serialize_SameTypeTwice_SameSerializer_ProducesIdenticalOutput()
        {
            var serializer = new JsonSerializer();
            var value = new SimpleItem { Id = 7, Name = "abc" };

            string first = serializer.Serialize(value);
            string second = serializer.Serialize(value);

            Assert.Equal("{\"Id\":7,\"Name\":\"abc\"}", first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Serialize_SameTypeManyTimes_SameSerializer_StaysStable()
        {
            var serializer = new JsonSerializer();
            var value = new SimpleItem { Id = 1, Name = "x" };

            string expected = serializer.Serialize(value);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(expected, serializer.Serialize(value));
            }
        }

        [Fact]
        public void Serialize_TypeAlone_ThenNested_ThenAloneAgain_IsConsistent()
        {
            var serializer = new JsonSerializer();
            var inner = new SimpleItem { Id = 3, Name = "inner" };
            var outer = new OuterWithSingleInner { Label = "outer", Inner = inner };

            string standaloneBefore = serializer.Serialize(inner);
            string nested = serializer.Serialize(outer);
            string standaloneAfter = serializer.Serialize(inner);

            Assert.Equal("{\"Id\":3,\"Name\":\"inner\"}", standaloneBefore);
            Assert.Equal("{\"Label\":\"outer\",\"Inner\":{\"Id\":3,\"Name\":\"inner\"}}", nested);
            Assert.Equal(standaloneBefore, standaloneAfter);
        }

        [Fact]
        public void Serialize_SameTypeUnderTwoDifferentMembers_BothUseSameShape()
        {
            var serializer = new JsonSerializer();
            var value = new OuterWithTwoInners
            {
                First = new SimpleItem { Id = 1, Name = "a" },
                Second = new SimpleItem { Id = 2, Name = "b" }
            };

            string json = serializer.Serialize(value);

            Assert.Equal("{\"First\":{\"Id\":1,\"Name\":\"a\"},\"Second\":{\"Id\":2,\"Name\":\"b\"}}", json);
        }

        [Fact]
        public void Serialize_SameTypeInTwoDifferentParents_SameSerializer_IsConsistent()
        {
            var serializer = new JsonSerializer();
            var inner = new SimpleItem { Id = 5, Name = "shared" };

            string viaSingle = serializer.Serialize(new OuterWithSingleInner { Label = "L", Inner = inner });
            string viaTwo = serializer.Serialize(new OuterWithTwoInners { First = inner, Second = null });

            Assert.Equal("{\"Label\":\"L\",\"Inner\":{\"Id\":5,\"Name\":\"shared\"}}", viaSingle);
            Assert.Equal("{\"First\":{\"Id\":5,\"Name\":\"shared\"},\"Second\":null}", viaTwo);
        }

        // --- Recursive / self referencing types --------------------------------------

        [Fact]
        public void Serialize_SelfReferencingType_NullTerminated_NoRefCheck()
        {
            var serializer = new JsonSerializer();
            var chain = new RecursiveNode { Value = 1, Next = new RecursiveNode { Value = 2, Next = null } };

            string json = serializer.Serialize(chain);

            Assert.Equal("{\"Value\":1,\"Next\":{\"Value\":2,\"Next\":null}}", json);
        }

        [Fact]
        public void Serialize_SelfReferencingType_Repeated_SameSerializer()
        {
            var serializer = new JsonSerializer();
            var chain = new RecursiveNode { Value = 1, Next = new RecursiveNode { Value = 2, Next = null } };

            string first = serializer.Serialize(chain);
            string second = serializer.Serialize(chain);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Serialize_MutuallyRecursiveTypes_NullTerminated()
        {
            var serializer = new JsonSerializer();
            var a = new MutualA { Name = "a", B = new MutualB { Number = 9, A = null } };

            string json = serializer.Serialize(a);

            Assert.Equal("{\"Name\":\"a\",\"B\":{\"Number\":9,\"A\":null}}", json);
        }

        [Fact]
        public void Serialize_RecursiveTypeThroughCollection_NullTerminated()
        {
            var serializer = new JsonSerializer();
            var tree = new TreeNode
            {
                Value = 1,
                Children = new List<TreeNode>
                {
                    new TreeNode { Value = 2, Children = null }
                }
            };

            string json = serializer.Serialize(tree);

            Assert.Equal("{\"Value\":1,\"Children\":[{\"Value\":2,\"Children\":null}]}", json);
        }

        // --- Nullable struct complex handler ------------------------------------------

        [Fact]
        public void Serialize_NullableStruct_DefaultDataSelection()
        {
            var serializer = new JsonSerializer();
            StructWithMembers? value = new StructWithMembers(1, 2);

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Contains("\"PublicField\":1", json);
        }

        [Fact]
        public void Serialize_NullableStruct_PublicFieldsAndProperties()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicFieldsAndProperties
            });
            StructWithMembers? value = new StructWithMembers(1, 2);

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Contains("\"PublicField\":1", json);
            Assert.Contains("\"PublicProp\":2", json);
        }

        [Fact]
        public void Serialize_NullableStruct_RemoveBackingFields()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicAndPrivateFields_RemoveBackingFields
            });
            StructWithMembers? value = new StructWithMembers(1, 2);

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Contains("\"PublicField\":1", json);
        }

        [Fact]
        public void Serialize_NullableStruct_Repeated_SameSerializer()
        {
            var serializer = new JsonSerializer();
            StructWithMembers? value = new StructWithMembers(4, 5);

            string first = serializer.Serialize(value);
            string second = serializer.Serialize(value);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Serialize_NullableStruct_NullThenValue_SameSerializer()
        {
            var serializer = new JsonSerializer();
            StructWithMembers? nothing = null;
            StructWithMembers? something = new StructWithMembers(1, 2);

            string nullJson = serializer.Serialize(nothing);
            string valueJson = serializer.Serialize(something);

            Assert.Equal("null", nullJson);
            AssertNoMalformedCommas(valueJson);
            Assert.Contains("\"PublicField\":1", valueJson);
        }

        // --- Comma bookkeeping when members are skipped -------------------------------

        [Fact]
        public void Serialize_FirstMemberIgnored_NoLeadingComma()
        {
            var serializer = new JsonSerializer();

            string json = serializer.Serialize(new FirstMemberIgnored());

            AssertNoMalformedCommas(json);
            Assert.Equal("{\"Second\":2,\"Third\":3}", json);
        }

        [Fact]
        public void Serialize_MiddleMemberIgnored_NoDoubleComma()
        {
            var serializer = new JsonSerializer();

            string json = serializer.Serialize(new MiddleMemberIgnored());

            AssertNoMalformedCommas(json);
            Assert.Equal("{\"First\":1,\"Third\":3}", json);
        }

        [Fact]
        public void Serialize_LastMemberIgnored_NoTrailingComma()
        {
            var serializer = new JsonSerializer();

            string json = serializer.Serialize(new LastMemberIgnored());

            AssertNoMalformedCommas(json);
            Assert.Equal("{\"First\":1,\"Second\":2}", json);
        }

        [Fact]
        public void Serialize_AllMembersIgnored_EmptyObject()
        {
            var serializer = new JsonSerializer();

            string json = serializer.Serialize(new AllMembersIgnored());

            AssertNoMalformedCommas(json);
            Assert.Equal("{}", json);
        }

        [Fact]
        public void Serialize_SingleMember_NoComma()
        {
            var serializer = new JsonSerializer();

            string json = serializer.Serialize(new SingleMember());

            AssertNoMalformedCommas(json);
            Assert.Equal("{\"Only\":1}", json);
        }

        [Fact]
        public void Serialize_FirstMemberIgnored_Indented_NoMalformedCommas()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings { indent = true });

            string json = serializer.Serialize(new FirstMemberIgnored());

            AssertNoMalformedCommas(json);
            Assert.Contains("\"Second\"", json);
            Assert.Contains("\"Third\"", json);
            Assert.DoesNotContain("\"First\"", json);
        }

        [Fact]
        public void Serialize_AllMembersIgnored_Indented_NoMalformedCommas()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings { indent = true });

            string json = serializer.Serialize(new AllMembersIgnored());

            AssertNoMalformedCommas(json);
        }

        [Fact]
        public void Serialize_NestedObjectWithIgnoredMembers_Indented_NoMalformedCommas()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings { indent = true });

            string json = serializer.Serialize(new OuterWithIgnoredInner
            {
                Label = "L",
                Inner = new MiddleMemberIgnored()
            });

            AssertNoMalformedCommas(json);
            Assert.DoesNotContain("\"Second\"", json);
        }

        // --- CreateFieldValueWriter branch coverage -----------------------------------

        [Fact]
        public void Serialize_AllFieldWriterBranches_InOneType()
        {
            var serializer = new JsonSerializer();
            var value = new AllBranchShapes
            {
                Primitive = 42,
                NonNullableComplex = new SimpleItem { Id = 1, Name = "n" },
                Boxed = "boxedString"
            };

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Contains("\"Primitive\":42", json);
            Assert.Contains("\"NonNullableComplex\":{\"Id\":1,\"Name\":\"n\"}", json);
            // The Boxed member is declared as object, so its runtime type deviates and the
            // default AddDeviatingTypeInfo wraps the value in a $type/$value envelope.
            string stringTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(string));
            Assert.Contains($"\"Boxed\":{{\"$type\":\"{stringTypeName}\",\"$value\":\"boxedString\"}}", json);
        }

        [Fact]
        public void Serialize_AllFieldWriterBranches_InOneType_NoTypeInfo()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
            var value = new AllBranchShapes
            {
                Primitive = 42,
                NonNullableComplex = new SimpleItem { Id = 1, Name = "n" },
                Boxed = "boxedString"
            };

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Equal("{\"Primitive\":42,\"NonNullableComplex\":{\"Id\":1,\"Name\":\"n\"},\"Boxed\":\"boxedString\"}", json);
        }

        [Fact]
        public void Serialize_AllFieldWriterBranches_WithNulls()
        {
            var serializer = new JsonSerializer();
            var value = new AllBranchShapes
            {
                Primitive = 0,
                NonNullableComplex = null,
                Boxed = null
            };

            string json = serializer.Serialize(value);

            AssertNoMalformedCommas(json);
            Assert.Contains("\"NonNullableComplex\":null", json);
            Assert.Contains("\"Boxed\":null", json);
        }

        [Fact]
        public void Serialize_BoxedField_DerivedValue_Repeated_SameSerializer()
        {
            var serializer = new JsonSerializer();
            var value = new AllBranchShapes
            {
                Primitive = 1,
                NonNullableComplex = new SimpleItem { Id = 1, Name = "n" },
                Boxed = new SimpleItem { Id = 2, Name = "m" }
            };

            string first = serializer.Serialize(value);
            string second = serializer.Serialize(value);

            AssertNoMalformedCommas(first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Serialize_NullableValueTypeField_ValueAndNull()
        {
            // A nullable value type field takes the dynamic fallback branch of CreateFieldValueWriter.
            // int? holding 5 is not a type deviation, so no $type/$value envelope must be written.
            var serializer = new JsonSerializer();

            string withValue = serializer.Serialize(new WithNullableField { Value = 5 });
            string withNull = serializer.Serialize(new WithNullableField { Value = null });

            Assert.Equal("{\"Value\":5}", withValue);
            Assert.Equal("{\"Value\":null}", withNull);
        }

        [Fact]
        public void Serialize_NullableValueTypeField_ValueAndNull_NoTypeInfo()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });

            string withValue = serializer.Serialize(new WithNullableField { Value = 5 });
            string withNull = serializer.Serialize(new WithNullableField { Value = null });

            Assert.Equal("{\"Value\":5}", withValue);
            Assert.Equal("{\"Value\":null}", withNull);
        }

        [Fact]
        public void Serialize_NullableValueTypeField_Repeated_SameSerializer()
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
            var value = new WithNullableField { Value = 5 };

            string first = serializer.Serialize(value);
            string second = serializer.Serialize(value);

            Assert.Equal("{\"Value\":5}", first);
            Assert.Equal(first, second);
        }

        // --- Test types ----------------------------------------------------------------

        private class SimpleItem
        {
            public int Id;
            public string Name;
        }

        private class OuterWithSingleInner
        {
            public string Label;
            public SimpleItem Inner;
        }

        private class OuterWithTwoInners
        {
            public SimpleItem First;
            public SimpleItem Second;
        }

        private class RecursiveNode
        {
            public int Value;
            public RecursiveNode Next;
        }

        private class MutualA
        {
            public string Name;
            public MutualB B;
        }

        private class MutualB
        {
            public int Number;
            public MutualA A;
        }

        private class TreeNode
        {
            public int Value;
            public List<TreeNode> Children;
        }

        private struct StructWithMembers
        {
            public int PublicField;
            public int PublicProp { get; set; }

            public StructWithMembers(int publicField, int publicProp)
            {
                PublicField = publicField;
                PublicProp = publicProp;
            }
        }

        private class FirstMemberIgnored
        {
            [JsonIgnore]
            public int First = 1;
            public int Second = 2;
            public int Third = 3;
        }

        private class MiddleMemberIgnored
        {
            public int First = 1;
            [JsonIgnore]
            public int Second = 2;
            public int Third = 3;
        }

        private class LastMemberIgnored
        {
            public int First = 1;
            public int Second = 2;
            [JsonIgnore]
            public int Third = 3;
        }

        private class AllMembersIgnored
        {
            [JsonIgnore]
            public int First = 1;
            [JsonIgnore]
            public int Second = 2;
        }

        private class SingleMember
        {
            public int Only = 1;
        }

        private class OuterWithIgnoredInner
        {
            public string Label;
            public MiddleMemberIgnored Inner;
        }

        private class AllBranchShapes
        {
            public int Primitive;
            public SimpleItem NonNullableComplex;
            public object Boxed;
        }

        private class WithNullableField
        {
            public int? Value;
        }
    }
}
