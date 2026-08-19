using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Verifies that settings configured on a single member actually change how that member's value is
/// written, and that such an override stays local: it must not leak into the shared writer used for
/// the same type elsewhere.
/// </summary>
public class JsonSerializerMemberSettingsTests
{
    public enum Color { Red, Green, Blue }

    public class EnumHolder
    {
        public Color Plain;
        public Color Overridden;
    }

    public class BytesHolder
    {
        public byte[] Plain;
        public byte[] Overridden;
    }

    static JsonSerializer CreateSerializer(System.Action<JsonSerializer.Settings> configure)
    {
        var settings = new JsonSerializer.Settings
        {
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = JsonSerializer.ReferenceCheck.NoRefCheck
        };
        configure(settings);
        return new JsonSerializer(settings);
    }

    [Fact]
    public void Serialize_MemberSettings_EnumAsString_AppliesOnlyToConfiguredMember()
    {
        var serializer = CreateSerializer(s =>
        {
            s.enumAsString = false;
            s.ConfigureType<EnumHolder>(ts =>
                ts.ConfigureMember<Color>(nameof(EnumHolder.Overridden), ms => ms.SetEnumAsString(true)));
        });

        string json = serializer.Serialize(new EnumHolder { Plain = Color.Green, Overridden = Color.Blue });

        Assert.Contains("\"Plain\":1", json);
        Assert.Contains("\"Overridden\":\"Blue\"", json);
    }

    [Fact]
    public void Serialize_MemberSettings_EnumAsNumber_AppliesOnlyToConfiguredMember()
    {
        var serializer = CreateSerializer(s =>
        {
            s.enumAsString = true;
            s.ConfigureType<EnumHolder>(ts =>
                ts.ConfigureMember<Color>(nameof(EnumHolder.Overridden), ms => ms.SetEnumAsString(false)));
        });

        string json = serializer.Serialize(new EnumHolder { Plain = Color.Green, Overridden = Color.Blue });

        Assert.Contains("\"Plain\":\"Green\"", json);
        Assert.Contains("\"Overridden\":2", json);
    }

    [Fact]
    public void Serialize_MemberSettings_ByteArrayBase64_AppliesOnlyToConfiguredMember()
    {
        var serializer = CreateSerializer(s =>
        {
            s.writeByteArrayAsBase64String = false;
            s.ConfigureType<BytesHolder>(ts =>
                ts.ConfigureMember<byte[]>(nameof(BytesHolder.Overridden), ms => ms.SetWriteByteArrayAsBase64String(true)));
        });

        string json = serializer.Serialize(new BytesHolder
        {
            Plain = new byte[] { 1, 2, 3 },
            Overridden = new byte[] { 1, 2, 3 }
        });

        Assert.Contains("\"Plain\":[1,2,3]", json);
        Assert.Contains("\"Overridden\":\"AQID\"", json);
    }

    [Fact]
    public void Serialize_MemberSettings_DoNotLeakIntoSharedWriterForSameType()
    {
        // The overridden member is written first, so if the override leaked into the shared writer
        // for Color, the later plain member and the root value would be affected too.
        var serializer = CreateSerializer(s =>
        {
            s.enumAsString = false;
            s.ConfigureType<EnumHolder>(ts =>
                ts.ConfigureMember<Color>(nameof(EnumHolder.Overridden), ms => ms.SetEnumAsString(true)));
        });

        string holderJson = serializer.Serialize(new EnumHolder { Plain = Color.Green, Overridden = Color.Blue });
        string rootJson = serializer.Serialize(Color.Blue);

        Assert.Contains("\"Overridden\":\"Blue\"", holderJson);
        Assert.Contains("\"Plain\":1", holderJson);
        Assert.Equal("2", rootJson);
    }

    [Fact]
    public void Serialize_MemberSettings_OverrideNameOnly_StillUsesSharedWriter()
    {
        // Pure member metadata must not create a member-local writer, and must not change the value
        // representation of the member.
        var serializer = CreateSerializer(s =>
        {
            s.enumAsString = false;
            s.ConfigureType<EnumHolder>(ts =>
                ts.ConfigureMember<Color>(nameof(EnumHolder.Overridden), ms => ms.OverrideName("renamed")));
        });

        string json = serializer.Serialize(new EnumHolder { Plain = Color.Green, Overridden = Color.Blue });

        Assert.Contains("\"renamed\":2", json);
        Assert.Contains("\"Plain\":1", json);
    }
}
