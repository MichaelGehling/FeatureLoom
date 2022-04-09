using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Storages;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.Security
{
    public class IdentityRole
    {
        #region static
        static string storageCategory = "Security/IdentityRoles";
        public static string StorageCategory { get => storageCategory; set => storageCategory = value; }

        public static Task<AsyncOut<bool, IdentityRole>> TryLoadIdentityRoleAsync(string roleName)
        {
            return Storage.GetReader(storageCategory).TryReadAsync<IdentityRole>(roleName);
        }
        #endregion

        string roleName;
        List<string> permissions = new List<string>();

        public IdentityRole(string roleName, IEnumerable<string> permissions = null)
        {
            this.roleName = roleName;
            if (permissions != null) this.permissions.AddRange(permissions);
        }

        public IdentityRole()
        {
        }

        [JsonIgnore]
        public string RoleName 
        { 
            get => roleName;
            set => roleName = value;
        }

        [JsonIgnore]
        public IEnumerable<string> Permissions => permissions;

        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool HasAnyPermission(string permissionWildcard = "*") => permissions.Any(permission => permission.MatchesWildcard(permissionWildcard));

        public Task<bool> TryStoreAsync()
        {
            return Storage.GetWriter(storageCategory).TryWriteAsync(roleName, this);
        }

        public void AddPermission(string permission)
        {
            if (!permissions.Contains(permission)) permissions.Add(permission);
        }

        public void RemovePermission(string permission)
        {
            permissions.Remove(permission);
        }
    }
}
