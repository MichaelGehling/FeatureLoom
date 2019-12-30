using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using Newtonsoft.Json;
using System;
using System.Net;

namespace FeatureFlowFramework.Web
{
    public struct EndpointConfig
    {
        [JsonIgnore]
        public IPAddress address;

        public int port;
        public string certificateName;

        [JsonIgnore]
        public string hostAddress;

        public string HostAddress
        {
            get
            {
                return hostAddress ?? address.ToString();
            }
            set
            {
                hostAddress = value;
                try
                {
                    address = hostAddress.ResolveToIpAddressAsync(true).Result;
                }
                catch(Exception e)
                {
                    Log.ERROR("Failed resolving hostName", e.ToString());
                }
            }
        }

        public EndpointConfig(IPAddress address, int port) : this()
        {
            this.address = address;
            this.port = port;
        }

        public EndpointConfig(IPAddress address, int port, string certificateName)
        {
            this.address = address;
            this.port = port;
            this.certificateName = certificateName;
            this.hostAddress = null;
        }

        public static int DefaultHttpsPort => 443;
        public static int DefaultHttpPort => 80;
    }
}
