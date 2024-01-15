using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using Newtonsoft.Json;
using System;
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

        static Dictionary<string, Identity> cache = null;
        static FeatureLock cacheLock = new();

        public static async Task UpdateCache()
        {
            using (cacheLock.Lock())
            {
                var reader = Storage.GetReader(StorageCategory);
                if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
            }
        }

        public static async Task<string[]> GetAllIdentityIdsAsync(bool forceUpdateCache = false)
        {            
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.Keys.ToArray();
            }
        }

        public static async Task<Identity[]> GetAllIdentitiesAsync(bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.Values.ToArray();
            }
        }


        public static async Task<(bool, Identity)> TryGetIdentityAsync(string identityId, bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                    forceUpdateCache = false;
                }

                if (forceUpdateCache)
                {
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAsync<Identity>(identityId)).TryOut(out var identity)) return (false, null);
                    cache[identityId] = identity;
                    return (true, identity);
                }
                else
                {
                    return (cache.TryGetValue(identityId, out var identity), identity);
                }
            }
        }

        public static async Task<bool> Exists(string identityId, bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.ContainsKey(identityId);
            }
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

        public Identity(string identityId)
        {
            this.identityId = identityId;
        }

        public Identity()
        {
        }

        [JsonIgnore]
        public IEnumerable<StoredCredential> StoredCredentials => storedCredentials;

        public void SetOrReplaceCredential(StoredCredential credential, bool storeChanges)
        {
            if (!storedCredentials.Replace(credential, item => item.credentialType == credential.credentialType))
            {
                storedCredentials.Add(credential);
            }
            if (storeChanges) _ = StoreAsync();
        }

        public bool TryGetCredential(string credentialType, out StoredCredential storedCredential)
        {
            return storedCredentials.TryFindFirst(credential => credential.credentialType == credentialType, out storedCredential);            
        }

        public bool RemoveCredential(string credentialType, bool storeChanges)
        {
            var result =  storedCredentials.RemoveWhere(credential => credential.credentialType == credentialType) > 0;
            if (storeChanges) _ = StoreAsync();
            return result;
        }

        public void ClearCredentials(bool storeChanges)
        {
            storedCredentials.Clear();
            if (storeChanges) _ = StoreAsync();
        }

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
                        if (IdentityRole.TryLoadIdentityRoleAsync(roleName).WaitFor().TryOut(out IdentityRole role))
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

        public async Task StoreAsync()
        {
            using (cacheLock.Lock())
            {
                if (cache == null)
                {                    
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                
                cache[identityId] = this;                
            }
            if (!await Storage.GetWriter(storageCategory).TryWriteAsync(identityId, this)) throw new Exception($"Failed writing identity {identityId} to storage");
        }

        public async Task RemoveFromStorageAsync()
        {
            using (cacheLock.Lock())
            {
                if (cache == null)
                {
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<Identity>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }

                cache.Remove(identityId);
            }
            if (!await Storage.GetWriter(storageCategory).TryDeleteAsync(identityId)) throw new Exception($"Failed removing identity {identityId} from storage");
        }

        public void AddRole(IdentityRole role, bool storeChanges)
        {
            if (!roleNames.Contains(role.RoleName))
            {
                roleNames.Add(role.RoleName);
                if (_roles != null) _roles.Add(role);
            }

            if (storeChanges) _ = StoreAsync();
        }

        public void RemoveRole(IdentityRole role, bool storeChanges)
        {
            if (roleNames.Contains(role.RoleName))
            {
                roleNames.Remove(role.RoleName);
                if (_roles != null) _roles.RemoveAll(r => role.RoleName == r.RoleName);
            }

            if (storeChanges) _ = StoreAsync();
        }
        
    }
}
