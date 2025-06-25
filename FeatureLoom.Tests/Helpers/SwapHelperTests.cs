using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class SwapHelperTests
{
    [Fact]
    public void Swap_SwapsIntValues()
    {
        int a = 1, b = 2;
        SwapHelper.Swap(ref a, ref b);
        Assert.Equal(2, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void Swap_SwapsStringValues()
    {
        string x = "foo", y = "bar";
        SwapHelper.Swap(ref x, ref y);
        Assert.Equal("bar", x);
        Assert.Equal("foo", y);
    }

    [Fact]
    public void Swap_SwapsReferenceTypes()
    {
        var obj1 = new TestClass { Value = 10 };
        var obj2 = new TestClass { Value = 20 };
        SwapHelper.Swap(ref obj1, ref obj2);
        Assert.Equal(20, obj1.Value);
        Assert.Equal(10, obj2.Value);
    }

    private class TestClass
    {
        public int Value { get; set; }
    }
}