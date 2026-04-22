using FeatureLoom.DependencyInversion;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

public static class JsonHelper
{
    public static JsonSerializer DefaultSerializer { get => Service<JsonHelperService>.Instance.DefaultSerializer; set => Service<JsonHelperService>.Instance.DefaultSerializer = value; }
    public static JsonDeserializer DefaultDeserializer { get => Service<JsonHelperService>.Instance.DefaultDeserializer; set => Service<JsonHelperService>.Instance.DefaultDeserializer = value; }
}

public class JsonHelperService
{
    JsonSerializer serializer = new(new()
    {
        indent = true
    });
    JsonDeserializer deserializer = new(settings =>
    {
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckWhereReasonable;
        settings.strict = false;
    });

    public JsonSerializer DefaultSerializer { get => serializer; set => serializer = value; }
    public JsonDeserializer DefaultDeserializer { get => deserializer; set => deserializer = value; }
}
