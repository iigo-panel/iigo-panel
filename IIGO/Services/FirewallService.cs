using System.ComponentModel.DataAnnotations;
using WindowsFirewallHelper;
using WindowsFirewallHelper.FirewallRules;

namespace IIGO.Services
{
    public class FirewallService
    {
        public List<FirewallRuleInfo> GetRules()
        {
            return [.. FirewallWAS.Instance.Rules.Select(FirewallRuleInfo.From).OrderBy(x => x.Name)];
        }

        public void AddRule(FirewallRuleModel model)
        {
            var rule = new FirewallWASRule(model.Name.Trim(), model.Action, model.Direction, model.Profiles);
            Apply(rule, model);
            FirewallWAS.Instance.Rules.Add(rule);
        }

        public void UpdateRule(FirewallWASRule rule, FirewallRuleModel model)
        {
            // Each property saves to the firewall as soon as it's set. Trying the changed values on a
            // scratch rule first means a bad value fails before the real rule is half changed
            var scratch = new FirewallWASRule(model.Name.Trim(), model.Action, model.Direction, model.Profiles) { Protocol = ToProtocol(model.Protocol) };
            Apply(scratch, model, rule);
            Apply(rule, model, rule);
        }

        public void DeleteRule(FirewallWASRule rule)
        {
            FirewallWAS.Instance.Rules.Remove(rule);
        }

        private static void Apply(FirewallWASRule rule, FirewallRuleModel model) => Apply(rule, model, rule);

        /// <summary>
        /// Sets on <paramref name="target"/> only the values that differ from <paramref name="current"/>. Built-in rules
        /// keep their indirect (@resource) names and special port keywords such as IPHTTPS unless they're changed
        /// </summary>
        private static void Apply(FirewallWASRule target, FirewallRuleModel model, FirewallWASRule current)
        {
            var native = target.GetCOMObject();
            var currentNative = current.GetCOMObject();
            var name = model.Name.Trim();
            var description = model.Description.Trim();
            var program = model.Program.Trim();

            if ((current.FriendlyName ?? "").Trim() != name)
                target.Name = name;
            if ((current.FriendlyDescription ?? "").Trim() != description)
                target.Description = description;
            if (current.IsEnable != model.Enabled)
                target.IsEnable = model.Enabled;
            if (current.Direction != model.Direction)
                target.Direction = model.Direction;
            if (current.Action != model.Action)
                target.Action = model.Action;
            if (current.Profiles != model.Profiles)
                target.Profiles = model.Profiles;
            if ((current.ApplicationName ?? "") != program)
                Set("Program", () => target.ApplicationName = program == "" ? null! : program);

            // Protocol goes before ports; changing to a protocol without ports clears them.
            // Ports are set again after a protocol change so they're checked against the new protocol
            bool protocolChanged = current.Protocol.ProtocolNumber != model.Protocol;
            if (protocolChanged)
                target.Protocol = ToProtocol(model.Protocol);

            if (model.HasPorts)
            {
                var localPorts = Compact(model.LocalPorts);
                var remotePorts = Compact(model.RemotePorts);
                if (protocolChanged || Compact(NativePorts(currentNative.LocalPorts)) != localPorts)
                    Set("Local ports", () => native.LocalPorts = localPorts);
                if (protocolChanged || Compact(NativePorts(currentNative.RemotePorts)) != remotePorts)
                    Set("Remote ports", () => native.RemotePorts = remotePorts);
            }

            var localAddresses = Compact(model.LocalAddresses);
            var remoteAddresses = Compact(model.RemoteAddresses);
            if (Compact(NativeAddresses(currentNative.LocalAddresses)) != localAddresses)
                Set("Local addresses", () => native.LocalAddresses = localAddresses == "" ? "*" : localAddresses);
            if (Compact(NativeAddresses(currentNative.RemoteAddresses)) != remoteAddresses)
                Set("Remote addresses", () => native.RemoteAddresses = remoteAddresses == "" ? "*" : remoteAddresses);
        }

        // TryParse because the public constructor takes a byte, and Any is 256
        private static FirewallProtocol ToProtocol(int protocolNumber) =>
            FirewallProtocol.TryParse(protocolNumber.ToString(), out var protocol) ? protocol : throw new ArgumentException($"Unknown protocol {protocolNumber}.");

        /// <summary>
        /// Windows only says "The parameter is incorrect", so name the field that was rejected
        /// </summary>
        private static void Set(string field, Action set)
        {
            try
            {
                set();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{field}: Windows rejected this value. {ex.Message}", ex);
            }
        }

        private static string Compact(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c))).Trim(',');

        internal static string NativePorts(string? ports) => ports is null or "*" ? "" : ports;

        internal static string NativeAddresses(string? addresses) => addresses is null or "*" ? "" : addresses;
    }

    /// <summary>
    /// A snapshot of a rule, so the list can be filtered without calling into the firewall for each row
    /// </summary>
    public record FirewallRuleInfo(
        IFirewallRule Rule,
        string Name,
        string Description,
        bool Enabled,
        FirewallDirection Direction,
        FirewallAction Action,
        FirewallProfiles Profiles,
        FirewallProtocol Protocol,
        FirewallScope Scope,
        string LocalPorts,
        string RemotePorts,
        string LocalAddresses,
        string RemoteAddresses,
        string Program)
    {
        // Only Windows Firewall with Advanced Security rules can be changed here
        public FirewallWASRule? EditableRule => Rule as FirewallWASRule;

        public string SearchText { get; } = string.Join(" ",
            Name, Description, Enabled ? "Enabled" : "Disabled", Direction, Action, Profiles, Protocol,
            LocalPorts, RemotePorts, LocalAddresses, RemoteAddresses, Program).ToLowerInvariant();

        public static FirewallRuleInfo From(IFirewallRule rule)
        {
            var native = (rule as FirewallWASRule)?.GetCOMObject();
            return new FirewallRuleInfo(
                rule,
                rule.FriendlyName ?? "",
                (rule as FirewallWASRule)?.FriendlyDescription ?? "",
                rule.IsEnable,
                rule.Direction,
                rule.Action,
                rule.Profiles,
                rule.Protocol,
                rule.Scope,
                native != null ? FirewallService.NativePorts(native.LocalPorts) : string.Join(",", rule.LocalPorts),
                native != null ? FirewallService.NativePorts(native.RemotePorts) : string.Join(",", rule.RemotePorts),
                native != null ? FirewallService.NativeAddresses(native.LocalAddresses) : "",
                native != null ? FirewallService.NativeAddresses(native.RemoteAddresses) : "",
                rule.ApplicationName ?? "");
        }
    }

    public class FirewallRuleModel : IValidatableObject
    {
        [Required(ErrorMessage = "Enter a name.")]
        [StringLength(255)]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public bool Enabled { get; set; } = true;

        public FirewallDirection Direction { get; set; } = FirewallDirection.Inbound;

        public FirewallAction Action { get; set; } = FirewallAction.Allow;

        public bool Domain { get; set; } = true;

        public bool Private { get; set; } = true;

        public bool Public { get; set; } = true;

        public int Protocol { get; set; } = FirewallProtocol.TCP.ProtocolNumber;

        public string LocalPorts { get; set; } = "";

        public string RemotePorts { get; set; } = "";

        public string LocalAddresses { get; set; } = "";

        public string RemoteAddresses { get; set; } = "";

        public string Program { get; set; } = "";

        public FirewallProfiles Profiles =>
            (Domain ? FirewallProfiles.Domain : 0) | (Private ? FirewallProfiles.Private : 0) | (Public ? FirewallProfiles.Public : 0);

        public bool HasPorts => Protocol == FirewallProtocol.TCP.ProtocolNumber || Protocol == FirewallProtocol.UDP.ProtocolNumber;

        public static FirewallRuleModel From(FirewallRuleInfo info) => new()
        {
            Name = info.Name,
            Description = info.Description,
            Enabled = info.Enabled,
            Direction = info.Direction,
            Action = info.Action,
            Domain = info.Profiles.HasFlag(FirewallProfiles.Domain),
            Private = info.Profiles.HasFlag(FirewallProfiles.Private),
            Public = info.Profiles.HasFlag(FirewallProfiles.Public),
            Protocol = info.Protocol.ProtocolNumber,
            LocalPorts = info.LocalPorts.Replace(",", ", "),
            RemotePorts = info.RemotePorts.Replace(",", ", "),
            LocalAddresses = info.LocalAddresses.Replace(",", ", "),
            RemoteAddresses = info.RemoteAddresses.Replace(",", ", "),
            Program = info.Program
        };

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Profiles == 0)
                yield return new ValidationResult("Select at least one profile.", [nameof(Domain)]);
        }
    }
}
