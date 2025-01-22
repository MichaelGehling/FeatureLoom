using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class NetworkExtensions
    {
        public async static Task<IPAddress> ResolveToIpAddressAsync(this string str, bool useDns = true)
        {
            IPAddress ipAddress;
            if (useDns)
            {
                var hostEntry = await Dns.GetHostEntryAsync(str).ConfigureAwait(false);
                ipAddress = hostEntry.AddressList[0];
            }
            else
            {
                IPAddress.TryParse(str, out ipAddress);
            }
            return ipAddress;
        }
    }
}