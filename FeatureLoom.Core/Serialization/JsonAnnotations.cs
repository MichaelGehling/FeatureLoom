using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization
{
    
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class JsonIgnoreAttribute : Attribute
    {
    }
   
}
