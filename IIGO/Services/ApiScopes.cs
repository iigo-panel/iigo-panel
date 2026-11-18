namespace IIGO.Services
{
    /// <summary>Access levels, lowest first. Each level includes the ones below it.</summary>
    public enum ApiAccess
    {
        None,
        Read,
        // Start, stop, recycle, run: actions that don't create, change or delete anything
        Operate,
        Write
    }

    /// <summary>
    /// A group of API endpoints that a key can be given access to.
    /// OperateLabel names the operate level, or is null when the area has no operate actions.
    /// HasWrite is false when every change the area allows is an operate action.
    /// </summary>
    public record ApiArea(string Name, string Label, string Description, string? OperateLabel = null, bool HasWrite = true)
    {
        public ApiAccess MaxAccess => HasWrite ? ApiAccess.Write : OperateLabel != null ? ApiAccess.Operate : ApiAccess.Read;

        public IEnumerable<ApiAccess> Levels =>
            Enum.GetValues<ApiAccess>().Where(x => x <= MaxAccess && (x != ApiAccess.Operate || OperateLabel != null));

        public string LevelLabel(ApiAccess access) => access switch
        {
            ApiAccess.None => "No access",
            ApiAccess.Read => "Read",
            ApiAccess.Operate => OperateLabel ?? "Read",
            _ => OperateLabel != null ? "Everything" : "Read & write"
        };

        /// <summary>
        /// The level a stored scope gives in this area. Levels the area doesn't have round down, so
        /// "services:write" (all of whose changes are operate actions) means operate.
        /// </summary>
        public ApiAccess Clamp(ApiAccess access) =>
            access > MaxAccess ? MaxAccess
            : access == ApiAccess.Operate && OperateLabel == null ? ApiAccess.Read
            : access;
    }

    /// <summary>
    /// What an API key may call. Stored on the key as a space-separated list such as
    /// "websites:read apppools:operate", or "*" for full access. GET requests need read access to the
    /// controller's area, actions marked with ApiAccessLevel need that level, and anything else needs write.
    /// </summary>
    public static class ApiScopes
    {
        public const string FullAccess = "*";

        public const string Websites = "websites";
        public const string AppPools = "apppools";
        public const string Firewall = "firewall";
        public const string IPSecurity = "ipsecurity";
        public const string ScheduledTasks = "tasks";
        public const string Services = "services";
        public const string Monitors = "monitors";
        public const string Updates = "updates";

        public static readonly IReadOnlyList<ApiArea> Areas =
        [
            new(Websites, "Websites", "Sites, bindings, applications, virtual directories and certificates",
                OperateLabel: "Read, start, stop & restart"),
            new(AppPools, "App pools", "Everything also adds and deletes pools",
                OperateLabel: "Read, start, stop & recycle"),
            new(Firewall, "Firewall", "Windows Firewall rules"),
            new(IPSecurity, "IP restrictions", "Server-wide IIS allow and deny rules"),
            new(ScheduledTasks, "Scheduled tasks", "Everything also adds, changes, enables, disables and deletes tasks",
                OperateLabel: "Read, run & stop"),
            new(Services, "Services", "The API can't add, change, stop or delete services",
                OperateLabel: "Read, start & restart", HasWrite: false),
            new(Monitors, "Monitors", "App Pool and HTTPS monitor status, events and settings"),
            new(Updates, "Windows updates", "Installing is only on the Check Updates page",
                OperateLabel: "Read & check for updates", HasWrite: false),
        ];

        static readonly Dictionary<string, ApiArea> AreasByName = Areas.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> Split(string? scopes) =>
            (scopes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public static bool IsFullAccess(string? scopes) => Split(scopes).Contains(FullAccess);

        /// <summary>Access per area. Entries for areas or levels this version doesn't know are ignored.</summary>
        public static Dictionary<string, ApiAccess> Parse(string? scopes)
        {
            var result = Areas.ToDictionary(x => x.Name, _ => ApiAccess.None, StringComparer.OrdinalIgnoreCase);
            foreach (var scope in Split(scopes))
            {
                var parts = scope.Split(':', 2);
                if (parts.Length != 2 || !AreasByName.TryGetValue(parts[0], out var area))
                    continue;
                // Names only; Enum.TryParse would also accept numbers
                if (!parts[1].All(char.IsLetter) || !Enum.TryParse<ApiAccess>(parts[1], ignoreCase: true, out var access))
                    continue;

                access = area.Clamp(access);
                if (access > result[area.Name])
                    result[area.Name] = access;
            }
            return result;
        }

        public static string Format(bool fullAccess, IReadOnlyDictionary<string, ApiAccess> access) =>
            fullAccess
                ? FullAccess
                : string.Join(' ', Areas
                    .Select(a => (a.Name, Access: a.Clamp(access.GetValueOrDefault(a.Name))))
                    .Where(x => x.Access != ApiAccess.None)
                    .Select(x => $"{x.Name}:{x.Access.ToString().ToLowerInvariant()}"));

        /// <summary>
        /// Whether a key with these scopes may make a request needing this access to an area.
        /// An endpoint with no area can only be called with full access.
        /// </summary>
        public static bool Allows(string? scopes, string? area, ApiAccess needed)
        {
            if (IsFullAccess(scopes))
                return true;
            if (string.IsNullOrEmpty(area))
                return false;

            return Parse(scopes).GetValueOrDefault(area) >= needed;
        }

        /// <summary>Readable summary for the API Keys page and logs</summary>
        public static string Describe(string? scopes)
        {
            if (IsFullAccess(scopes))
                return "Full access";

            var access = Parse(scopes);
            var parts = Areas
                .Where(a => access[a.Name] != ApiAccess.None)
                .Select(a => $"{a.Label} ({a.LevelLabel(access[a.Name]).ToLowerInvariant()})")
                .ToList();
            return parts.Count == 0 ? "No access" : string.Join(", ", parts);
        }
    }

    /// <summary>The permission choices on the API Keys page</summary>
    public class ApiScopeSelection
    {
        public bool FullAccess { get; set; }

        public Dictionary<string, ApiAccess> Access { get; set; } = ApiScopes.Parse(null);

        public bool IsEmpty => !FullAccess && Access.Values.All(x => x == ApiAccess.None);

        public string ToScopes() => ApiScopes.Format(FullAccess, Access);

        public static ApiScopeSelection FromScopes(string? scopes) => new()
        {
            FullAccess = ApiScopes.IsFullAccess(scopes),
            Access = ApiScopes.Parse(scopes)
        };
    }
}
