using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

public struct JsonFragment
{        
    public JsonFragment(string jsonString)
    {
        JsonString = jsonString;
    }

    public string JsonString { get; set; }
    public bool IsValid => JsonString != null;
    public override string ToString() => JsonString;
}
