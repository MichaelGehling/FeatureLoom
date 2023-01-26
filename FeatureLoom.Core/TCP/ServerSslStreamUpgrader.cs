using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace FeatureLoom.TCP
{
    public class ServerSslStreamUpgrader : IStreamUpgrader
    {
        private readonly X509Certificate2 serverCertificate;

        public ServerSslStreamUpgrader(X509Certificate2 serverCertificate)
        {
            this.serverCertificate = serverCertificate;
        }

        public Stream Upgrade(Stream stream)
        {
            var sslStream = new SslStream(stream, false);
            sslStream.AuthenticateAsServer(serverCertificate, false, System.Security.Authentication.SslProtocols.None, true);
            return sslStream;
        }
    }
}