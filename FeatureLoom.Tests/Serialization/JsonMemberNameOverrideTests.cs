using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonMemberNameOverrideTests
{
    public class Leaf
    {
        public int a_b { get; set; }
        public int c_d;
    }

    public class Root
    {
        public int x_y;
        public string keep_me;
        public Leaf child_node;
    }

    static readonly Func<string, string> Slash = n => n.Replace('_', '/');

    static JsonSerializer Serializer(Action<JsonSerializer.Settings> configure)
    {
        var s = new JsonSerializer.Settings();
        configure(s);
        return new JsonSerializer(s);
    }

    static JsonDeserializer Deserializer(Action<JsonDeserializer.Settings> configure)
    {
        var s = new JsonDeserializer.Settings();
        configure(s);
        return new JsonDeserializer(s);
    }

    [Fact]
    public void Serializer_TypeRule_RenamesDirectMembersOnly_ExplicitNameWins()
    {
        var serializer = Serializer(s => s.ConfigureType<Root>(t =>
        {
            t.OverrideMemberNames(Slash);
            t.ConfigureMember<string>(nameof(Root.keep_me), m => m.OverrideName("kept"));
        }));

        string json = serializer.Serialize(new Root { x_y = 1, keep_me = "k", child_node = new Leaf { a_b = 2, c_d = 3 } });

        Assert.Contains("\"x/y\":1", json);
        Assert.Contains("\"kept\":\"k\"", json);
        Assert.Contains("\"child/node\":", json);
        Assert.Contains("\"a_b\":2", json);
        Assert.Contains("\"c_d\":3", json);
    }

    [Fact]
    public void Serializer_RecursiveRule_RenamesWholeSubtree()
    {
        var serializer = Serializer(s => s.ConfigureType<Root>(t => t.ConfigureRecursively(r => r.OverrideMemberNames(Slash))));

        string json = serializer.Serialize(new Root { x_y = 1, child_node = new Leaf { a_b = 2, c_d = 3 } });

        Assert.Contains("\"x/y\":1", json);
        Assert.Contains("\"child/node\":", json);
        Assert.Contains("\"a/b\":2", json);
        Assert.Contains("\"c/d\":3", json);
    }

    [Fact]
    public void Serializer_TypeRule_WinsOverRecursiveRule()
    {
        var serializer = Serializer(s =>
        {
            s.ConfigureType<Root>(t => t.ConfigureRecursively(r => r.OverrideMemberNames(Slash)));
            s.ConfigureType<Leaf>(t => t.OverrideMemberNames(n => n.ToUpperInvariant()));
        });

        string json = serializer.Serialize(new Root { child_node = new Leaf { a_b = 2 } });

        Assert.Contains("\"A_B\":2", json);
    }

    [Fact]
    public void Deserializer_TypeRule_RenamesDirectMembersOnly_ExplicitNameWins()
    {
        var deserializer = Deserializer(s => s.ConfigureType<Root>(t =>
        {
            t.OverrideMemberNames(Slash);
            t.ConfigureMember<string>(nameof(Root.keep_me), m => m.OverrideName("kept"));
        }));

        Assert.True(deserializer.TryDeserialize("{\"x/y\":1,\"kept\":\"k\",\"child/node\":{\"a_b\":2,\"c_d\":3}}", out Root root));
        Assert.Equal(1, root.x_y);
        Assert.Equal("k", root.keep_me);
        Assert.Equal(2, root.child_node.a_b);
        Assert.Equal(3, root.child_node.c_d);
    }

    [Fact]
    public void Deserializer_TypeRule_OriginalNameNoLongerMatches()
    {
        var deserializer = Deserializer(s => s.ConfigureType<Root>(t => t.OverrideMemberNames(Slash)));

        Assert.True(deserializer.TryDeserialize("{\"x_y\":1}", out Root root));
        Assert.Equal(0, root.x_y);
    }

    [Fact]
    public void Deserializer_RecursiveRule_RenamesWholeSubtree()
    {
        var deserializer = Deserializer(s => s.ConfigureType<Root>(t => t.ConfigureRecursively(r => r.OverrideMemberNames(Slash))));

        Assert.True(deserializer.TryDeserialize("{\"x/y\":1,\"child/node\":{\"a/b\":2,\"c/d\":3}}", out Root root));
        Assert.Equal(1, root.x_y);
        Assert.Equal(2, root.child_node.a_b);
        Assert.Equal(3, root.child_node.c_d);
    }

    [Fact]
    public void RoundTrip_WithRecursiveRule()
    {
        var serializer = Serializer(s => s.ConfigureType<Root>(t => t.ConfigureRecursively(r => r.OverrideMemberNames(Slash))));
        var deserializer = Deserializer(s => s.ConfigureType<Root>(t => t.ConfigureRecursively(r => r.OverrideMemberNames(Slash))));

        string json = serializer.Serialize(new Root { x_y = 5, keep_me = "v", child_node = new Leaf { a_b = 6, c_d = 7 } });
        Assert.True(deserializer.TryDeserialize(json, out Root root));
        Assert.Equal(5, root.x_y);
        Assert.Equal("v", root.keep_me);
        Assert.Equal(6, root.child_node.a_b);
        Assert.Equal(7, root.child_node.c_d);
    }
}
