using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

public static class JsonHelper
{
    static FeatureJsonSerializer serializer = new(new()
    {
        indent = true
    });
    static FeatureJsonDeserializer deserializer = new(new()
    {
        enableProposedTypes = true,
        strict = false,        
    });

    public static FeatureJsonSerializer DefaultSerializer => serializer;
    public static FeatureJsonDeserializer DefaultDeserializer => deserializer;
}
