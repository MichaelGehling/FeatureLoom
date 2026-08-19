using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Covers the propagation of reference-path writing from a member to its declaring type.
/// <para>
/// A parent type must write a ref path for itself as soon as any of its members can be the target
/// of a reference. Whether a member can be such a target depends on the settings the member's
/// reader was actually built with - which, for a member with its own settings, is the member
/// settings and not the settings of the shared reader for that member's type.
/// </para>
/// </summary>
public class JsonDeserializerMemberRefPathTests
{
    public class Referenced
    {
        public string Text;
    }

    public class Parent
    {
        public Referenced First;
        public Referenced Second;
    }

    public class Root
    {
        public Parent Parent;
        public Referenced Alias;
    }

    /// <summary>
    /// Reference resolution is off by default and enabled only for one member. The parent therefore
    /// only learns that it needs a ref path if the member settings are taken into account.
    /// </summary>
    [Fact]
    public void Deserialize_MemberEnablesReferenceResolution_ParentWritesRefPath()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.DisabledByDefault
        };
        settings.ConfigureType<Parent>(ts =>
            ts.ConfigureMember<Referenced>(nameof(Parent.First), ms => ms.SetReferenceResolution(true)));
        settings.ConfigureType<Root>(ts =>
            ts.ConfigureMember<Referenced>(nameof(Root.Alias), ms => ms.SetReferenceResolution(true)));

        var deserializer = new JsonDeserializer(settings);

        // "Alias" points back at the object that was read for Parent.First.
        const string json = "{\"Parent\":{\"First\":{\"Text\":\"shared\"},\"Second\":null},\"Alias\":{\"$ref\":\"$.Parent.First\"}}";

        Assert.True(deserializer.TryDeserialize(json, out Root root));
        Assert.NotNull(root.Parent?.First);
        Assert.Equal("shared", root.Parent.First.Text);
        Assert.Same(root.Parent.First, root.Alias);
    }
}
