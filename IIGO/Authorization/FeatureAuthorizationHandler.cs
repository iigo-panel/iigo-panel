using IIGO.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace IIGO.Authorization
{
    public class FeatureAuthorizationHandler : AuthorizationHandler<FeatureRequirement>
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public FeatureAuthorizationHandler(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, FeatureRequirement requirement)
        {
            if (context.User.IsInRole("Administrator"))
            {
                context.Succeed(requirement);
                return;
            }

            var roles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            if (roles.Count == 0)
                return;

            using var scope = _scopeFactory.CreateScope();
            var permissionService = scope.ServiceProvider.GetRequiredService<RolePermissionService>();

            if (await permissionService.RolesHaveFeatureAsync(roles, requirement.Feature))
            {
                context.Succeed(requirement);
            }
        }
    }
}
