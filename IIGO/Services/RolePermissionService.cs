using IIGO.Data;
using Microsoft.EntityFrameworkCore;

namespace IIGO.Services
{
    public class RolePermissionService(ApplicationDbContext context)
    {
        public async Task<bool> RolesHaveFeatureAsync(IEnumerable<string> roleNames, string feature)
        {
            var roleIds = await context.Roles
                .Where(r => roleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            return await context.RolePermission
                .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.Feature == feature);
        }

        public async Task<List<string>> GetRoleFeaturesAsync(string roleId)
        {
            return await context.RolePermission
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Feature)
                .ToListAsync();
        }

        public async Task SetRoleFeaturesAsync(string roleId, IEnumerable<string> features)
        {
            var existing = await context.RolePermission
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            context.RolePermission.RemoveRange(existing);

            foreach (var feature in features)
            {
                context.RolePermission.Add(new RolePermission { RoleId = roleId, Feature = feature });
            }

            await context.SaveChangesAsync();
        }
    }
}
