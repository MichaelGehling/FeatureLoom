
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FeatureLoom.Security
{
    public class UserNamePasswordPBKDF2Handler : ICredentialHandler<UsernamePassword>
    {
        public string CredentialType => "UsernamePasswordPBKDF2";
        public int saltLength = 32;
        public int hashLength = 32;
        public int iterations = 100_000;
#if !NETSTANDARD2_0
        public HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;
#endif

        public StoredCredential GenerateStoredCredential(UsernamePassword credential)
        {
            
#if NETSTANDARD2_0
           OptLog.WARNING().Build($"Using PBKDF2 with SHA1 for password hashing which is insecure. Consider upgrading to .NetStandard2.1 or later for better security with SHA256.");
#else
            if (hashAlgorithm == HashAlgorithmName.SHA1)
            {
                OptLog.WARNING().Build($"Using PBKDF2 with SHA1 for password hashing which is insecure. Consider setting hashAlgorithm to SHA256");
            }
#endif        

        StoredCredential storedCredential = new StoredCredential();
            storedCredential.credentialType = CredentialType;
            storedCredential.properties.Add("Username", credential.username);            
            storedCredential.properties.Add("Iterations", iterations.ToString());
            byte[] salt = RandomGenerator.Bytes(saltLength, true);
            storedCredential.properties.Add("Salt", Convert.ToBase64String(salt));
#if NETSTANDARD2_0
            Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations);            
#else
            Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations, hashAlgorithm);
#endif
            byte[] hashedPassword = pbkdf2.GetBytes(hashLength);
            string hashedPasswordStr = Convert.ToBase64String(hashedPassword);
            storedCredential.properties.Add("HashedPassword", hashedPasswordStr);
            return storedCredential;
        }


        public bool VerifyCredential(UsernamePassword credential, StoredCredential storedCredential)
        {
            if (storedCredential.credentialType != CredentialType) return false;
            if (!storedCredential.CheckProperty("Username", credential.username)) return false;
            if (storedCredential.properties.TryGetValue("Salt", out string saltStr) && storedCredential.properties.TryGetValue("Iterations", out string iterationsStr))
            {
                int iterations = int.Parse(iterationsStr);
                byte[] salt = Convert.FromBase64String(saltStr);
#if NETSTANDARD2_0
                Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations);
                OptLog.WARNING()?.Build($"Using PBKDF2 with SHA1 for password hashing. Consider upgrading to .NetStandard2.1 or later for better security with SHA256.");
#else
                Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations, hashAlgorithm);
#endif
                byte[] hashedPassword = pbkdf2.GetBytes(hashLength);
                string hashedPasswordStr = Convert.ToBase64String(hashedPassword);
                return storedCredential.CheckProperty("HashedPassword", hashedPasswordStr);
            }
            else return false;
        }
    }
}
