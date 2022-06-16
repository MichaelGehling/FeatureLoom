using FeatureLoom.Time;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FeatureLoom.Extensions;

#if NETSTANDARD2_0
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Operators;
#endif

namespace FeatureLoom.Core.Security
{
    public class CertificateBuilder
    {
        public CertificateBuilder(string subjectName)
        {
            SubjectName = subjectName;
        }

        public string SubjectName { get; set; }
        public Algorithm SignatureAlgorithm { get; set; } = Algorithm.ECDSA_SHA256;
        public TimeFrame ValidityTime { get; set; } = new TimeFrame(2.Years());

        public enum Algorithm
        {
            ECDSA_SHA256,
            ECDSA_SHA384,
            ECDSA_SHA512
        }

        /// <summary>
        /// Builds
        /// </summary>
        /// <returns></returns>
        public X509Certificate2 Build()
        {
            return GenerateSelfSignedCertificate();
        }




#if NETSTANDARD2_0
        private string ConvertAlgorithmName(Algorithm algorithm)
        {
            switch(algorithm)
            {
                case Algorithm.ECDSA_SHA256: return "SHA256withECDSA";
                case Algorithm.ECDSA_SHA384: return "SHA384withECDSA";
                case Algorithm.ECDSA_SHA512: return "SHA512withECDSA";
                default: throw new Exception($"Algorithm {algorithm.ToName()} is not supported.");
            }
        }

        public X509Certificate2 GenerateSelfSignedCertificate()
        {
            const int keyStrength = 2048;

            // Generating Random Numbers
            CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
            SecureRandom random = new SecureRandom(randomGenerator);

            // The Certificate Generator
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

            // Serial Number
            Org.BouncyCastle.Math.BigInteger serialNumber = BigIntegers.CreateRandomInRange(Org.BouncyCastle.Math.BigInteger.One, Org.BouncyCastle.Math.BigInteger.ValueOf(Int64.MaxValue), random);            
            certificateGenerator.SetSerialNumber(serialNumber);                        

            // Issuer and Subject Name
            X509Name subjectDN = new X509Name(SubjectName);            
            certificateGenerator.SetIssuerDN(subjectDN); // self issued
            certificateGenerator.SetSubjectDN(subjectDN);

            // Valid For
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(2);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // Subject Public Key
            AsymmetricCipherKeyPair subjectKeyPair;
            var keyGenerationParameters = new KeyGenerationParameters(random, keyStrength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Generating the Certificate
            AsymmetricCipherKeyPair issuerKeyPair = subjectKeyPair;

            // Signature Algorithm
            // TODO: The Asn1SignatureFactory does not work for ECDSA -> Try to build a ISignatureFactory with ECDsa
            throw new NotImplementedException("The Asn1SignatureFactory does not work for ECDSA -> Try to build a ISignatureFactory with ECDsa!");
            ISignatureFactory signatureFactory = new Asn1SignatureFactory(ConvertAlgorithmName(SignatureAlgorithm), issuerKeyPair.Private, random);

            // selfsign certificate
            Org.BouncyCastle.X509.X509Certificate certificate = certificateGenerator.Generate(signatureFactory);

            // correcponding private key
            PrivateKeyInfo info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);

            // merge into X509Certificate2
            X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded());

            Asn1Sequence seq = (Asn1Sequence)Asn1Object.FromByteArray(info.PrivateKeyData.GetDerEncoded());
            if (seq.Count != 9)
            {
                //throw new PemException("malformed sequence in RSA private key");
            }

            RsaPrivateKeyStructure rsa = RsaPrivateKeyStructure.GetInstance(seq);
            RsaPrivateCrtKeyParameters rsaparams = new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent, rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);

            x509.PrivateKey = DotNetUtilities.ToRSA(rsaparams);
            return x509;

        }
#else
        private HashAlgorithmName ConvertAlgorithmName(Algorithm algorithm)
        {
            switch (algorithm)
            {
                case Algorithm.ECDSA_SHA256: return HashAlgorithmName.SHA256;
                case Algorithm.ECDSA_SHA384: return HashAlgorithmName.SHA384;
                case Algorithm.ECDSA_SHA512: return HashAlgorithmName.SHA512;
                default: throw new Exception($"Algorithm {algorithm.ToName()} is not supported.");
            }
        }

        public X509Certificate2 GenerateSelfSignedCertificate()
        {
            var ecdsa = ECDsa.Create();
            var req = new CertificateRequest(SubjectName, ecdsa, ConvertAlgorithmName(SignatureAlgorithm));
            X509Certificate2 cert = req.CreateSelfSigned(ValidityTime.utcStartTime, ValidityTime.utcEndTime);
            return cert;
        }
#endif

    }
}
