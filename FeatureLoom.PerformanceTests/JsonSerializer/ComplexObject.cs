using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.PerformanceTests.JsonSerializer;


/// <summary>
/// A deliberately balanced object covering all commonly used field types, so no single
/// type dominates the measured serialization cost. It is used to compare the overall
/// throughput of the serializers on a realistic payload.
///
/// The field mix approximates typical payload data rather than worst cases:
/// strings form the largest group (as in most real JSON), only one of them requires
/// escaping, numeric values use ordinary magnitudes instead of type limits, and date
/// and time types are not over-represented. Edge cases (maximum digit counts, full
/// 17-digit doubles, heavy escaping) are covered by the dedicated per-type benchmarks,
/// where they can be measured without distorting this overall throughput comparison.
/// </summary>
public class ComplexObject
{    
    public int id = 0;

    // Text: strings dominate typical JSON payloads, and only a minority of them
    // contain characters that need escaping.
    public string myString = "This is a string";
    public string myName = "Jane Doe";
    public string myCode = "ORD-2024-000123";
    public string myDescription = "A short description of the item";
    public string myEscapedString = "Line1\r\nLine2\t\"quoted\"";

    // Integers: ordinary magnitudes, not type limits.
    public int myInt = -42;
    public int? myNullableInt = null;
    public long myLong = 1234567890L;
    public uint myUint = 65432u;

    // Floating point
    public float myFloat = 123.456f;
    // A short decimal, representative of typical payload data. Values requiring the full
    // 17 significant digits are deliberately not used here: they always take the
    // round-trip fallback path and would dominate this benchmark. That edge case is
    // covered per magnitude by SerializeDoubleValuesTest instead.
    public double myDouble = -0.00015890432;
    public decimal myDecimal = 12345.6789m;

    // Misc value types
    public bool myBool = true;
    public MyEnum myEnum = MyEnum.Val5;
    public Guid myGuid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    // Date and time: two representative fields are enough. Fractional seconds and
    // offset handling are covered by the dedicated date/time benchmarks.
    public DateTime myDateTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    public TimeSpan myTimeSpan = new TimeSpan(2, 3, 4, 5, 6);

    // Containers
    public IList<int> myIntList = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };    
    public byte[] myByteArray = new byte[] { 123, 0, 255, 42, 99, 100, 101, 102, 103, 104 };
    public string[] myStringArray = new string[] { "alpha", "beta", "gamma" };
    public Dictionary<string, string> myDictionary = new Dictionary<string, string>
    {
        { "first", "one" },
        { "second", "two" },
        { "third", "three" },
    };

    // Nested objects
    public EmbeddedStruct myEmbeddedStruct = new EmbeddedStruct("Another string", 99);
    public SimpleObject embeddedSimple = new SimpleObject() { id = 1, name = "one", value = 1.11 };
    public List<SimpleObject> embeddedSimpleList = new List<SimpleObject>
    {
        new SimpleObject() { id = 2, name = "two", value = 2.22 },
        new SimpleObject() { id = 3, name = "three", value = 3.33 },
    };

    public ComplexObject() { }

    public ComplexObject(int id)
    {
        this.id = id;
    }

    public struct EmbeddedStruct
    {
        public string embeddedString;
        public uint embeddedInt;

        public EmbeddedStruct(string embeddedString, uint embeddedInt)
        {
            this.embeddedString = embeddedString;
            this.embeddedInt = embeddedInt;
        }
    }

    public enum MyEnum
    {
        Val1, Val2, Val3, Val4, Val5, Val6,
    }
}
