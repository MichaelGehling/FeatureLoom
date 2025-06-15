using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Helpers;

public class DeepComparerTests
{
    #region Primitives and Strings

    [Fact]
    public void PrimitiveEquality_ShouldReturnTrue()
    {
        int a = 42;
        int b = 42;
        Assert.True(DeepComparer.AreEqual(a, b));
        Assert.True(a.EqualsDeep(b));
    }

    [Fact]
    public void PrimitiveInequality_ShouldReturnFalse()
    {
        int a = 42;
        int b = 43;
        Assert.False(DeepComparer.AreEqual(a, b));
    }

    [Fact]
    public void StringEquality_ShouldReturnTrue()
    {
        string s1 = "hello";
        string s2 = "hello";
        Assert.True(DeepComparer.AreEqual(s1, s2));
    }

    [Fact]
    public void StringInequality_ShouldReturnFalse()
    {
        string s1 = "hello";
        string s2 = "world";
        Assert.False(DeepComparer.AreEqual(s1, s2));
    }

    #endregion

    #region Class with Private and Inherited Fields

    private class TestClass
    {
        private int value;
        public TestClass(int value) { this.value = value; }
    }

    [Fact]
    public void TestClassEquality_ShouldReturnTrue()
    {
        var obj1 = new TestClass(100);
        var obj2 = new TestClass(100);
        Assert.True(DeepComparer.AreEqual(obj1, obj2));
    }

    [Fact]
    public void TestClassInequality_ShouldReturnFalse()
    {
        var obj1 = new TestClass(100);
        var obj2 = new TestClass(200);
        Assert.False(DeepComparer.AreEqual(obj1, obj2));
    }

    private class BaseClass
    {
        private int baseValue;
        public BaseClass(int value) { baseValue = value; }
    }

    private class DerivedClass : BaseClass
    {
        private string name;
        public DerivedClass(int value, string name) : base(value)
        {
            this.name = name;
        }
    }

    [Fact]
    public void InheritedFieldEquality_ShouldReturnTrue()
    {
        var obj1 = new DerivedClass(10, "Test");
        var obj2 = new DerivedClass(10, "Test");
        Assert.True(DeepComparer.AreEqual(obj1, obj2));
    }

    [Fact]
    public void InheritedFieldInequality_ShouldReturnFalse()
    {
        var obj1 = new DerivedClass(10, "Test");
        var obj2 = new DerivedClass(20, "Test");
        Assert.False(DeepComparer.AreEqual(obj1, obj2));
    }

    #endregion

    #region Cyclic References

    private class Node
    {
        public int Value;
        public Node Next;
        public Node(int value) { Value = value; }
    }

    [Fact]
    public void CyclicReference_ShouldReturnTrue()
    {
        var node1 = new Node(1);
        var node2 = new Node(1);
        // Create a cycle.
        node1.Next = node1;
        node2.Next = node2;
        Assert.True(DeepComparer.AreEqual(node1, node2));
    }

    [Fact]
    public void DifferentCycles_ShouldReturnFalse()
    {
        var node1 = new Node(1);
        var node2 = new Node(1);
        var node3 = new Node(2);
        node1.Next = node1;
        node2.Next = node3; // Different cycle structure
        Assert.False(DeepComparer.AreEqual(node1, node2));
    }

    #endregion

    #region Collections - IList<T>

    [Fact]
    public void ListEquality_ShouldReturnTrue()
    {
        var list1 = new List<int> { 1, 2, 3, 4 };
        var list2 = new List<int> { 1, 2, 3, 4 };
        Assert.True(DeepComparer.AreEqual(list1, list2));
    }

    [Fact]
    public void ListInequality_ShouldReturnFalse()
    {
        var list1 = new List<int> { 1, 2, 3, 4 };
        var list2 = new List<int> { 1, 2, 3, 5 };
        Assert.False(DeepComparer.AreEqual(list1, list2));
    }

    #endregion

    #region Collections - Dictionaries

    [Fact]
    public void DictionaryEquality_ShouldReturnTrue()
    {
        var dict1 = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 }
        };

        // Reversed order
        var dict2 = new Dictionary<string, int>
        {
            { "two", 2 },
            { "one", 1 }
        };

        Assert.True(DeepComparer.AreEqual(dict1, dict2));
    }

    [Fact]
    public void DictionaryInequality_ShouldReturnFalse()
    {
        var dict1 = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 }
        };

        var dict2 = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 3 }
        };

        Assert.False(DeepComparer.AreEqual(dict1, dict2));
    }

    [Fact]
    public void DictionaryOrderIndependence_ShouldReturnTrue()
    {
        var dict1 = new Dictionary<int, string>
        {
            { 1, "a" },
            { 2, "b" }
        };

        var dict2 = new Dictionary<int, string>
        {
            { 2, "b" },
            { 1, "a" }
        };

        Assert.True(DeepComparer.AreEqual(dict1, dict2));
    }

    #endregion

    #region Mixed Nested Collections

    private class ComplexObject
    {
        private List<int> numbers;
        private Dictionary<string, string> mapping;
        public ComplexObject(List<int> numbers, Dictionary<string, string> mapping)
        {
            this.numbers = numbers;
            this.mapping = mapping;
        }
    }

    [Fact]
    public void ComplexObjectEquality_ShouldReturnTrue()
    {
        var obj1 = new ComplexObject(
            new List<int> { 1, 2, 3 },
            new Dictionary<string, string> { { "a", "alpha" }, { "b", "beta" } });
        var obj2 = new ComplexObject(
            new List<int> { 1, 2, 3 },
            new Dictionary<string, string> { { "b", "beta" }, { "a", "alpha" } });

        Assert.True(DeepComparer.AreEqual(obj1, obj2));
    }

    [Fact]
    public void ComplexObjectInequality_ShouldReturnFalse()
    {
        var obj1 = new ComplexObject(
            new List<int> { 1, 2, 3 },
            new Dictionary<string, string> { { "a", "alpha" }, { "b", "beta" } });
        var obj2 = new ComplexObject(
            new List<int> { 1, 2, 4 },
            new Dictionary<string, string> { { "a", "alpha" }, { "b", "beta" } });

        Assert.False(DeepComparer.AreEqual(obj1, obj2));
    }

    #endregion

    // Base class
    private class A
    {
        public int Value;
    }

    // Derived class with an extra field.
    private class B : A
    {
        public int Extra;
    }

    [Fact]
    public void StrictTypeCheck_ShouldReturnFalse_WhenRuntimeTypesDiffer()
    {
        // Create an instance of A.
        A a = new A { Value = 10 };
        // Create an instance of B (which is an A with extra data).
        A b = new B { Value = 10, Extra = 20 };

        // With strictTypeCheck set to true, the runtime types are compared
        // so even though the base field "Value" is equal, the extra field in B
        // makes the types different.
        Assert.False(DeepComparer.AreEqual(a, b, strictTypeCheck: true));
    }

    [Fact]
    public void StrictTypeCheckDisabled_ShouldReturnTrue_WhenBaseFieldsAreEqual()
    {
        // Create an instance of A.
        A a = new A { Value = 10 };
        // Create an instance of B (which is an A with extra data).
        A b = new B { Value = 10, Extra = 20 };

        // When strictTypeCheck is false, the deep comparison only examines
        // the fields defined on the compile–time type (A) and ignores extra fields
        // present in the runtime type (B).
        Assert.True(DeepComparer.AreEqual(a, b, strictTypeCheck: false));
    }

    #region Null Handling

    [Fact]
    public void BothNull_ShouldReturnTrue()
    {
        object a = null;
        object b = null;
        Assert.True(DeepComparer.AreEqual(a, b));
    }

    [Fact]
    public void NullVsNonNull_ShouldReturnFalse()
    {
        object a = null;
        object b = new object();
        Assert.False(DeepComparer.AreEqual(a, b));
        Assert.False(DeepComparer.AreEqual(b, a));
    }

    #endregion

    #region Nullable Value Types

    [Fact]
    public void NullableValueTypes_EqualValues_ShouldReturnTrue()
    {
        int? a = 5;
        int? b = 5;
        Assert.True(DeepComparer.AreEqual(a, b));
    }

    [Fact]
    public void NullableValueTypes_OneNull_ShouldReturnFalse()
    {
        int? a = null;
        int? b = 5;
        Assert.False(DeepComparer.AreEqual(a, b));
        Assert.False(DeepComparer.AreEqual(b, a));
    }

    [Fact]
    public void NullableValueTypes_BothNull_ShouldReturnTrue()
    {
        int? a = null;
        int? b = null;
        Assert.True(DeepComparer.AreEqual(a, b));
    }

    #endregion

    #region Empty Collections

    [Fact]
    public void EmptyLists_ShouldReturnTrue()
    {
        var list1 = new List<int>();
        var list2 = new List<int>();
        Assert.True(DeepComparer.AreEqual(list1, list2));
    }

    [Fact]
    public void EmptyVsNonEmptyList_ShouldReturnFalse()
    {
        var list1 = new List<int>();
        var list2 = new List<int> { 1 };
        Assert.False(DeepComparer.AreEqual(list1, list2));
    }

    [Fact]
    public void EmptyDictionaries_ShouldReturnTrue()
    {
        var dict1 = new Dictionary<string, int>();
        var dict2 = new Dictionary<string, int>();
        Assert.True(DeepComparer.AreEqual(dict1, dict2));
    }

    [Fact]
    public void EmptyVsNonEmptyDictionary_ShouldReturnFalse()
    {
        var dict1 = new Dictionary<string, int>();
        var dict2 = new Dictionary<string, int> { { "a", 1 } };
        Assert.False(DeepComparer.AreEqual(dict1, dict2));
    }

    #endregion

    #region Collections with Reference Types

    private class RefType
    {
        public int X;
        public RefType(int x) { X = x; }
    }

    [Fact]
    public void ListOfReferenceTypes_Equal_ShouldReturnTrue()
    {
        var list1 = new List<RefType> { new RefType(1), new RefType(2) };
        var list2 = new List<RefType> { new RefType(1), new RefType(2) };
        Assert.True(DeepComparer.AreEqual(list1, list2));
    }

    [Fact]
    public void ListOfReferenceTypes_NotEqual_ShouldReturnFalse()
    {
        var list1 = new List<RefType> { new RefType(1), new RefType(2) };
        var list2 = new List<RefType> { new RefType(1), new RefType(3) };
        Assert.False(DeepComparer.AreEqual(list1, list2));
    }

    #endregion

    #region Dictionaries with Complex Keys

    private class KeyType
    {
        public int Id;
        public KeyType(int id) { Id = id; }
    }

    [Fact]
    public void DictionaryWithComplexKeys_Equal_ShouldReturnTrue()
    {
        var dict1 = new Dictionary<KeyType, string>
        {
            { new KeyType(1), "a" },
            { new KeyType(2), "b" }
        };
        var dict2 = new Dictionary<KeyType, string>
        {
            { new KeyType(2), "b" },
            { new KeyType(1), "a" }
        };
        Assert.True(DeepComparer.AreEqual(dict1, dict2));
    }

    [Fact]
    public void DictionaryWithComplexKeys_NotEqual_ShouldReturnFalse()
    {
        var dict1 = new Dictionary<KeyType, string>
        {
            { new KeyType(1), "a" },
            { new KeyType(2), "b" }
        };
        var dict2 = new Dictionary<KeyType, string>
        {
            { new KeyType(1), "a" },
            { new KeyType(2), "c" }
        };
        Assert.False(DeepComparer.AreEqual(dict1, dict2));
    }

    #endregion

    #region Deeply Nested Structures

    private class Nested
    {
        public Nested Child;
        public int Value;
        public Nested(int value, Nested child = null)
        {
            Value = value;
            Child = child;
        }
    }

    [Fact]
    public void DeeplyNestedObjects_Equal_ShouldReturnTrue()
    {
        var n1 = new Nested(1, new Nested(2, new Nested(3)));
        var n2 = new Nested(1, new Nested(2, new Nested(3)));
        Assert.True(DeepComparer.AreEqual(n1, n2));
    }

    [Fact]
    public void DeeplyNestedObjects_NotEqual_ShouldReturnFalse()
    {
        var n1 = new Nested(1, new Nested(2, new Nested(3)));
        var n2 = new Nested(1, new Nested(2, new Nested(4)));
        Assert.False(DeepComparer.AreEqual(n1, n2));
    }

    #endregion
}
