using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class BoxTests
{
    [Fact]
    public void Box_Stores_And_Retrieves_Value()
    {
        var box = new Box<int>(42);
        Assert.Equal(42, box.value);
        Assert.Equal(42, box.GetValue<int>());
    }

    [Fact]
    public void Box_Implicit_Conversion_Works()
    {
        Box<string> box = "hello";
        Assert.Equal("hello", box.value);
    }

    [Fact]
    public void Box_Clear_Sets_Value_To_Default()
    {
        var box = new Box<double>(3.14);
        box.Clear();
        Assert.Equal(0.0, box.value);
    }

    [Fact]
    public void Box_Equals_And_Operators_Work_With_Box()
    {
        var box1 = new Box<int>(5);
        var box2 = new Box<int>(5);
        var box3 = new Box<int>(7);

        Assert.True(box1.Equals(box2));
        Assert.True(box1 == box2);
        Assert.False(box1 != box2);
        Assert.False(box1 == box3);
        Assert.True(box1 != box3);
    }

    [Fact]
    public void Box_Equals_And_Operators_Work_With_Value()
    {
        var box = new Box<int>(10);

        Assert.True(box.Equals(10));
        Assert.True(box == 10);
        Assert.True(10 == box);
        Assert.False(box != 10);
        Assert.False(10 != box);
        Assert.False(box == 5);
        Assert.True(box != 5);
    }

    [Fact]
    public void Box_Handles_Null_Correctly()
    {
        Box<string> box = null;
        Box<string> box2 = new Box<string>(null);

        Assert.True(box == null);
        Assert.True(null == box);
        Assert.False(box != null);
        Assert.False(null != box);

        Assert.True(box2 == (string)null);
        Assert.True((string)null == box2);
        Assert.False(box2 != (string)null);
        Assert.False((string)null != box2);
    }

    [Fact]
    public void Box_ToString_And_HashCode_Work()
    {
        var box = new Box<int>(123);
        Assert.Equal("123", box.ToString());
        Assert.Equal(123.GetHashCode(), box.GetHashCode());

        var boxNull = new Box<string>(null);
        Assert.Equal(string.Empty, boxNull.ToString());
        Assert.Equal(0, boxNull.GetHashCode());
    }

    [Fact]
    public void Box_GetValue_Throws_On_Wrong_Type()
    {
        var box = new Box<int>(42);
        Assert.Throws<System.Exception>(() => box.GetValue<string>());
    }

    [Fact]
    public void Box_SetValue_Throws_On_Wrong_Type()
    {
        var box = new Box<int>();
        Assert.Throws<System.Exception>(() => box.SetValue("not an int"));
    }
}