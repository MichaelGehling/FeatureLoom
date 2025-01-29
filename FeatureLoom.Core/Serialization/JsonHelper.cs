using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

public static class JsonHelper
{
    static FeatureJsonSerializer serializer = new();
    static FeatureJsonDeserializer deserializer = new(new()
    {
        enableProposedTypes = true,
    });

    public static FeatureJsonSerializer DefaultSerializer => serializer;
    public static FeatureJsonDeserializer DefaultDeserializer => deserializer;
}
