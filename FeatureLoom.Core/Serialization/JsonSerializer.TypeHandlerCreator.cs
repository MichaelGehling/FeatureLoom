using System;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {
        /// <summary>
        /// Matches types and creates the writer for them. Matching and creation happen once per
        /// type, when the type's writer is built, never per value.
        /// </summary>
        internal interface ITypeHandlerCreator
        {
            bool SupportsType(Type type);
            void CreateTypeHandler(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type);
        }
    }
}
