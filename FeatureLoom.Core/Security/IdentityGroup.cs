using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using FeatureLoom.Storages;
using Newtonsoft.Json;

namespace FeatureLoom.Security
{
    public class IdentityGroup
    {
        #region static
        static string storageCategory = "Security/IdentityGroups";
        public static string StorageCategory { get => storageCategory; set => storageCategory = value; }

        static Dictionary<string, IdentityGroup> cache = null;
        static FeatureLock cacheLock = new();

        public static async Task UpdateCache()
        {
            using(cacheLock.Lock())
            {
                var reader = Storage.GetReader(StorageCategory);
                if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
            }
        }        

        public static async Task<string[]> GetAllGroupIdsAsync(bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.Keys.ToArray();
            }
        }

        public static async Task<IdentityGroup[]> GetAllGroupsAsync(bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.Values.ToArray();
            }
        }


        public static async Task<(bool, IdentityGroup)> TryGetGroupAsync(string groupId, bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                    forceUpdateCache = false;
                }

                if (forceUpdateCache)
                {
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAsync<IdentityGroup>(groupId)).TryOut(out var group)) return (false, null);
                    cache[groupId] = group;
                    return (true, group);
                }
                else
                {
                    return (cache.TryGetValue(groupId, out var group), group);
                }
            }
        }

        public static async Task<bool> Exists(string groupId, bool forceUpdateCache = false)
        {
            using (var lockHandle = cacheLock.LockReadOnly())
            {
                if (cache == null) forceUpdateCache = true;
                if (forceUpdateCache)
                {
                    lockHandle.UpgradeToWriteMode();
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }
                return cache.ContainsKey(groupId);
            }
        }
        #endregion

        public async Task StoreAsync()
        {
            using (cacheLock.Lock())
            {
                if (cache == null)
                {
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }

                cache[groupId] = this;
            }
            if (!await Storage.GetWriter(storageCategory).TryWriteAsync(groupId, this)) throw new Exception($"Failed writing group {groupId} to storage");
        }

        public async Task RemoveFromStorageAsync()
        {
            using (cacheLock.Lock())
            {
                if (cache == null)
                {
                    var reader = Storage.GetReader(StorageCategory);
                    if (!(await reader.TryReadAllAsync<IdentityGroup>()).TryOut(out cache)) throw new Exception("Failed update cache from storage");
                }

                cache.Remove(groupId);
            }
            if (!await Storage.GetWriter(storageCategory).TryDeleteAsync(groupId)) throw new Exception($"Failed removing group {groupId} from storage");
        }

        string groupId;
        Dictionary<string, bool> memberIdentities = new Dictionary<string, bool>();

        [JsonIgnore]
        FeatureLock memberLock = new FeatureLock();

        [JsonIgnore]
        public string GroupId => groupId;

        [JsonIgnore]
        public string[] MemberIds
        {
            get
            {
                using (memberLock.LockReadOnly()) return memberIdentities.Keys.ToArray();
            }
        }

        [JsonIgnore]
        public string[] OwnerIds
        {
            get
            {
                using (memberLock.LockReadOnly()) return memberIdentities.Where(member => member.Value == true).Select(member => member.Key).ToArray();
            }
        }

        public IdentityGroup()
        {
        }

        public IdentityGroup(string groupId)
        {
            this.groupId = groupId;
        }

        public void AddGroupMember(string identityId, bool isGroupOwner, bool storeChanges)
        {
            using (memberLock.Lock())
            {
                memberIdentities[identityId] = isGroupOwner;
            }
            if (storeChanges) _ = StoreAsync();
        }

        public void RemoveGroupMember(string identityId, bool storeChanges)
        {
            using (memberLock.Lock())
            {
                memberIdentities.Remove(identityId);
            }
            if (storeChanges) _ = StoreAsync();
        }

        public bool IsOwner(string identityId)
        {
            using (memberLock.LockReadOnly())
            {
                return memberIdentities.TryGetValue(identityId, out bool isOwner) ? isOwner : false;
            }
        }

        public bool IsMember(string identityId)
        {
            using (memberLock.LockReadOnly())
            {
                return memberIdentities.ContainsKey(identityId);
            }
        }
    }
}


