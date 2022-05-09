using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.Security
{
    public class Identity
    {
        #region static
        static string storageCategory = "Security/Identities";
        public static string StorageCategory { get => storageCategory; set => storageCategory = value; }        

        public static Task<AsyncOut<bool, Identity>> TryLoadIdentityAsync(string identityId)
        {
            return Storage.GetReader(storageCategory).TryReadAsync<Identity>(identityId);
        }

        public static bool Exists(string identityId)
        {
            return Storage.GetReader(storageCategory).Exists(identityId);
        }
        #endregion

        string identityId;
        List<StoredCredential> storedCredentials = new List<StoredCredential>();
        List<string> roleNames = new List<string>();
        [JsonIgnore]
        List<IdentityRole> _roles;

        public Identity(string identityId, StoredCredential credential)
        {
            this.identityId = identityId;
            this.storedCredentials.Add(credential);
        }

        public Identity()
        {
        }

        [JsonIgnore]
        public IEnumerable<StoredCredential> StoredCredentials => storedCredentials;

        public void SetOrReplaceCredential(StoredCredential credential)
        {
            if (!storedCredentials.Replace(credential, item => item.credentialType == credential.credentialType))
            {
                storedCredentials.Add(credential);
            }
        }

        public bool TryGetCredential(string credentialType, out StoredCredential storedCredential)
        {
            return storedCredentials.TryFindFirst(credential => credential.credentialType == credentialType, out storedCredential);
        }

        public bool RemoveCredential(string credentialType)
        {
            return storedCredentials.RemoveWhere(credential => credential.credentialType == credentialType) > 0;
        }

        public void ClearCredentials() => storedCredentials.Clear();

        [JsonIgnore]
        public string IdentityId => identityId;

        [JsonIgnore]
        public IEnumerable<IdentityRole> Roles
        {
            get
            {
                if (_roles == null)
                {
                    List<IdentityRole> roles = new List<IdentityRole>();                    
                    foreach (var roleName in roleNames)
                    {
                        if (IdentityRole.TryLoadIdentityRoleAsync(roleName).WaitFor().Out(out IdentityRole role))
                        {
                            roles.Add(role);
                        }
                    }
                    _roles = roles;
                }
                return _roles;
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Permissions => Roles.SelectMany(role => role.Permissions).Distinct();

        public bool HasRole(string roleName) => Roles.Any(role => role.RoleName == roleName);

        public bool HasPermission(string permission) => Roles.Any(role => role.HasPermission(permission));
        public bool HasAnyPermission() => Roles.Any(role => role.HasAnyPermission());
        public bool HasAnyPermission(IEnumerable<string> checkedPermissions) => Roles.Any(role => role.HasAnyPermission(checkedPermissions));
        public bool MatchesAnyPermission(string permissionWildcard) => Roles.Any(role => role.MatchesAnyPermission(permissionWildcard));
        public bool MatchesAnyPermission(IEnumerable<string> permissionWildcards) => Roles.Any(role => role.MatchesAnyPermission(permissionWildcards));

        public Task<bool> TryStoreAsync()
        {
            return Storage.GetWriter(storageCategory).TryWriteAsync(identityId, this);
        }

        public Task<bool> TryRemoveFromStorageAsync()
        {
            return Storage.GetWriter(storageCategory).TryDeleteAsync(identityId);
        }

        public void AddRole(IdentityRole role)
        {
            if (!roleNames.Contains(role.RoleName))
            {
                roleNames.Add(role.RoleName);
                if (_roles != null) _roles.Add(role);
            }
        }

        public void RemoveRole(IdentityRole role)
        {
            if (roleNames.Contains(role.RoleName))
            {
                roleNames.Remove(role.RoleName);
                if (_roles != null) _roles.RemoveAll(r => role.RoleName == r.RoleName);
            }
        }
        
    }
}
