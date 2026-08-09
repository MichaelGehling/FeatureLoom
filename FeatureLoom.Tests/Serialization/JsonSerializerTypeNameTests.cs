using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;

namespace FeatureLoom.Serialization;

/// <summary>
/// Covers the type name formats the serializer can write into "$type" members:
/// the simplified FeatureLoom format, the plain CLR full name, the Newtonsoft compatible
/// assembly qualified name, and explicitly registered custom names.
/// </summary>
public class JsonSerializerTypeNameTests
{
    public class Animal
    {
        public string Name { get; set; }
    }

    public class Dog : Animal
    {
        public bool CanBark { get; set; }
    }

    public class Holder
    {
        public Animal Pet { get; set; }
    }

    public class GenericHolder
    {
        public object Values { get; set; }
    }

    static JsonSerializer CreateSerializer(Action<JsonSerializer.Settings> configure = null)
    {
        var settings = new JsonSerializer.Settings
        {
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
        };
        configure?.Invoke(settings);
        return new JsonSerializer(settings);
    }

    [Fact]
    public void SimplifiedFormat_IsTheDefault()
    {
        var serializer = CreateSerializer();
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex", CanBark = true } });

        Assert.Contains($"\"$type\":\"{typeof(Dog).FullName}\"", json);
        Assert.DoesNotContain(", FeatureLoom.Tests", json);
    }

    [Fact]
    public void FullNameFormat_WritesFullNameWithoutAssembly()
    {
        var serializer = CreateSerializer(s => s.typeNameFormat = JsonSerializer.TypeNameFormat.FullName);
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex" } });

        Assert.Contains($"\"$type\":\"{typeof(Dog).FullName}\"", json);
        Assert.DoesNotContain(", FeatureLoom.Tests", json);
    }

    [Fact]
    public void AssemblyQualifiedFormat_WritesShortAssemblyName()
    {
        var serializer = CreateSerializer(s => s.typeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified);
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex" } });

        string expected = typeof(Dog).FullName + ", " + typeof(Dog).Assembly.GetName().Name;
        Assert.Contains($"\"$type\":\"{expected}\"", json);

        // The verbose parts of the assembly qualified name must be stripped.
        Assert.DoesNotContain("Version=", json);
        Assert.DoesNotContain("PublicKeyToken=", json);
        Assert.DoesNotContain("Culture=", json);
    }

    [Fact]
    public void CustomTypeName_OverridesEveryFormat()
    {
        foreach (var format in new[]
        {
            JsonSerializer.TypeNameFormat.Simplified,
            JsonSerializer.TypeNameFormat.FullName,
            JsonSerializer.TypeNameFormat.AssemblyQualified,
        })
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeNameFormat = format;
                s.AddCustomTypeName<Dog>("dog");
            });

            string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex" } });
            Assert.Contains("\"$type\":\"dog\"", json);
        }
    }

    [Fact]
    public void CustomTypeName_OverridesGenericFormat()
    {
        var serializer = CreateSerializer(s =>
        {
            s.typeNameFormat = JsonSerializer.TypeNameFormat.Simplified;
            s.genericTypeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified;
            s.AddCustomTypeName(typeof(List<string>), "stringList");
        });

        string json = serializer.Serialize(new GenericHolder { Values = new List<string> { "a" } });
        Assert.Contains("\"$type\":\"stringList\"", json);
    }

    [Fact]
    public void GenericTypeNameFormat_IsAppliedOnlyToGenericTypes()
    {
        var serializer = CreateSerializer(s =>
        {
            s.typeNameFormat = JsonSerializer.TypeNameFormat.Simplified;
            s.genericTypeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified;
        });

        string genericJson = serializer.Serialize(new GenericHolder { Values = new List<string> { "a" } });
        // Generic type uses the CLR/Newtonsoft nested notation incl. assembly.
        Assert.Contains("System.Collections.Generic.List`1[[", genericJson);

        string simpleJson = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex" } });
        // Non generic type keeps the simplified name.
        Assert.Contains($"\"$type\":\"{typeof(Dog).FullName}\"", simpleJson);
    }

    [Fact]
    public void GenericTypeNameFormat_DefaultsToTypeNameFormat()
    {
        var serializer = CreateSerializer(s => s.typeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified);

        string json = serializer.Serialize(new GenericHolder { Values = new List<string> { "a" } });
        Assert.Contains("System.Collections.Generic.List`1[[", json);
    }

    [Theory]
    [InlineData(JsonSerializer.TypeNameFormat.Simplified)]
    [InlineData(JsonSerializer.TypeNameFormat.FullName)]
    [InlineData(JsonSerializer.TypeNameFormat.AssemblyQualified)]
    public void AllFormats_RoundTripThroughOwnDeserializer(JsonSerializer.TypeNameFormat format)
    {
        var serializer = CreateSerializer(s => s.typeNameFormat = format);
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex", CanBark = true } });

        var deserializerSettings = new JsonDeserializer.Settings();
        var deserializer = new JsonDeserializer(deserializerSettings);

        Assert.True(deserializer.TryDeserialize(json, out Holder result));
        var dog = Assert.IsType<Dog>(result.Pet);
        Assert.Equal("Rex", dog.Name);
        Assert.True(dog.CanBark);
    }

    [Fact]
    public void AssemblyQualifiedOutput_IsReadableByNewtonsoft()
    {
        var serializer = CreateSerializer(s => s.typeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified);
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex", CanBark = true } });

        var result = JsonConvert.DeserializeObject<Holder>(json, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
        });

        var dog = Assert.IsType<Dog>(result.Pet);
        Assert.Equal("Rex", dog.Name);
        Assert.True(dog.CanBark);
    }

    [Fact]
    public void NewtonsoftOutput_IsReadableByFeatureLoom()
    {
        string json = JsonConvert.SerializeObject(
            new Holder { Pet = new Dog { Name = "Rex", CanBark = true } },
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

        var deserializer = new JsonDeserializer(new JsonDeserializer.Settings());

        Assert.True(deserializer.TryDeserialize(json, out Holder result));
        var dog = Assert.IsType<Dog>(result.Pet);
        Assert.Equal("Rex", dog.Name);
        Assert.True(dog.CanBark);
    }

    [Fact]
    public void OurAssemblyQualifiedName_MatchesNewtonsoftFormat()
    {
        string ourJson = CreateSerializer(s => s.typeNameFormat = JsonSerializer.TypeNameFormat.AssemblyQualified)
            .Serialize(new Holder { Pet = new Dog { Name = "Rex", CanBark = true } });

        string newtonsoftJson = JsonConvert.SerializeObject(
            new Holder { Pet = new Dog { Name = "Rex", CanBark = true } },
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

        string expectedTypeName = typeof(Dog).FullName + ", " + typeof(Dog).Assembly.GetName().Name;

        // Both serializers must agree on the exact type name text.
        Assert.Contains(expectedTypeName, ourJson);
        Assert.Contains(expectedTypeName, newtonsoftJson);
    }

    [Fact]
    public void CustomTypeName_RoundTripsWhenDeserializerKnowsTheName()
    {
        var serializer = CreateSerializer(s => s.AddCustomTypeName<Dog>("dog"));
        string json = serializer.Serialize(new Holder { Pet = new Dog { Name = "Rex", CanBark = true } });
        Assert.Contains("\"$type\":\"dog\"", json);

        // The deserializer keeps its own name-to-type map, so the counterpart has to be registered.
        var deserializerSettings = new JsonDeserializer.Settings();
        deserializerSettings.AddCustomTypeName("dog", typeof(Dog));
        var deserializer = new JsonDeserializer(deserializerSettings);

        Assert.True(deserializer.TryDeserialize(json, out Holder result));
        var dog = Assert.IsType<Dog>(result.Pet);
        Assert.Equal("Rex", dog.Name);
        Assert.True(dog.CanBark);
    }

    [Fact]
    public void ClearCustomTypeNames_RestoresConfiguredFormat()
    {
        var settings = new JsonSerializer.Settings
        {
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
        };
        settings.AddCustomTypeName<Dog>("dog");
        settings.ClearCustomTypeNames();

        string json = new JsonSerializer(settings).Serialize(new Holder { Pet = new Dog { Name = "Rex" } });

        Assert.DoesNotContain("\"$type\":\"dog\"", json);
        Assert.Contains($"\"$type\":\"{typeof(Dog).FullName}\"", json);
    }
}
