namespace IIGO.Services
{
    public static class Feature
    {
        public const string ViewWebsites = "ViewWebsites";
        public const string EditWebsites = "EditWebsites";
        public const string CreateWebsites = "CreateWebsites";
        public const string ViewAppPools = "ViewAppPools";
        public const string EditAppPools = "EditAppPools";
        public const string CreateAppPools = "CreateAppPools";
        public const string ViewFirewallRules = "ViewFirewallRules";
        public const string EditFirewallRules = "EditFirewallRules";
        public const string ViewScheduledTasks = "ViewScheduledTasks";
        public const string EditScheduledTasks = "EditScheduledTasks";
        public const string ViewServices = "ViewServices";
        public const string ViewSettings = "ViewSettings";
        public const string ViewRoles = "ViewRoles";
        public const string ManageRoles = "ManageRoles";

        public static readonly IReadOnlyList<string> All =
        [
            ViewWebsites, EditWebsites, CreateWebsites,
            ViewAppPools, EditAppPools, CreateAppPools,
            ViewFirewallRules, EditFirewallRules,
            ViewScheduledTasks, EditScheduledTasks,
            ViewServices, ViewSettings,
            ViewRoles, ManageRoles
        ];
    }
}
