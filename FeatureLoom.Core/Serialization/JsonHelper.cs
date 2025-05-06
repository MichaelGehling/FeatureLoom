using FeatureLoom.DependencyInversion;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

public static class JsonHelper
{
    public static FeatureJsonSerializer DefaultSerializer { get => Service<JsonHelperService>.Instance.DefaultSerializer; set => Service<JsonHelperService>.Instance.DefaultSerializer = value; }
    public static FeatureJsonDeserializer DefaultDeserializer { get => Service<JsonHelperService>.Instance.DefaultDeserializer; set => Service<JsonHelperService>.Instance.DefaultDeserializer = value; }
}

public class JsonHelperService
{
    FeatureJsonSerializer serializer = new(new()
    {
        indent = true
    });
    FeatureJsonDeserializer deserializer = new(new()
    {
        enableProposedTypes = true,
        strict = false,
    });

    public FeatureJsonSerializer DefaultSerializer { get => serializer; set => serializer = value; }
    public FeatureJsonDeserializer DefaultDeserializer { get => deserializer; set => deserializer = value; }
}
