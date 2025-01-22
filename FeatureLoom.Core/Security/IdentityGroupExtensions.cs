using FeatureLoom.Extensions;
using FeatureLoom.Storages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.Security
{
    public static class IdentityGroupExtensions
    {        
        public static async Task<IEnumerable<IdentityGroup>> GetMemberGroups(this Identity identity, bool forceUpdateCache)
        {
            var groups = await IdentityGroup.GetAllGroupsAsync(forceUpdateCache).ConfigureAwait(false);
            return groups.Where(group => group.IsMember(identity.IdentityId));
        }

        public static async Task<IEnumerable<IdentityGroup>> GetOwnedGroups(this Identity identity, bool forceUpdateCache)
        {
            var groups = await IdentityGroup.GetAllGroupsAsync(forceUpdateCache).ConfigureAwait(false);
            return groups.Where(group => group.IsOwner(identity.IdentityId));
        }

        public static async Task<bool> HasPermissionForItem(this Identity identity, string permission, IItemAccess item, bool ignoreGroups, bool forceUpdateCache)
        {
            if (item.HasIdentityPermission(permission, identity.IdentityId)) return true;
            if (ignoreGroups) return false;

            var groups = await identity.GetMemberGroups(forceUpdateCache).ConfigureAwait(false);
            return groups.Any(group => item.HasGroupPermission(permission, group.GroupId));
        }
    }
}

