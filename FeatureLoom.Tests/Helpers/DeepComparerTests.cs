using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Helpers
{
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
    }
}
