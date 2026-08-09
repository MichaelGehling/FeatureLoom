using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Xunit;

namespace FeatureLoom.Serialization
{
    /// <summary>
    /// Covers the id based reference format (JsonSerializer.ReferenceFormat.IdBased), which
    /// mirrors the "$id"/"$ref" convention used by System.Text.Json (ReferenceHandler.Preserve)
    /// and Newtonsoft.Json (PreserveReferencesHandling). The default JsonPath format stays
    /// untouched and is covered by JsonSerializerReferenceTests.
    /// </summary>
    public class JsonSerializerIdReferenceTests
    {
        public class Node
        {
            public string Name;
            public Node Next;
            public Node Other;
        }

        public class ListHolder
        {
            public List<Node> Items;
            public List<Node> Same;
        }

        private static JsonSerializer.Settings IdBased => new JsonSerializer.Settings
        {
            referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
            referenceFormat = JsonSerializer.ReferenceFormat.IdBased,
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
        };

        private static JsonSerializer.Settings JsonPathBased => new JsonSerializer.Settings
        {
            referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
            referenceFormat = JsonSerializer.ReferenceFormat.JsonPath,
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
        };

        private static JsonSerializerOptions StjOptions => new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            IncludeFields = true
        };

        [Fact]
        public void SharedObject_IsWrittenWithIdAndRef()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };

            var json = new JsonSerializer(IdBased).Serialize(root);

            Assert.Equal(
                "{\"$id\":\"1\",\"Name\":\"root\",\"Next\":{\"$id\":\"2\",\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":{\"$ref\":\"2\"}}",
                json);
        }

        /// <summary>
        /// A collection cannot carry its own "$id" member, so it is wrapped into an object and the
        /// elements move into "$values" - exactly like System.Text.Json does.
        /// </summary>
        [Fact]
        public void SharedList_IsWrappedIntoIdAndValues()
        {
            var list = new List<Node> { new Node { Name = "a" } };
            var holder = new ListHolder { Items = list, Same = list };

            var json = new JsonSerializer(IdBased).Serialize(holder);

            Assert.Equal(
                "{\"$id\":\"1\",\"Items\":{\"$id\":\"2\",\"$values\":[{\"$id\":\"3\",\"Name\":\"a\",\"Next\":null,\"Other\":null}]},\"Same\":{\"$ref\":\"2\"}}",
                json);
        }

        [Fact]
        public void Output_IsIdenticalToSystemTextJson()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };

            var json = new JsonSerializer(IdBased).Serialize(root);

            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(root, StjOptions), json);
        }

        [Fact]
        public void Output_IsReadableBySystemTextJson()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };

            var json = new JsonSerializer(IdBased).Serialize(root);
            var restored = System.Text.Json.JsonSerializer.Deserialize<Node>(json, StjOptions);

            Assert.Equal("root", restored.Name);
            Assert.Equal("shared", restored.Next.Name);
            Assert.Same(restored.Next, restored.Other);
        }

        [Fact]
        public void Output_IsReadableByNewtonsoft()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };

            var json = new JsonSerializer(IdBased).Serialize(root);
            var restored = JsonConvert.DeserializeObject<Node>(json, new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            });

            Assert.Equal("root", restored.Name);
            Assert.Equal("shared", restored.Next.Name);
            Assert.Same(restored.Next, restored.Other);
        }

        [Fact]
        public void SystemTextJsonOutput_IsReadableByFeatureLoom()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };
            var json = System.Text.Json.JsonSerializer.Serialize(root, StjOptions);

            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault
            });

            Assert.True(deserializer.TryDeserialize(json, out Node restored));
            Assert.Equal("root", restored.Name);
            Assert.Same(restored.Next, restored.Other);
        }

        [Fact]
        public void IdBasedOutput_RoundTripsThroughOwnDeserializer()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };
            var json = new JsonSerializer(IdBased).Serialize(root);

            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault
            });

            Assert.True(deserializer.TryDeserialize(json, out Node restored));
            Assert.Equal("root", restored.Name);
            Assert.Same(restored.Next, restored.Other);
        }

        /// <summary>
        /// Self references must not cause an endless loop in the id based format either.
        /// </summary>
        [Fact]
        public void SelfReference_IsWrittenAsRefToOwnId()
        {
            var root = new Node { Name = "root" };
            root.Next = root;

            var json = new JsonSerializer(IdBased).Serialize(root);

            Assert.Equal("{\"$id\":\"1\",\"Name\":\"root\",\"Next\":{\"$ref\":\"1\"},\"Other\":null}", json);
        }

        /// <summary>
        /// The default format must stay unaffected by the new option.
        /// </summary>
        [Fact]
        public void JsonPathFormat_IsUnchanged()
        {
            var shared = new Node { Name = "shared" };
            var root = new Node { Name = "root", Next = shared, Other = shared };

            var json = new JsonSerializer(JsonPathBased).Serialize(root);

            Assert.Equal(
                "{\"Name\":\"root\",\"Next\":{\"Name\":\"shared\",\"Next\":null,\"Other\":null},\"Other\":{\"$ref\":\"$.Next\"}}",
                json);
        }
    }
}
