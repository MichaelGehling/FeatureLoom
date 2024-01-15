using System.Collections.Generic;

namespace FeatureLoom.Security
{
    public interface IItemAccess
    {
        IEnumerable<string> Groups { get; }
        IEnumerable<string> Identities { get; }
        IEnumerable<string> Permissions { get; }

        void AddGroupPermission(string permission, string groupId);
        void AddIdentityPermission(string permission, string identityId);
        bool RemoveGroupPermission(string permission, string groupId);
        bool RemoveIdentityPermission(string permission, string identityId);
        void ClearGroupsWithPermission(string permission);
        void ClearIdentitiesWithPermission(string permission);
        IEnumerable<string> GetGroupsWithPermission(string permission);
        IEnumerable<string> GetIdentitiesWithPermission(string permission);
        bool HasGroupPermission(string permission, string groupId);
        bool HasIdentityPermission(string permission, string identityId);        
    }
}