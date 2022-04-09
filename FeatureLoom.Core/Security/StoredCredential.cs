using System.Collections.Generic;

namespace FeatureLoom.Security
{
    public class StoredCredential
    {
        public string credentialType;
        public Dictionary<string, string> properties = new Dictionary<string, string>();

        public bool CheckProperty(string propertyKey, string expectedValue)
        {
            return properties.TryGetValue(propertyKey, out string value) && value == expectedValue;
        }
    }
}
