using FeatureFlowFramework.Services.MetaData;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Services.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.Services.Web
{
    public struct HttpEndpointConfig
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
                    address = hostAddress.ResolveToIpAddressAsync(true).WaitFor();
                }
                catch(Exception e)
                {
                    Log.ERROR("Failed resolving hostName", e.ToString());
                }
            }
        }

        public HttpEndpointConfig(IPAddress address, int port) : this()
        {
            this.address = address;
            this.port = port;
        }

        public HttpEndpointConfig(IPAddress address, int port, string certificateName)
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