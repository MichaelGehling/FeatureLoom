using FeatureLoom.Logging;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class ClientSslStreamUpgrader : IStreamUpgrader
    {
        private readonly string serverName;
        private readonly bool ignoreFailedAuthentication;

        public ClientSslStreamUpgrader(string serverName, bool ignoreFailedAuthentication)
        {
            this.serverName = serverName;
            this.ignoreFailedAuthentication = ignoreFailedAuthentication;
        }

        public Stream Upgrade(Stream stream)
        {
            SslStream sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            sslStream.AuthenticateAsClient(serverName);
            return sslStream;
        }

        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (ignoreFailedAuthentication) return true;

            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            OptLog.ERROR()?.Build("Certificate error: {0}", sslPolicyErrors.ToName());
            // refuse connection
            return false;
        }
    }
}