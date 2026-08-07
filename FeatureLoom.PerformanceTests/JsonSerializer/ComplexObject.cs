using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.PerformanceTests.JsonSerializer;


/// <summary>
/// A deliberately balanced object covering all commonly used field types, so no single
/// type dominates the measured serialization cost. It is used to compare the overall
/// throughput of the serializers on a realistic payload.
/// </summary>
public class ComplexObject
{    
    public int id = 0;

    // Text
    public string myString = "This is a string";
    public string myEscapedString = "Line1\r\nLine2\t\"quoted\"";

    // Integers
    public int myInt = -42;
    public int? myNullableInt = null;
    public long myLong = 1234567890123456789L;
    public uint myUint = 4294967295u;

    // Floating point
    public float myFloat = 123.456f;
    public double myDouble = -0.00015890432405285535;
    public decimal myDecimal = 12345.6789m;

    // Misc value types
    public bool myBool = true;
    public MyEnum myEnum = MyEnum.Val5;
    public Guid myGuid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    public DateTime myDateTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    public DateTime myDateTimeWithFraction = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
    public DateTimeOffset myDateTimeOffset = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));
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
