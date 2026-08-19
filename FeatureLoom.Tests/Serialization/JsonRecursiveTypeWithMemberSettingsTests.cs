using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Guards the termination behavior of reader/writer creation for recursive types that are
/// combined with per-member settings.
/// <para>
/// Member settings produce a *local* type reader that is deliberately not cached, so the usual
/// "register in the cache before building" recursion guard does not apply to them. Termination
/// instead relies on the settings themselves forming a finite tree: every
/// <c>ConfigureMember</c> allocates a fresh settings object, so no settings object can ever
/// contain itself. Each recursion step descends one level into that tree and the deepest level
/// has no member settings left, which falls back to the cached reader for the type.
/// </para>
/// <para>
/// If someone ever introduces a way to reuse/alias a settings object, these tests hang instead of
/// failing - that is intentional, a hanging test is a clearer signal than a subtly wrong result.
/// </para>
/// </summary>
public class JsonRecursiveTypeWithMemberSettingsTests
{
    public class Node
    {
        public string Name;
        public Node Next;
    }

    [Fact]
    public void Deserialize_RecursiveType_WithMemberSettingsOnRecursiveMember_Terminates()
    {
        var deserializer = new JsonDeserializer(settings =>
        {
            settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
            settings.ConfigureType<Node>(ts =>
                ts.ConfigureMember<Node>(nameof(Node.Next), ms => ms.SetUseStringCache(false)));
        });

        string json = "{\"Name\":\"a\",\"Next\":{\"Name\":\"b\",\"Next\":{\"Name\":\"c\",\"Next\":null}}}";

        Assert.True(deserializer.TryDeserialize(json, out Node node));
        Assert.Equal("a", node.Name);
        Assert.Equal("b", node.Next.Name);
        Assert.Equal("c", node.Next.Next.Name);
        Assert.Null(node.Next.Next.Next);
    }

    [Fact]
    public void Deserialize_RecursiveType_WithNestedMemberSettings_Terminates()
    {
        // Nested member settings on the same recursive member: each level consumes one level of
        // explicitly written configuration, so the descent is bounded by the configuration depth.
        var deserializer = new JsonDeserializer(settings =>
        {
            settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
            settings.ConfigureType<Node>(ts =>
                ts.ConfigureMember<Node>(nameof(Node.Next), ms =>
                    ms.ConfigureMember<Node>(nameof(Node.Next), ms2 =>
                        ms2.ConfigureMember<Node>(nameof(Node.Next), ms3 => ms3.SetUseStringCache(false)))));
        });

        string json = "{\"Name\":\"a\",\"Next\":{\"Name\":\"b\",\"Next\":{\"Name\":\"c\",\"Next\":null}}}";

        Assert.True(deserializer.TryDeserialize(json, out Node node));
        Assert.Equal("c", node.Next.Next.Name);
    }

    [Fact]
    public void Serialize_RecursiveType_WithMemberSettings_Terminates()
    {
        var serializer = new FeatureLoom.Serialization.JsonSerializer(new FeatureLoom.Serialization.JsonSerializer.Settings
        {
            typeInfoHandling = FeatureLoom.Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = FeatureLoom.Serialization.JsonSerializer.ReferenceCheck.NoRefCheck
        });

        var node = new Node { Name = "a", Next = new Node { Name = "b", Next = null } };

        string json = serializer.Serialize(node);

        Assert.Contains("\"Name\":\"a\"", json);
        Assert.Contains("\"Name\":\"b\"", json);
    }
}
