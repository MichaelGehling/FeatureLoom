using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerSettingsInteractionMatrixTests
{
    public class Value
    {
        private int number;
        public int GetNumber() => number;
    }

    public class Holder
    {
        public List<Value> Scoped;
        public List<Value> Plain;
    }

    public class ProposedBase
    {
        public int Value;
    }

    public class ProposedA : ProposedBase
    {
        public int A;
    }

    public class ProposedB : ProposedBase
    {
        public int B;
    }

    public class Forbidden
    {
        public int Value;
    }

    public class SecurityHolder
    {
        public Forbidden Value;
    }

    static JsonDeserializer.Settings CreateSettings() => new()
    {
        dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
        referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
        proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
        rethrowExceptions = true,
        logCatchedExceptions = false
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemberElementRecursiveSettingsOverrideBroaderScopesRegardlessOfRegistrationOrder(bool exactFirst)
    {
        var settings = CreateSettings();
        Action configureGeneric = () => settings.ConfigureGenericType(typeof(List<>), generic =>
            generic.ConfigureElement<Value>(element =>
                element.SetDataAccess(JsonDeserializer.DataAccess.PublicFieldsAndProperties)));
        Action configureExact = () => settings.ConfigureType<List<Value>>(exact =>
            exact.ConfigureElement<Value>(element =>
                element.SetDataAccess(JsonDeserializer.DataAccess.PublicFieldsAndProperties)));
        if (exactFirst)
        {
            configureExact();
            configureGeneric();
        }
        else
        {
            configureGeneric();
            configureExact();
        }
        settings.ConfigureType<Holder>(type => type.ConfigureMember<List<Value>>(nameof(Holder.Scoped), member =>
            member.ConfigureElement<Value>(element => element.ConfigureRecursively(recursive =>
                recursive.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)))));
        var deserializer = new JsonDeserializer(settings);

        const string json = "{\"Scoped\":[{\"number\":1}],\"Plain\":[{\"number\":2}]}";
        Assert.True(deserializer.TryDeserialize(json, out Holder holder));
        Assert.Equal(1, holder.Scoped[0].GetNumber());
        Assert.Equal(0, holder.Plain[0].GetNumber());
    }

    [Fact]
    public void DistinctProposedTypesReuseContextWithoutCrossContamination()
    {
        string typeA = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedA));
        string typeB = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedB));
        var settings = CreateSettings();
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways;
        var deserializer = new JsonDeserializer(settings);

        string json = $"[{{\"$type\":\"{typeA}\",\"Value\":1,\"A\":2}},{{\"$type\":\"{typeB}\",\"Value\":3,\"B\":4}},{{\"$type\":\"{typeA}\",\"Value\":5,\"A\":6}}]";
        Assert.True(deserializer.TryDeserialize(json, out List<ProposedBase> values));
        Assert.Equal(2, Assert.IsType<ProposedA>(values[0]).A);
        Assert.Equal(4, Assert.IsType<ProposedB>(values[1]).B);
        Assert.Equal(6, Assert.IsType<ProposedA>(values[2]).A);
    }

    [Fact]
    public void RecursiveSettingsCannotBypassForbiddenTypePolicy()
    {
        var settings = CreateSettings();
        settings.rethrowExceptions = false;
        settings.logCatchedExceptions = false;
        settings.AddForbiddenType(typeof(Forbidden));
        settings.ConfigureType<SecurityHolder>(type => type.ConfigureRecursively(recursive =>
            recursive.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)));
        var deserializer = new JsonDeserializer(settings);

        Assert.False(deserializer.TryDeserialize("{\"Value\":{\"Value\":1}}", out SecurityHolder holder));
        Assert.Null(holder);
    }

    [Fact]
    public void RecursiveSettingsCannotBypassWhitelistPolicy()
    {
        var settings = CreateSettings();
        settings.rethrowExceptions = false;
        settings.logCatchedExceptions = false;
        settings.typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes;
        settings.AddAllowedType<SecurityHolder>();
        settings.ConfigureType<SecurityHolder>(type => type.ConfigureRecursively(recursive =>
            recursive.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)));
        var deserializer = new JsonDeserializer(settings);

        Assert.False(deserializer.TryDeserialize("{\"Value\":{\"Value\":1}}", out SecurityHolder holder));
        Assert.Null(holder);
    }
}
