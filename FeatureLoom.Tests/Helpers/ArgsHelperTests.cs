using FeatureLoom.Helpers;
using Xunit;
using System.Linq;

namespace FeatureLoom.Helpers;

public class ArgsHelperTests
{
    [Fact]
    public void Parses_Named_And_Valueless_Arguments()
    {
        var args = new[] { "-name=Teddy", "-friends", "Jim", "Carl" };
        var helper = new ArgsHelper(args);

        Assert.Equal("Teddy", helper.GetFirstByKey("name"));
        Assert.True(helper.HasKey("friends"));
        var friends = helper.GetAllAfterKey("friends").ToArray();
        Assert.Equal(new[] { "Jim", "Carl" }, friends);
    }

    [Fact]
    public void Parses_Named_And_Valueless_Arguments_CaseInsensitive()
    {
        var args = new[] { "-Name=Teddy", "-FRIENDS", "Jim", "Carl" };
        var helper = new ArgsHelper(args);

        Assert.Equal("Teddy", helper.GetFirstByKey("NAME"));
        Assert.True(helper.HasKey("friends"));
        var friends = helper.GetAllAfterKey("FrIeNdS").ToArray();
        Assert.Equal(new[] { "Jim", "Carl" }, friends);
    }

    [Fact]
    public void Parses_Arguments_From_String_With_Quotes()
    {
        var helper = new ArgsHelper("-name=\"Teddy Bear\" -desc='A \"quoted\" value' plainValue");

        Assert.Equal("Teddy Bear", helper.GetFirstByKey("name"));
        Assert.Equal("A \"quoted\" value", helper.GetFirstByKey("desc"));
        Assert.Equal("plainValue", helper.GetByIndex(2));
    }

    [Fact]
    public void Parses_Arguments_From_String_With_Quotes_CaseInsensitive()
    {
        var helper = new ArgsHelper("-NAME=\"Teddy Bear\" -DESC='A \"quoted\" value' plainValue");

        Assert.Equal("Teddy Bear", helper.GetFirstByKey("name"));
        Assert.Equal("A \"quoted\" value", helper.GetFirstByKey("DESC"));
        Assert.Equal("plainValue", helper.GetByIndex(2));
    }

    [Fact]
    public void Returns_Null_For_Missing_Key()
    {
        var helper = new ArgsHelper(new[] { "-foo=bar" });
        Assert.Null(helper.GetFirstByKey("missing"));
    }

    [Fact]
    public void Returns_Null_For_Missing_Key_CaseInsensitive()
    {
        var helper = new ArgsHelper(new[] { "-FOO=bar" });
        Assert.Null(helper.GetFirstByKey("missing"));
    }

    [Fact]
    public void Can_Get_By_Index_And_Convert_Type()
    {
        var helper = new ArgsHelper(new[] { "-NUM=42", "true" });
        Assert.True(helper.TryGetByIndex<int>(0, out var num));
        Assert.Equal(42, num);
        Assert.True(helper.TryGetByIndex<bool>(1, out var flag));
        Assert.True(flag);
    }

    [Fact]
    public void Can_Get_All_By_Key()
    {
        var helper = new ArgsHelper(new[] { "-tag=one", "-tag=two", "-tag=three" });
        var tags = helper.GetAllByKey("tag").ToArray();
        Assert.Equal(new[] { "one", "two", "three" }, tags);
    }

    [Fact]
    public void Can_Get_All_By_Key_CaseInsensitive()
    {
        var helper = new ArgsHelper(new[] { "-TAG=one", "-tag=two", "-Tag=three" });
        var tags = helper.GetAllByKey("tAg").ToArray();
        Assert.Equal(new[] { "one", "two", "three" }, tags);
    }

    [Fact]
    public void Can_Find_Index_By_Key()
    {
        var helper = new ArgsHelper(new[] { "-a=1", "-b=2", "-c=3" });
        Assert.Equal(1, helper.FindIndexByKey("b"));
        Assert.Equal(-1, helper.FindIndexByKey("x"));
    }

    [Fact]
    public void Can_Find_Index_By_Key_CaseInsensitive()
    {
        var helper = new ArgsHelper(new[] { "-A=1", "-B=2", "-C=3" });
        Assert.Equal(1, helper.FindIndexByKey("b"));
        Assert.Equal(-1, helper.FindIndexByKey("x"));
    }
}