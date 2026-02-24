using Microsoft.AspNetCore.Authorization;

namespace IIGO.Authorization
{
    public class FeatureRequirement : IAuthorizationRequirement
    {
        public string Feature { get; }

        public FeatureRequirement(string feature)
        {
            Feature = feature;
        }
    }
}
