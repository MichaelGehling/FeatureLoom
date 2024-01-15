using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace FeatureLoom.Security
{
    public class ItemAccessHelper : IItemAccess
    {
        Dictionary<string, HashSet<string>> permissionToIdentityIds = new();
        Dictionary<string, HashSet<string>> permissionToGroupIds = new();

        [JsonIgnore]
        FeatureLock accessLock = new FeatureLock();

        [JsonIgnore]
        public IEnumerable<string> Permissions
        {
            get
            {
                using (accessLock.LockReadOnly())
                {
                    return permissionToIdentityIds.Keys.Union(permissionToGroupIds.Keys);
                }
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Identities
        {
            get
            {
                using (accessLock.LockReadOnly())
                {
                    return permissionToIdentityIds.Values.SelectMany(users => users).Distinct();
                }
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Groups
        {
            get
            {
                using (accessLock.LockReadOnly())
                {
                    return permissionToGroupIds.Values.SelectMany(users => users).Distinct();
                }
            }
        }

        public IEnumerable<string> GetIdentitiesWithPermission(string permission)
        {
            using (accessLock.LockReadOnly())
            {
                if (permissionToIdentityIds.TryGetValue(permission, out var result)) return result;
                else return Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> GetGroupsWithPermission(string permission)
        {
            using (accessLock.LockReadOnly())
            {
                if (permissionToGroupIds.TryGetValue(permission, out var result)) return result;
                else return Enumerable.Empty<string>();
            }
        }

        public bool HasIdentityPermission(string permission, string identityId)
        {
            using (accessLock.LockReadOnly())
            {
                if (!permissionToIdentityIds.TryGetValue(permission, out var identityIds)) return false;
                return identityIds.Contains(identityId);
            }
        }

        public bool HasGroupPermission(string permission, string groupId)
        {
            using (accessLock.LockReadOnly())
            {
                if (!permissionToGroupIds.TryGetValue(permission, out var groupIds)) return false;
                return groupIds.Contains(groupId);
            }
        }

        public void AddIdentityPermission(string permission, string identityId)
        {
            using (accessLock.Lock())
            {
                if (!permissionToIdentityIds.TryGetValue(permission, out var identityIds))
                {
                    identityIds = new();
                    permissionToIdentityIds[permission] = identityIds;
                }
                identityIds.Add(identityId);
            }
        }

        public void AddGroupPermission(string permission, string groupId)
        {
            using (accessLock.Lock())
            {
                if (!permissionToGroupIds.TryGetValue(permission, out var groupIds))
                {
                    groupIds = new();
                    permissionToGroupIds[permission] = groupIds;
                }
                groupIds.Add(groupId);
            }
        }

        public bool RemoveGroupPermission(string permission, string groupId)
        {
            using (accessLock.Lock())
            {
                if (!permissionToGroupIds.TryGetValue(permission, out var groupIds)) return false;
                return groupIds.Remove(groupId);
            }
        }

        public bool RemoveIdentityPermission(string permission, string identityId)
        {
            using (accessLock.Lock())
            {
                if (!permissionToIdentityIds.TryGetValue(permission, out var identityIds)) return false;
                return identityIds.Remove(identityId);
            }
        }

        public void ClearGroupsWithPermission(string permission)
        {
            using (accessLock.Lock())
            {
                if (!permissionToGroupIds.TryGetValue(permission, out var groupIds)) return;
                groupIds.Clear();
            }
        }

        public void ClearIdentitiesWithPermission(string permission)
        {
            using (accessLock.Lock())
            {
                if (!permissionToIdentityIds.TryGetValue(permission, out var identityIds)) return;
                identityIds.Clear();
            }
        }
    }
}


