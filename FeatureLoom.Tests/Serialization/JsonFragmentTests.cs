using System.Text;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonFragmentTests
    {
        [Fact]
        public void Default_IsInvalid_AndReturnsEmptyRepresentations()
        {
            JsonFragment fragment = default;

            Assert.False(fragment.IsValid);
            Assert.False(fragment.IsString);
            Assert.False(fragment.IsUtf8);
            Assert.Equal(string.Empty, fragment.JsonString);
            Assert.Empty(fragment.JsonUtf8);
            Assert.Equal(0, fragment.GetHashCode());
        }

        [Fact]
        public void StringCtor_AndUtf8Ctor_AreEqual()
        {
            const string json = "{\"a\":1,\"text\":\"hello\"}";
            var fromString = new JsonFragment(json);
            var fromUtf8 = new JsonFragment(Encoding.UTF8.GetBytes(json));

            Assert.True(fromString.Equals(fromUtf8));
            Assert.True(fromUtf8.Equals(fromString));
            Assert.Equal(fromString.GetHashCode(), fromUtf8.GetHashCode());
        }

        [Fact]
        public void Utf8Ctor_DecodesToExpectedString()
        {
            const string json = "{\"emoji\":\"😀\"}";
            var bytes = Encoding.UTF8.GetBytes(json);
            var fragment = new JsonFragment(bytes);

            Assert.True(fragment.IsUtf8);
            Assert.Equal(json, fragment.JsonString);
        }

        [Fact]
        public void EqualsObject_WorksForSameType_AndRejectsOtherTypes()
        {
            const string json = "{\"x\":42}";
            object sameValue = new JsonFragment(Encoding.UTF8.GetBytes(json));
            object differentType = json;

            var fragment = new JsonFragment(json);

            Assert.True(fragment.Equals(sameValue));
            Assert.False(fragment.Equals(differentType));
            Assert.False(fragment.Equals((object)null));
        }

        [Fact]
        public void EqualityOperators_AreConsistentWithEquals()
        {
            const string json = "{\"k\":\"v\"}";
            var left = new JsonFragment(json);
            var right = new JsonFragment(Encoding.UTF8.GetBytes(json));
            var different = new JsonFragment("{\"k\":\"other\"}");

            Assert.True(left == right);
            Assert.False(left != right);
            Assert.False(left == different);
            Assert.True(left != different);
        }

        [Fact]
        public void ImplicitConversions_WorkBothWays()
        {
            const string json = "{\"n\":7}";

            JsonFragment fromString = json;
            string asString = fromString;
            Assert.Equal(json, asString);

            var bytes = Encoding.UTF8.GetBytes(json);
            JsonFragment fromBytes = bytes;
            byte[] asBytes = fromBytes;
            Assert.Equal(bytes, asBytes);
        }

        [Fact]
        public void GetHashCode_IsStableAcrossRepeatedCalls()
        {
            const string json = "{\"a\":1}";
            var fragment = new JsonFragment(json);

            int h1 = fragment.GetHashCode();
            int h2 = fragment.GetHashCode();
            int h3 = fragment.GetHashCode();

            Assert.Equal(h1, h2);
            Assert.Equal(h2, h3);
        }

        [Fact]
        public void InvalidFragments_AreEqual()
        {
            var defaultFragment = default(JsonFragment);
            var nullStringFragment = new JsonFragment((string)null);

            Assert.True(defaultFragment.Equals(nullStringFragment));
            Assert.True(defaultFragment == nullStringFragment);
            Assert.Equal(0, defaultFragment.GetHashCode());
            Assert.Equal(0, nullStringFragment.GetHashCode());
        }
    }
}