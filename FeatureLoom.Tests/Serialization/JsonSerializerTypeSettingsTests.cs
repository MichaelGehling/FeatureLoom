using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    /// <summary>
    /// Covers the per-type and per-member write settings that mirror the JsonDeserializer
    /// configuration model.
    /// </summary>
    public class JsonSerializerTypeSettingsTests
    {
        public class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public string Secret { get; set; }
        }

        public class Box<T>
        {
            public T Content { get; set; }
            public string Label { get; set; }
        }

        public class AttributedPerson
        {
            public string Name { get; set; }

            [JsonIgnore]
            public string Secret { get; set; }

            [JsonInclude]
            private string Hidden { get; set; }

            public AttributedPerson() { }

            public AttributedPerson(string name, string secret, string hidden)
            {
                Name = name;
                Secret = secret;
                Hidden = hidden;
            }
        }

        private static JsonSerializer CreateSerializer(Action<JsonSerializer.Settings> configure)
        {
            var settings = new JsonSerializer.Settings();
            settings.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo;
            settings.dataSelection = JsonSerializer.DataSelection.PublicFieldsAndProperties;
            configure?.Invoke(settings);
            return new JsonSerializer(settings);
        }

        [Fact]
        public void ConfigureType_SetIgnore_OmitsMember()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore())));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30, Secret = "x" });

            Assert.DoesNotContain("Secret", json);
            Assert.Contains("\"Name\":\"Ann\"", json);
            Assert.Contains("\"Age\":30", json);
        }

        [Fact]
        public void ConfigureType_OverrideName_RenamesMember()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Name), ms => ms.OverrideName("fullName"))));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30 });

            Assert.Contains("\"fullName\":\"Ann\"", json);
            Assert.DoesNotContain("\"Name\"", json);
        }

        [Fact]
        public void ConfigureType_IgnoreFirstMember_ProducesValidJson()
        {
            // The first member carries no leading comma. Skipping it must not leave a stray comma.
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Name), ms => ms.SetIgnore())));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30, Secret = "s" });

            Assert.DoesNotContain("{,", json);
            Assert.DoesNotContain(",,", json);
            Assert.DoesNotContain(",}", json);
            Assert.DoesNotContain("\"Name\"", json);
        }

        [Fact]
        public void ConfigureType_AllMembersIgnored_ProducesEmptyObject()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                {
                    ts.ConfigureMember<string>(nameof(Person.Name), ms => ms.SetIgnore());
                    ts.ConfigureMember<int>(nameof(Person.Age), ms => ms.SetIgnore());
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore());
                }));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30, Secret = "s" });

            Assert.Equal("{}", json);
        }

        [Fact]
        public void ConfigureType_SetDataSelection_OverridesGlobalSetting()
        {
            // Globally only public properties are written, but Person switches to the field based
            // selection, which emits the compiler generated backing fields under clean names.
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.SetDataSelection(JsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields)));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30, Secret = "s" });

            Assert.Contains("\"Name\":\"Ann\"", json);
            Assert.Contains("\"Age\":30", json);
        }

        [Fact]
        public void ConfigureType_SettingsDoNotLeakToOtherTypes()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Name), ms => ms.OverrideName("fullName"))));

            string personJson = serializer.Serialize(new Person { Name = "Ann", Age = 1 });
            string boxJson = serializer.Serialize(new Box<string> { Content = "c", Label = "Name" });

            Assert.Contains("\"fullName\"", personJson);
            Assert.Contains("\"Label\":\"Name\"", boxJson);
            Assert.DoesNotContain("fullName", boxJson);
        }

        [Fact]
        public void ConfigureGenericType_AppliesToConstructedTypes()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureGenericType(typeof(Box<>), ts =>
                    ts.ConfigureMember<string>(nameof(Box<object>.Label), ms => ms.SetIgnore())));

            string intBox = serializer.Serialize(new Box<int> { Content = 1, Label = "l" });
            string stringBox = serializer.Serialize(new Box<string> { Content = "c", Label = "l" });

            Assert.DoesNotContain("Label", intBox);
            Assert.DoesNotContain("Label", stringBox);
            Assert.Contains("\"Content\":1", intBox);
        }

        [Fact]
        public void ConfigureType_TakesPrecedenceOverGenericTypeDefinition()
        {
            var serializer = CreateSerializer(s =>
            {
                s.ConfigureGenericType(typeof(Box<>), ts =>
                    ts.ConfigureMember<string>(nameof(Box<object>.Label), ms => ms.SetIgnore()));
                s.ConfigureType<Box<int>>(ts =>
                    ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("tag")));
            });

            string intBox = serializer.Serialize(new Box<int> { Content = 1, Label = "l" });
            string stringBox = serializer.Serialize(new Box<string> { Content = "c", Label = "l" });

            // The concrete configuration wins for Box<int>, the generic one still applies elsewhere.
            Assert.Contains("\"tag\":\"l\"", intBox);
            Assert.DoesNotContain("Label", stringBox);
        }

        [Fact]
        public void ConfigureMember_NullCallback_RemovesMemberSettings()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                {
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore());
                    ts.ConfigureMember<string>(nameof(Person.Secret), null);
                }));

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 1, Secret = "s" });

            Assert.Contains("\"Secret\":\"s\"", json);
        }

        [Fact]
        public void ConfigureType_NullCallback_RemovesTypeSettings()
        {
            var serializer = CreateSerializer(s =>
            {
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore()));
                s.ConfigureType<Person>(null);
            });

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 1, Secret = "s" });

            Assert.Contains("\"Secret\":\"s\"", json);
        }

        [Fact]
        public void ConfigureMember_UnknownMember_Throws()
        {
            var settings = new JsonSerializer.Settings();

            Assert.Throws<Exception>(() =>
                settings.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>("DoesNotExist", ms => ms.SetIgnore())));
        }

        [Fact]
        public void ConfigureMember_WrongMemberType_Throws()
        {
            var settings = new JsonSerializer.Settings();

            Assert.Throws<Exception>(() =>
                settings.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<int>(nameof(Person.Name), ms => ms.SetIgnore())));
        }

        [Fact]
        public void ConfigureType_RepeatedConfiguration_IsMerged()
        {
            var serializer = CreateSerializer(s =>
            {
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore()));
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Name), ms => ms.OverrideName("fullName")));
            });

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 1, Secret = "s" });

            Assert.DoesNotContain("Secret", json);
            Assert.Contains("\"fullName\":\"Ann\"", json);
        }

        [Fact]
        public void ConfigureType_SettingsApplyConsistentlyOnRepeatedSerialization()
        {
            // The type writer is cached after the first use, so the settings must survive reuse.
            var serializer = CreateSerializer(s =>
                s.ConfigureType<Person>(ts =>
                    ts.ConfigureMember<string>(nameof(Person.Secret), ms => ms.SetIgnore())));

            var person = new Person { Name = "Ann", Age = 1, Secret = "s" };
            string first = serializer.Serialize(person);
            string second = serializer.Serialize(person);

            Assert.Equal(first, second);
            Assert.DoesNotContain("Secret", first);
        }

        public enum Color { Red = 0, Green = 1 }

        public class Palette
        {
            public Color Primary { get; set; }
            public Color? Secondary { get; set; }
        }

        public class Wrapper
        {
            public Person Person { get; set; }
        }

        [Fact]
        public void ConfigureType_SetEnumAsString_OverridesGlobalSetting()
        {
            var serializer = CreateSerializer(s =>
            {
                s.enumAsString = false;
                s.ConfigureType<Color>(ts => ts.SetEnumAsString(true));
            });

            string json = serializer.Serialize(new Palette { Primary = Color.Green });

            Assert.Contains("\"Primary\":\"Green\"", json);
        }

        [Fact]
        public void ConfigureType_SetEnumAsString_AppliesToNullableEnumMembers()
        {
            var serializer = CreateSerializer(s =>
            {
                s.enumAsString = false;
                s.ConfigureType<Color>(ts => ts.SetEnumAsString(true));
            });

            string json = serializer.Serialize(new Palette { Primary = Color.Red, Secondary = Color.Green });

            Assert.Contains("\"Secondary\":\"Green\"", json);
        }

        [Fact]
        public void ConfigureType_SetEnumAsString_False_OverridesGlobalTrue()
        {
            var serializer = CreateSerializer(s =>
            {
                s.enumAsString = true;
                s.ConfigureType<Color>(ts => ts.SetEnumAsString(false));
            });

            string json = serializer.Serialize(new Palette { Primary = Color.Green });

            Assert.Contains("\"Primary\":1", json);
        }

        [Fact]
        public void ConfigureType_SetTypeInfoHandling_AddsTypeInfoForConfiguredTypeOnly()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo;
                s.ConfigureType<Person>(ts => ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo));
            });

            string json = serializer.Serialize(new Wrapper { Person = new Person { Name = "Ann" } });

            // The nested Person carries type info, the enclosing Wrapper does not.
            Assert.Contains("\"$type\"", json);
            Assert.DoesNotContain(nameof(Wrapper), json);
        }

        [Fact]
        public void ConfigureType_SetTypeInfoHandling_CanSuppressTypeInfoForOneType()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.ConfigureType<Person>(ts => ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo));
            });

            string json = serializer.Serialize(new Wrapper { Person = new Person { Name = "Ann" } });

            // Only the Wrapper writes a "$type"; the Person body starts right at its first member.
            Assert.Contains(nameof(Wrapper), json);
            Assert.Contains("\"Person\":{\"Name\"", json);
            Assert.Equal(1, json.Split("\"$type\"").Length - 1);
        }

        [Fact]
        public void ConfigureType_WithoutOverrides_UsesGlobalSettings()
        {
            var serializer = CreateSerializer(s =>
            {
                s.enumAsString = true;
                s.ConfigureType<Palette>(ts => ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties));
            });

            string json = serializer.Serialize(new Palette { Primary = Color.Green });

            // Color itself is not configured, so the global enumAsString still applies.
            Assert.Contains("\"Primary\":\"Green\"", json);
        }

        public class BlobHolder
        {
            public byte[] Data { get; set; }
        }

        [Fact]
        public void ConfigureType_SetWriteByteArrayAsBase64String_OverridesGlobalSetting()
        {
            var serializer = CreateSerializer(s =>
            {
                s.writeByteArrayAsBase64String = true;
                s.ConfigureType<byte[]>(ts => ts.SetWriteByteArrayAsBase64String(false));
            });

            string json = serializer.Serialize(new BlobHolder { Data = new byte[] { 1, 2, 3 } });

            Assert.Contains("\"Data\":[1,2,3]", json);
        }

        [Fact]
        public void ConfigureType_SetWriteByteArrayAsBase64String_True_OverridesGlobalFalse()
        {
            var serializer = CreateSerializer(s =>
            {
                s.writeByteArrayAsBase64String = false;
                s.ConfigureType<byte[]>(ts => ts.SetWriteByteArrayAsBase64String(true));
            });

            string json = serializer.Serialize(new BlobHolder { Data = new byte[] { 1, 2, 3 } });

            Assert.Contains($"\"Data\":\"{Convert.ToBase64String(new byte[] { 1, 2, 3 })}\"", json);
        }

        public class LazySequence : IEnumerable<int>
        {
            public int Marker = 42;

            public IEnumerator<int> GetEnumerator()
            {
                yield return 1;
                yield return 2;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void ConfigureType_SetTreatEnumerablesAsCollections_False_WritesTypeAsObject()
        {
            var serializer = CreateSerializer(s =>
            {
                s.treatEnumerablesAsCollections = true;
                s.ConfigureType<LazySequence>(ts => ts.SetTreatEnumerablesAsCollections(false));
            });

            string json = serializer.Serialize(new LazySequence());

            Assert.Contains("\"Marker\":42", json);
        }

        [Fact]
        public void ConfigureType_SetTreatEnumerablesAsCollections_True_WritesTypeAsArray()
        {
            var serializer = CreateSerializer(s =>
            {
                s.treatEnumerablesAsCollections = false;
                s.ConfigureType<LazySequence>(ts => ts.SetTreatEnumerablesAsCollections(true));
            });

            string json = serializer.Serialize(new LazySequence());

            Assert.Equal("[1,2]", json);
        }

        [Theory]
        [InlineData(JsonSerializer.DataSelection.PublicFieldsAndProperties)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields_RemoveBackingFields)]
        public void ConfigureMember_SetIgnoreFalse_ReincludesJsonIgnoredMember(JsonSerializer.DataSelection dataSelection)
        {
            var serializer = CreateSerializer(s =>
            {
                s.dataSelection = dataSelection;
                s.ConfigureType<AttributedPerson>(ts =>
                    ts.ConfigureMember<string>(nameof(AttributedPerson.Secret), ms => ms.SetIgnore(false)));
            });

            string json = serializer.Serialize(new AttributedPerson("Ann", "s3cr3t", "h"));

            // The explicit member setting overrules the JsonIgnore attribute.
            Assert.Contains("s3cr3t", json);
        }

        [Theory]
        [InlineData(JsonSerializer.DataSelection.PublicFieldsAndProperties)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields)]
        [InlineData(JsonSerializer.DataSelection.PublicAndPrivateFields_RemoveBackingFields)]
        public void ConfigureMember_SetIgnoreTrue_OmitsJsonIncludedMember(JsonSerializer.DataSelection dataSelection)
        {
            var serializer = CreateSerializer(s =>
            {
                s.dataSelection = dataSelection;
                s.ConfigureType<AttributedPerson>(ts =>
                    ts.ConfigureMember<string>("Hidden", ms => ms.SetIgnore()));
            });

            string json = serializer.Serialize(new AttributedPerson("Ann", "s3cr3t", "h1dden"));

            // The explicit member setting overrules the JsonInclude attribute.
            Assert.DoesNotContain("h1dden", json);
        }

        [Fact]
        public void ConfigureMember_SetIgnoreFalse_DoesNotDuplicateNormalMember()
        {
            var serializer = CreateSerializer(s =>
                s.ConfigureType<AttributedPerson>(ts =>
                    ts.ConfigureMember<string>(nameof(AttributedPerson.Name), ms => ms.SetIgnore(false))));

            string json = serializer.Serialize(new AttributedPerson("Ann", "s", "h"));

            Assert.Equal(1, json.Split("\"Name\"").Length - 1);
        }

        [Fact]
        public void ConfigureType_SetCustomTypeName_OverridesTypeNameFormat()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.typeNameFormat = JsonSerializer.TypeNameFormat.FullName;
                s.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));
            });

            string json = serializer.Serialize(new Person { Name = "Ann" });

            Assert.Contains("\"$type\":\"person\"", json);
        }

        [Fact]
        public void ConfigureType_SetCustomTypeName_NonGenericOverloadSharesTheSameEntry()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.ConfigureType(typeof(Person), ts => ts.SetCustomTypeName("firstName"));
                s.ConfigureType<Person>(ts => ts.SetCustomTypeName("secondName"));
            });

            string json = serializer.Serialize(new Person { Name = "Ann" });

            Assert.Contains("\"$type\":\"secondName\"", json);
            Assert.DoesNotContain("firstName", json);
        }

        [Fact]
        public void ConfigureType_SetCustomTypeName_DoesNotAffectOtherTypes()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));
            });

            string json = serializer.Serialize(new Wrapper { Person = new Person { Name = "Ann" } });

            Assert.Contains("\"$type\":\"person\"", json);
            Assert.Contains(nameof(Wrapper), json);
        }

        [Fact]
        public void ConfigureType_SetCustomTypeName_IsNotInheritedByConstructedGenericTypes()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.ConfigureType<Box<int>>(ts => ts.SetCustomTypeName("intBox"));
            });

            string intJson = serializer.Serialize(new Box<int> { Content = 1 });
            string stringJson = serializer.Serialize(new Box<string> { Content = "x" });

            Assert.Contains("\"$type\":\"intBox\"", intJson);
            Assert.DoesNotContain("intBox", stringJson);
        }

        [Fact]
        public void ConfigureGenericType_SetCustomTypeName_Throws()
        {
            // A single literal name cannot stay unique across the constructed types.
            Assert.Throws<Exception>(() => CreateSerializer(s =>
                s.ConfigureGenericType(typeof(Box<>), ts => ts.SetCustomTypeName("box"))));
        }

        [Fact]
        public void ConfigureType_SetCustomTypeName_RoundTripsIntoConfiguredType()
        {
            var serializer = CreateSerializer(s =>
            {
                s.typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo;
                s.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));
            });

            string json = serializer.Serialize(new Person { Name = "Ann", Age = 30 });

            var deserializerSettings = new JsonDeserializer.Settings();
            deserializerSettings.AddCustomTypeName("person", typeof(Person));
            var deserializer = new JsonDeserializer(deserializerSettings);

            Assert.True(deserializer.TryDeserialize(json, out object result));
            var person = Assert.IsType<Person>(result);
            Assert.Equal("Ann", person.Name);
            Assert.Equal(30, person.Age);
        }
    }
}
