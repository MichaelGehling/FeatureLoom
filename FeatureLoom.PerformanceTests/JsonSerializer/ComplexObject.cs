using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.PerformanceTests.JsonSerializer;


public class ComplexObject
{    
    public int id = 0;
    public string myString = "This is a string";
    public int myInt = -42;
    public float myFloat = 123.456f;
    public EmbeddedStruct myEmbeddedStruct = new EmbeddedStruct("Another string", 99);
    public List<byte> myBytesList = new List<byte> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    public MyEnum myEnum = MyEnum.Val5;

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

public class SimpleObject
{
    public int id = 0;
    public string name = "This is a string";
    public double value = 123.456;
}
