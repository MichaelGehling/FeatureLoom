namespace FeatureLoom.Security
{
    public interface ICredentialHandler<T>
    {
        string CredentialType { get; }
        StoredCredential GenerateStoredCredential(T credential);
        bool VerifyCredential(T credential, StoredCredential storedCredential);
    }
}
