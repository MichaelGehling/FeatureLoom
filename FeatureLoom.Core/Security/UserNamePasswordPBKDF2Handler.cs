using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
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

        public StoredCredential GenerateStoredCredential(UsernamePassword credential)
        {
            StoredCredential storedCredential = new StoredCredential();
            storedCredential.credentialType = CredentialType;
            storedCredential.properties.Add("Username", credential.username);            
            storedCredential.properties.Add("Iterations", iterations.ToString());
            byte[] salt = RandomGenerator.Bytes(saltLength, true);
            storedCredential.properties.Add("Salt", Convert.ToBase64String(salt));
            Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations);
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
                Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(credential.password, salt, iterations);
                byte[] hashedPassword = pbkdf2.GetBytes(hashLength);
                string hashedPasswordStr = Convert.ToBase64String(hashedPassword);
                return storedCredential.CheckProperty("HashedPassword", hashedPasswordStr);
            }
            else return false;
        }
    }
}
