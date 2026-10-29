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
        // Viewing only; creating roles and changing their permissions is limited to the Administrator role,
        // since anyone who can change permissions can grant themselves everything
        public const string ViewRoles = "ViewRoles";
        // Viewing only; creating, disabling and deleting keys is limited to the Administrator role
        public const string ViewApiKeys = "ViewApiKeys";
        // System status and the process list, which shows every process's command line
        public const string ViewSystemStatus = "ViewSystemStatus";

        public static readonly IReadOnlyList<string> All =
        [
            ViewWebsites, EditWebsites, CreateWebsites,
            ViewAppPools, EditAppPools, CreateAppPools,
            ViewFirewallRules, EditFirewallRules,
            ViewScheduledTasks, EditScheduledTasks,
            ViewServices, ViewSettings,
            ViewRoles,
            ViewApiKeys,
            ViewSystemStatus
        ];
    }
}
