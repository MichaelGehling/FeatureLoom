using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Extensions;

public class CloningExtensionsTests
{
    private class Node
    {
        public int Value;
        public Node Next;
    }

    private class Pair
    {
        public Node First;
        public Node Second;
    }

    private class ComplexContainer
    {
        public string Name;
        public List<Node> Items;
        public Dictionary<int, Node> Map;
    }

    private struct Coordinates
    {
        public int X;
        public int Y;
    }

    private struct StructWithRefs
    {
        public int Id;
        public Node Anchor;
        public List<int> Values;
    }

    private struct Metrics
    {
        public int Count;
        public decimal Ratio;
        public Node Anchor;
    }

    private class DeepNestedContainer
    {
        public string Name;
        public Dictionary<string, List<Metrics>> Groups;
        public List<Dictionary<int, string[]>> LookupLayers;
    }

    private interface INodeOwner
    {
        Node Node { get; set; }
    }

    private class NodeOwner : INodeOwner
    {
        public Node Node { get; set; }
    }

    private abstract class AbstractOwner
    {
        public Node Marker;
    }

    private sealed class ConcreteOwner : AbstractOwner
    {
        public int Id;
    }

    private sealed class EnumerableWithoutAdd : IEnumerable<int>
    {
        private readonly List<int> values = new List<int>();

        public EnumerableWithoutAdd() { }

        public EnumerableWithoutAdd(params int[] items)
        {
            if (items != null) values.AddRange(items);
        }

        public IEnumerator<int> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ContainerWithEnumerableWithoutAdd
    {
        public EnumerableWithoutAdd Data;
    }

    private struct StructWithInterfaceOwner
    {
        public int Id;
        public INodeOwner Owner;
    }

    private sealed class ThrowingAddList : IList
    {
        private readonly List<object> items = new List<object>();

        public ThrowingAddList() { }

        public ThrowingAddList(params object[] seed)
        {
            if (seed != null) items.AddRange(seed);
        }

        public int Add(object value) => throw new InvalidOperationException("Add is not allowed.");
        public void Clear() => items.Clear();
        public bool Contains(object value) => items.Contains(value);
        public int IndexOf(object value) => items.IndexOf(value);
        public void Insert(int index, object value) => throw new InvalidOperationException("Insert is not allowed.");
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public void Remove(object value) => items.Remove(value);
        public void RemoveAt(int index) => items.RemoveAt(index);
        public object this[int index] { get => items[index]; set => items[index] = value; }

        public void CopyTo(Array array, int index) => ((ICollection)items).CopyTo(array, index);
        public int Count => items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => ((ICollection)items).SyncRoot;
        public IEnumerator GetEnumerator() => items.GetEnumerator();
    }

    private sealed class ThrowingAddDictionary : IDictionary
    {
        private readonly Dictionary<object, object> items = new Dictionary<object, object>();

        public ThrowingAddDictionary() { }

        public ThrowingAddDictionary(params (object Key, object Value)[] seed)
        {
            if (seed == null) return;
            foreach (var kv in seed) items[kv.Key] = kv.Value;
        }

        public void Add(object key, object value) => throw new InvalidOperationException("Add is not allowed.");
        public void Clear() => items.Clear();
        public bool Contains(object key) => items.ContainsKey(key);
        public IDictionaryEnumerator GetEnumerator() => ((IDictionary)items).GetEnumerator();
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public ICollection Keys => items.Keys.ToList();
        public void Remove(object key) => items.Remove(key);
        public ICollection Values => items.Values.ToList();
        public object this[object key] { get => items[key]; set => items[key] = value; }

        public void CopyTo(Array array, int index) => ((IDictionary)items).CopyTo(array, index);
        public int Count => items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => ((IDictionary)items).SyncRoot;
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    }

    private sealed class ThrowingAddEnumerable : IEnumerable<int>
    {
        private readonly List<int> values = new List<int>();

        public ThrowingAddEnumerable() { }

        public ThrowingAddEnumerable(params int[] seed)
        {
            if (seed != null) values.AddRange(seed);
        }

        public void Add(int value) => throw new InvalidOperationException("Add is not allowed.");

        public IEnumerator<int> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadonlyFieldContainer
    {
        public readonly Node Node;
        public readonly List<int> Values;

        public ReadonlyFieldContainer(Node node, List<int> values)
        {
            Node = node;
            Values = values;
        }
    }
    private struct ReadonlyStructContainer
    {
        public readonly Node Node;
        public readonly List<int> Values;

        public ReadonlyStructContainer(Node node, List<int> values)
        {
            Node = node;
            Values = values;
        }
    }

    private sealed class OnlyParameterizedCtorContainer
    {
        public readonly int Id;
        public Node Node;

        public OnlyParameterizedCtorContainer(int id, Node node)
        {
            Id = id;
            Node = node;
        }
    }

    private sealed class ObjectPayloadContainer
    {
        public object Payload;
        public object[] Items;
    }

    private abstract class BaseReadonlyOwner
    {
        public readonly Node BaseNode;

        protected BaseReadonlyOwner(Node baseNode)
        {
            BaseNode = baseNode;
        }
    }

    private sealed class DerivedReadonlyOwner : BaseReadonlyOwner
    {
        public readonly List<int> Numbers;
        public Node Tail;

        public DerivedReadonlyOwner(Node baseNode, List<int> numbers, Node tail) : base(baseNode)
        {
            Numbers = numbers;
            Tail = tail;
        }
    }

    [Fact]
    public void TryClone_NullReference_ShouldReturnTrueAndNullClone()
    {
        Node source = null;

        bool success = source.TryCloneDeep(out Node clone);

        Assert.True(success);
        Assert.Null(clone);
    }

    [Fact]
    public void TryClone_Primitive_ShouldReturnSameValue()
    {
        int source = 42;

        bool success = source.TryCloneDeep(out int clone);

        Assert.True(success);
        Assert.Equal(42, clone);
    }

    [Fact]
    public void TryClone_PrimitivesAndImmutableTypes_ShouldCloneByValue()
    {
        bool boolSource = true;
        decimal decimalSource = 123.456m;
        DateTime dateSource = new DateTime(2024, 5, 1, 10, 30, 0, DateTimeKind.Utc);
        Guid guidSource = Guid.NewGuid();
        string stringSource = "clone-me";

        Assert.True(boolSource.TryCloneDeep(out bool boolClone));
        Assert.True(decimalSource.TryCloneDeep(out decimal decimalClone));
        Assert.True(dateSource.TryCloneDeep(out DateTime dateClone));
        Assert.True(guidSource.TryCloneDeep(out Guid guidClone));
        Assert.True(stringSource.TryCloneDeep(out string stringClone));

        Assert.Equal(boolSource, boolClone);
        Assert.Equal(decimalSource, decimalClone);
        Assert.Equal(dateSource, dateClone);
        Assert.Equal(guidSource, guidClone);
        Assert.Equal(stringSource, stringClone);
    }

    [Fact]
    public void TryClone_StructWithoutReferences_ShouldCloneByValue()
    {
        var source = new Coordinates { X = 10, Y = 20 };

        bool success = source.TryCloneDeep(out Coordinates clone);

        Assert.True(success);
        Assert.Equal(10, clone.X);
        Assert.Equal(20, clone.Y);
    }

    [Fact]
    public void TryClone_StructWithReferenceFields_ShouldDeepCloneReferences()
    {
        var source = new StructWithRefs
        {
            Id = 7,
            Anchor = new Node
            {
                Value = 100,
                Next = new Node { Value = 200 }
            },
            Values = new List<int> { 1, 2, 3 }
        };

        bool success = source.TryCloneDeep(out StructWithRefs clone);

        Assert.True(success);
        Assert.Equal(source.Id, clone.Id);

        Assert.NotNull(clone.Anchor);
        Assert.NotSame(source.Anchor, clone.Anchor);
        Assert.Equal(100, clone.Anchor.Value);

        Assert.NotNull(clone.Anchor.Next);
        Assert.NotSame(source.Anchor.Next, clone.Anchor.Next);
        Assert.Equal(200, clone.Anchor.Next.Value);

        Assert.NotNull(clone.Values);
        Assert.NotSame(source.Values, clone.Values);
        Assert.Equal(new[] { 1, 2, 3 }, clone.Values);
    }

    [Fact]
    public void TryClone_NestedObject_ShouldDeepClone()
    {
        var source = new Node
        {
            Value = 1,
            Next = new Node { Value = 2 }
        };

        bool success = source.TryCloneDeep(out Node clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(source.Value, clone.Value);

        Assert.NotNull(clone.Next);
        Assert.NotSame(source.Next, clone.Next);
        Assert.Equal(source.Next.Value, clone.Next.Value);
    }

    [Fact]
    public void TryClone_CyclicReference_ShouldPreserveCycle()
    {
        var source = new Node { Value = 7 };
        source.Next = source;

        bool success = source.TryCloneDeep(out Node clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(7, clone.Value);
        Assert.Same(clone, clone.Next);
    }

    [Fact]
    public void TryClone_SharedReference_ShouldStaySharedInClone()
    {
        var shared = new Node { Value = 99 };
        var source = new Pair
        {
            First = shared,
            Second = shared
        };

        bool success = source.TryCloneDeep(out Pair clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone.First);
        Assert.NotNull(clone.Second);
        Assert.Same(clone.First, clone.Second);
        Assert.NotSame(source.First, clone.First);
    }

    [Fact]
    public void TryClone_ListAndDictionary_ShouldDeepCloneCollectionsAndElements()
    {
        var n1 = new Node { Value = 1 };
        var n2 = new Node { Value = 2 };

        var source = new ComplexContainer
        {
            Name = "container",
            Items = new List<Node> { n1, n2 },
            Map = new Dictionary<int, Node>
            {
                { 10, n1 },
                { 20, n2 }
            }
        };

        bool success = source.TryCloneDeep(out ComplexContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.Equal("container", clone.Name);

        Assert.NotNull(clone.Items);
        Assert.Equal(2, clone.Items.Count);
        Assert.NotSame(source.Items, clone.Items);
        Assert.NotSame(source.Items[0], clone.Items[0]);
        Assert.Equal(1, clone.Items[0].Value);
        Assert.Equal(2, clone.Items[1].Value);

        Assert.NotNull(clone.Map);
        Assert.Equal(2, clone.Map.Count);
        Assert.NotSame(source.Map, clone.Map);
        Assert.NotSame(source.Map[10], clone.Map[10]);
        Assert.Equal(1, clone.Map[10].Value);
        Assert.Equal(2, clone.Map[20].Value);
    }

    [Fact]
    public void TryClone_DeeplyNestedMixedTypes_ShouldDeepCloneAllLevels()
    {
        var sharedNode = new Node
        {
            Value = 500,
            Next = new Node { Value = 501 }
        };

        var source = new DeepNestedContainer
        {
            Name = "deep",
            Groups = new Dictionary<string, List<Metrics>>
            {
                {
                    "g1",
                    new List<Metrics>
                    {
                        new Metrics { Count = 1, Ratio = 0.1m, Anchor = sharedNode },
                        new Metrics { Count = 2, Ratio = 0.2m, Anchor = sharedNode }
                    }
                },
                {
                    "g2",
                    new List<Metrics>
                    {
                        new Metrics { Count = 3, Ratio = 1.5m, Anchor = new Node { Value = 700 } }
                    }
                }
            },
            LookupLayers = new List<Dictionary<int, string[]>>
            {
                new Dictionary<int, string[]>
                {
                    { 1, new[] { "a", "b" } },
                    { 2, new[] { "x", "y", "z" } }
                }
            }
        };

        bool success = source.TryCloneDeep(out DeepNestedContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal("deep", clone.Name);

        Assert.NotNull(clone.Groups);
        Assert.NotSame(source.Groups, clone.Groups);

        var sourceG1 = source.Groups["g1"];
        var cloneG1 = clone.Groups["g1"];
        Assert.NotSame(sourceG1, cloneG1);
        Assert.Equal(2, cloneG1.Count);

        var cloneMetric0 = cloneG1[0];
        var cloneMetric1 = cloneG1[1];

        Assert.Equal(1, cloneMetric0.Count);
        Assert.Equal(0.1m, cloneMetric0.Ratio);
        Assert.Equal(2, cloneMetric1.Count);
        Assert.Equal(0.2m, cloneMetric1.Ratio);

        Assert.NotNull(cloneMetric0.Anchor);
        Assert.NotSame(sourceG1[0].Anchor, cloneMetric0.Anchor);
        Assert.Same(cloneMetric0.Anchor, cloneMetric1.Anchor); // shared ref preserved
        Assert.NotNull(cloneMetric0.Anchor.Next);
        Assert.NotSame(sourceG1[0].Anchor.Next, cloneMetric0.Anchor.Next);

        Assert.NotNull(clone.LookupLayers);
        Assert.NotSame(source.LookupLayers, clone.LookupLayers);
        Assert.NotSame(source.LookupLayers[0], clone.LookupLayers[0]);
        Assert.NotSame(source.LookupLayers[0][1], clone.LookupLayers[0][1]);
        Assert.Equal(new[] { "a", "b" }, clone.LookupLayers[0][1]);
        Assert.Equal(new[] { "x", "y", "z" }, clone.LookupLayers[0][2]);
    }

    [Fact]
    public void TryClone_MultiDimensionalArray_ShouldCloneElements()
    {
        var source = new Node[2, 2];
        source[0, 0] = new Node { Value = 1 };
        source[0, 1] = new Node { Value = 2 };
        source[1, 0] = new Node { Value = 3 };
        source[1, 1] = new Node { Value = 4 };

        bool success = source.TryCloneDeep(out Node[,] clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(2, clone.GetLength(0));
        Assert.Equal(2, clone.GetLength(1));

        Assert.Equal(1, clone[0, 0].Value);
        Assert.Equal(2, clone[0, 1].Value);
        Assert.Equal(3, clone[1, 0].Value);
        Assert.Equal(4, clone[1, 1].Value);

        Assert.NotSame(source[0, 0], clone[0, 0]);
        Assert.NotSame(source[1, 1], clone[1, 1]);
    }

    [Fact]
    public void TryClone_InterfaceTypedObject_ShouldUseRuntimeTypeAndSucceed()
    {
        INodeOwner source = new NodeOwner
        {
            Node = new Node
            {
                Value = 10,
                Next = new Node { Value = 11 }
            }
        };

        bool success = source.TryCloneDeep(out INodeOwner clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.IsType<NodeOwner>(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone.Node);
        Assert.NotSame(source.Node, clone.Node);
        Assert.Equal(10, clone.Node.Value);
        Assert.NotNull(clone.Node.Next);
        Assert.NotSame(source.Node.Next, clone.Node.Next);
        Assert.Equal(11, clone.Node.Next.Value);
    }

    [Fact]
    public void TryClone_AbstractTypedObject_ShouldUseRuntimeTypeAndSucceed()
    {
        AbstractOwner source = new ConcreteOwner
        {
            Id = 123,
            Marker = new Node { Value = 77 }
        };

        bool success = source.TryCloneDeep(out AbstractOwner clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.IsType<ConcreteOwner>(clone);
        Assert.NotSame(source, clone);

        var concreteClone = (ConcreteOwner)clone;
        Assert.Equal(123, concreteClone.Id);
        Assert.NotNull(concreteClone.Marker);
        Assert.Equal(77, concreteClone.Marker.Value);
        Assert.NotSame(source.Marker, concreteClone.Marker);
    }

    [Fact]
    public void TryClone_EnumerableWithoutAdd_ShouldCloneUsingFieldState()
    {
        var source = new EnumerableWithoutAdd(1, 2, 3);

        bool success = source.TryCloneDeep(out EnumerableWithoutAdd clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(new[] { 1, 2, 3 }, clone.ToList());
    }

    [Fact]
    public void TryClone_NestedEnumerableWithoutAdd_ShouldCloneUsingFieldState()
    {
        var source = new ContainerWithEnumerableWithoutAdd
        {
            Data = new EnumerableWithoutAdd(4, 5, 6)
        };

        bool success = source.TryCloneDeep(out ContainerWithEnumerableWithoutAdd clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.NotNull(clone.Data);
        Assert.NotSame(source.Data, clone.Data);
        Assert.Equal(new[] { 4, 5, 6 }, clone.Data.ToList());
    }

    [Fact]
    public void TryClone_IEnumerableWithThrowingAdd_ShouldCloneUsingFieldState()
    {
        var source = new ThrowingAddEnumerable(1, 2, 3);

        bool success = source.TryCloneDeep(out ThrowingAddEnumerable clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(new[] { 1, 2, 3 }, clone.ToList());
    }

    [Fact]
    public void TryClone_IListWithThrowingAdd_ShouldThrow()
    {
        var source = new ThrowingAddList(1, 2, 3);

        Assert.Throws<InvalidOperationException>(() =>
        {
            source.TryCloneDeep(out ThrowingAddList _);
        });
    }

    [Fact]
    public void TryClone_IDictionaryWithThrowingAdd_ShouldThrow()
    {
        var source = new ThrowingAddDictionary(
            ("a", 1),
            ("b", 2));

        Assert.Throws<InvalidOperationException>(() =>
        {
            source.TryCloneDeep(out ThrowingAddDictionary _);
        });
    }

    [Fact]
    public void TryClone_ReadonlyFields_ShouldBeCloned()
    {
        var source = new ReadonlyFieldContainer(
            new Node
            {
                Value = 77,
                Next = new Node { Value = 88 }
            },
            new List<int> { 9, 8, 7 });

        bool success = source.TryCloneDeep(out ReadonlyFieldContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone.Node);
        Assert.NotSame(source.Node, clone.Node);
        Assert.Equal(77, clone.Node.Value);
        Assert.NotNull(clone.Node.Next);
        Assert.NotSame(source.Node.Next, clone.Node.Next);
        Assert.Equal(88, clone.Node.Next.Value);

        Assert.NotNull(clone.Values);
        Assert.NotSame(source.Values, clone.Values);
        Assert.Equal(new[] { 9, 8, 7 }, clone.Values);
    }

    [Fact]
    public void TryClone_NonZeroLowerBoundArray_ShouldCloneBoundsAndElements()
    {
        Array source = Array.CreateInstance(typeof(Node), new[] { 2, 2 }, new[] { 1, -2 });

        var shared = new Node { Value = 42 };
        source.SetValue(shared, 1, -2);
        source.SetValue(null, 1, -1);
        source.SetValue(new Node { Value = 99 }, 2, -2);
        source.SetValue(shared, 2, -1);

        bool success = source.TryCloneDeep(out Array clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.Equal(1, clone.GetLowerBound(0));
        Assert.Equal(-2, clone.GetLowerBound(1));
        Assert.Equal(2, clone.GetLength(0));
        Assert.Equal(2, clone.GetLength(1));

        var cA = (Node)clone.GetValue(1, -2);
        var cB = (Node)clone.GetValue(2, -1);
        var cC = (Node)clone.GetValue(2, -2);

        Assert.NotNull(cA);
        Assert.NotNull(cB);
        Assert.NotNull(cC);

        Assert.Same(cA, cB);
        Assert.NotSame(shared, cA);
        Assert.Equal(42, cA.Value);
        Assert.Equal(99, cC.Value);
        Assert.Null(clone.GetValue(1, -1));
    }

    [Fact]
    public void TryClone_ObjectTypedRoot_ShouldUseRuntimeType()
    {
        object source = new Node
        {
            Value = 5,
            Next = new Node { Value = 6 }
        };

        bool success = source.TryCloneDeep(out object cloneObj);

        Assert.True(success);
        Assert.NotNull(cloneObj);
        Assert.IsType<Node>(cloneObj);
        Assert.NotSame(source, cloneObj);

        var clone = (Node)cloneObj;
        var src = (Node)source;

        Assert.Equal(5, clone.Value);
        Assert.NotNull(clone.Next);
        Assert.Equal(6, clone.Next.Value);
        Assert.NotSame(src.Next, clone.Next);
    }

    [Fact]
    public void TryClone_StructWithInterfaceOwner_ShouldCloneRuntimeTypeAndNestedReferences()
    {
        var source = new StructWithInterfaceOwner
        {
            Id = 12,
            Owner = new NodeOwner
            {
                Node = new Node
                {
                    Value = 200,
                    Next = new Node { Value = 201 }
                }
            }
        };

        bool success = source.TryCloneDeep(out StructWithInterfaceOwner clone);

        Assert.True(success);
        Assert.Equal(12, clone.Id);

        Assert.NotNull(clone.Owner);
        Assert.IsType<NodeOwner>(clone.Owner);
        Assert.NotSame(source.Owner, clone.Owner);

        Assert.NotNull(clone.Owner.Node);
        Assert.NotSame(source.Owner.Node, clone.Owner.Node);
        Assert.Equal(200, clone.Owner.Node.Value);

        Assert.NotNull(clone.Owner.Node.Next);
        Assert.NotSame(source.Owner.Node.Next, clone.Owner.Node.Next);
        Assert.Equal(201, clone.Owner.Node.Next.Value);
    }

    [Fact]
    public void TryClone_ReadonlyStructFields_ShouldBeCloned()
    {
        var source = new ReadonlyStructContainer(
            new Node
            {
                Value = 700,
                Next = new Node { Value = 701 }
            },
            new List<int> { 3, 4, 5 });

        bool success = source.TryCloneDeep(out ReadonlyStructContainer clone);

        Assert.True(success);

        Assert.NotNull(clone.Node);
        Assert.NotSame(source.Node, clone.Node);
        Assert.Equal(700, clone.Node.Value);

        Assert.NotNull(clone.Node.Next);
        Assert.NotSame(source.Node.Next, clone.Node.Next);
        Assert.Equal(701, clone.Node.Next.Value);

        Assert.NotNull(clone.Values);
        Assert.NotSame(source.Values, clone.Values);
        Assert.Equal(new[] { 3, 4, 5 }, clone.Values);
    }

    [Fact]
    public void TryClone_TypeWithoutParameterlessConstructor_ShouldCloneUsingUninitializedObjectFallback()
    {
        var source = new OnlyParameterizedCtorContainer(
            123,
            new Node
            {
                Value = 11,
                Next = new Node { Value = 12 }
            });

        bool success = source.TryCloneDeep(out OnlyParameterizedCtorContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.Equal(123, clone.Id);

        Assert.NotNull(clone.Node);
        Assert.NotSame(source.Node, clone.Node);
        Assert.Equal(11, clone.Node.Value);

        Assert.NotNull(clone.Node.Next);
        Assert.NotSame(source.Node.Next, clone.Node.Next);
        Assert.Equal(12, clone.Node.Next.Value);
    }

    [Fact]
    public void TryClone_SelfReferencingArray_ShouldPreserveCycle()
    {
        object[] source = new object[2];
        source[0] = source;
        source[1] = new Node { Value = 9 };

        bool success = source.TryCloneDeep(out object[] clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.Same(clone, clone[0]);

        Assert.IsType<Node>(clone[1]);
        var clonedNode = (Node)clone[1];
        Assert.Equal(9, clonedNode.Value);
        Assert.NotSame(source[1], clonedNode);
    }

    [Fact]
    public void TryClone_DictionaryWithSelfReferenceAndSharedValues_ShouldPreserveGraphShape()
    {
        var shared = new Node { Value = 50 };
        var source = new Dictionary<object, object>
        {
            ["self"] = null,
            ["a"] = shared,
            ["b"] = shared,
            ["nullValue"] = null
        };
        source["self"] = source;

        bool success = source.TryCloneDeep(out Dictionary<object, object> clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.Same(clone, clone["self"]);
        Assert.Null(clone["nullValue"]);

        var a = Assert.IsType<Node>(clone["a"]);
        var b = Assert.IsType<Node>(clone["b"]);

        Assert.Same(a, b);
        Assert.NotSame(shared, a);
        Assert.Equal(50, a.Value);
    }

    [Fact]
    public void TryClone_ObjectTypedFields_ShouldUseRuntimeTypesInsideGraph()
    {
        var source = new ObjectPayloadContainer
        {
            Payload = new Node
            {
                Value = 33,
                Next = new Node { Value = 34 }
            },
            Items = new object[]
            {
                new Node { Value = 40 },
                123,
                "abc"
            }
        };

        bool success = source.TryCloneDeep(out ObjectPayloadContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        var sourcePayload = Assert.IsType<Node>(source.Payload);
        var clonePayload = Assert.IsType<Node>(clone.Payload);

        Assert.NotSame(sourcePayload, clonePayload);
        Assert.Equal(33, clonePayload.Value);
        Assert.NotNull(clonePayload.Next);
        Assert.Equal(34, clonePayload.Next.Value);
        Assert.NotSame(sourcePayload.Next, clonePayload.Next);

        Assert.NotNull(clone.Items);
        Assert.NotSame(source.Items, clone.Items);
        Assert.Equal(3, clone.Items.Length);

        var clonedItemNode = Assert.IsType<Node>(clone.Items[0]);
        Assert.Equal(40, clonedItemNode.Value);
        Assert.NotSame(source.Items[0], clone.Items[0]);

        Assert.Equal(123, clone.Items[1]);
        Assert.Equal("abc", clone.Items[2]);
    }

    [Fact]
    public void TryClone_JaggedArrayWithSharedInnerArray_ShouldPreserveSharing()
    {
        var sharedInner = new[]
        {
            new Node { Value = 1 },
            new Node { Value = 2 }
        };

        Node[][] source =
        {
            sharedInner,
            sharedInner
        };

        bool success = source.TryCloneDeep(out Node[][] clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(2, clone.Length);

        Assert.NotNull(clone[0]);
        Assert.Same(clone[0], clone[1]);
        Assert.NotSame(sharedInner, clone[0]);

        Assert.Equal(1, clone[0][0].Value);
        Assert.Equal(2, clone[0][1].Value);
        Assert.NotSame(sharedInner[0], clone[0][0]);
        Assert.NotSame(sharedInner[1], clone[0][1]);
    }

    [Fact]
    public void TryClone_ListWithSelfReference_ShouldPreserveCycle()
    {
        var source = new List<object>();
        source.Add(source);
        source.Add(new Node { Value = 88 });

        bool success = source.TryCloneDeep(out List<object> clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal(2, clone.Count);

        Assert.Same(clone, clone[0]);

        var cloneNode = Assert.IsType<Node>(clone[1]);
        Assert.Equal(88, cloneNode.Value);
        Assert.NotSame(source[1], cloneNode);
    }

    [Fact]
    public void TryClone_DelegateRoot_ShouldReturnFalse()
    {
        Action source = static () => { };

        bool success = source.TryCloneDeep(out Action clone);

        Assert.False(success);
        Assert.Null(clone);
    }

    [Fact]
    public void TryClone_ReadonlyFieldsInInheritance_ShouldBeCloned()
    {
        var source = new DerivedReadonlyOwner(
            new Node
            {
                Value = 300,
                Next = new Node { Value = 301 }
            },
            new List<int> { 7, 8, 9 },
            new Node { Value = 400 });

        bool success = source.TryCloneDeep(out DerivedReadonlyOwner clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone.BaseNode);
        Assert.NotSame(source.BaseNode, clone.BaseNode);
        Assert.Equal(300, clone.BaseNode.Value);

        Assert.NotNull(clone.BaseNode.Next);
        Assert.NotSame(source.BaseNode.Next, clone.BaseNode.Next);
        Assert.Equal(301, clone.BaseNode.Next.Value);

        Assert.NotNull(clone.Numbers);
        Assert.NotSame(source.Numbers, clone.Numbers);
        Assert.Equal(new[] { 7, 8, 9 }, clone.Numbers);

        Assert.NotNull(clone.Tail);
        Assert.NotSame(source.Tail, clone.Tail);
        Assert.Equal(400, clone.Tail.Value);
    }

    [Fact]
    public async void TryClone_ConcurrentCalls_ShouldProduceIndependentValidClones()
    {
        var shared = new Node
        {
            Value = 10,
            Next = new Node { Value = 11 }
        };

        var source = new Pair
        {
            First = shared,
            Second = shared
        };

        Task[] tasks = Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            bool success = source.TryCloneDeep(out Pair clone);

            Assert.True(success);
            Assert.NotNull(clone);
            Assert.NotSame(source, clone);

            Assert.NotNull(clone.First);
            Assert.NotNull(clone.Second);
            Assert.Same(clone.First, clone.Second);
            Assert.NotSame(source.First, clone.First);

            Assert.Equal(10, clone.First.Value);
            Assert.NotNull(clone.First.Next);
            Assert.Equal(11, clone.First.Next.Value);
            Assert.NotSame(source.First.Next, clone.First.Next);
        })).ToArray<Task>();

        await Task.WhenAll(tasks);
    }

    private sealed class TypeFieldContainer
    {
        public Type DeclaredType;
    }

    [Fact]
    public void TryClone_TypeField_ShouldSucceedAndKeepSameTypeReference()
    {
        var source = new TypeFieldContainer
        {
            DeclaredType = typeof(Dictionary<string, int>)
        };

        bool success = source.TryCloneDeep(out TypeFieldContainer clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone.DeclaredType);
        Assert.Same(source.DeclaredType, clone.DeclaredType);
    }


    [Fact]
    public void TryClone_TypeDictValue_ShouldSucceedAndKeepSameTypeReference()
    {
       
        Dictionary<Type, Type[]> source = new Dictionary<Type, Type[]>
        {
            [typeof(string)] = new Type[] { typeof(string) }
        };

        bool success = source.TryCloneDeep(out Dictionary<Type, Type[]> clone);

        Assert.True(success);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);

        Assert.NotNull(clone[typeof(string)]);
        Assert.Equal(source[typeof(string)], clone[typeof(string)]);
    }
}