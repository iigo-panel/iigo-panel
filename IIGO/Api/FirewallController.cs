using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using WindowsFirewallHelper;

namespace IIGO.Api
{
    /// <summary>
    /// Windows Firewall rules: the same actions as the Firewall page.
    /// </summary>
    /// <remarks>
    /// Windows doesn't give rules a stable ID, so a rule is picked by name and, when several share a name,
    /// by direction too. A name that still matches more than one rule gets 409 Conflict.
    /// </remarks>
    [Route("api/firewall/rules")]
    public class FirewallController(ILogger<FirewallController> logger, FirewallService firewallService) : ApiControllerBase(logger)
    {
        /// <summary>Lists rules. Every word in <paramref name="search"/> has to match somewhere in the rule, like the page's filter.</summary>
        [HttpGet]
        public ActionResult<List<FirewallRuleDto>> GetRules([FromQuery] string? search = null)
        {
            try
            {
                var terms = (search ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return firewallService.GetRules()
                    .Where(rule => terms.All(rule.SearchText.Contains))
                    .Select(FirewallRuleDto.From)
                    .ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading firewall rules");
            }
        }

        [HttpPost]
        public ActionResult<FirewallRuleDto> AddRule([FromBody] FirewallRuleModel model)
        {
            var name = model.Name.Trim();
            try
            {
                firewallService.AddRule(model);
                Logger.LogWarning("API key {KeyName} added firewall rule {Name} ({Direction}, {Action})", KeyName, name, model.Direction, model.Action);

                // Several rules can share a name; the new one is the last match with these settings
                var added = firewallService.GetRules().LastOrDefault(x => x.Name == name && x.Direction == model.Direction && x.Action == model.Action);
                return added == null ? StatusCode(StatusCodes.Status201Created) : StatusCode(StatusCodes.Status201Created, FirewallRuleDto.From(added));
            }
            catch (Exception ex)
            {
                return Failed(ex, $"adding firewall rule {name}");
            }
        }

        /// <summary>Replaces every setting of the rule named <paramref name="name"/> with the body.</summary>
        [HttpPut]
        public ActionResult<FirewallRuleDto> UpdateRule([FromQuery, Required] string name, [FromQuery] FirewallDirection? direction, [FromBody] FirewallRuleModel model)
        {
            var (rule, error) = FindRule(name, direction);
            if (rule == null)
                return error!;

            try
            {
                firewallService.UpdateRule(rule.EditableRule!, model);
                Logger.LogWarning("API key {KeyName} changed firewall rule {Name} ({Direction})", KeyName, rule.Name, rule.Direction);

                var updated = firewallService.GetRules().FirstOrDefault(x => x.Name == model.Name.Trim() && x.Direction == model.Direction && x.Action == model.Action);
                return updated == null ? NoContent() : FirewallRuleDto.From(updated);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"changing firewall rule {name}");
            }
        }

        [HttpDelete]
        public IActionResult DeleteRule([FromQuery, Required] string name, [FromQuery] FirewallDirection? direction)
        {
            var (rule, error) = FindRule(name, direction);
            if (rule == null)
                return error!;

            try
            {
                firewallService.DeleteRule(rule.EditableRule!);
                Logger.LogWarning("API key {KeyName} deleted firewall rule {Name} ({Direction}, {Action})", KeyName, rule.Name, rule.Direction, rule.Action);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failed(ex, $"deleting firewall rule {name}");
            }
        }

        private (FirewallRuleInfo? Rule, ObjectResult? Error) FindRule(string name, FirewallDirection? direction)
        {
            var matches = firewallService.GetRules()
                .Where(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) && (direction == null || x.Direction == direction))
                .ToList();

            if (matches.Count == 0)
                return (null, NotFoundProblem($"No firewall rule named {name}{(direction == null ? "" : $" ({direction})")} was found"));
            if (matches.Count > 1)
                return (null, ConflictProblem($"{matches.Count} firewall rules are named {name}{(direction == null ? ". Add direction=Inbound or direction=Outbound to pick one." : $" ({direction}). Change them on the Firewall page.")}"));
            if (matches[0].EditableRule == null)
                return (null, ConflictProblem($"{name} can't be changed here"));

            return (matches[0], null);
        }

        public record FirewallRuleDto(
            string Name,
            string Description,
            bool Enabled,
            FirewallDirection Direction,
            FirewallAction Action,
            bool Domain,
            bool Private,
            bool Public,
            int Protocol,
            string ProtocolName,
            string LocalPorts,
            string RemotePorts,
            string LocalAddresses,
            string RemoteAddresses,
            string Program,
            bool Editable)
        {
            // Same field names as the request body, so a rule can be read, changed and sent back
            public static FirewallRuleDto From(FirewallRuleInfo info) => new(
                info.Name,
                info.Description,
                info.Enabled,
                info.Direction,
                info.Action,
                info.Profiles.HasFlag(FirewallProfiles.Domain),
                info.Profiles.HasFlag(FirewallProfiles.Private),
                info.Profiles.HasFlag(FirewallProfiles.Public),
                info.Protocol.ProtocolNumber,
                info.Protocol.ToString(),
                info.LocalPorts,
                info.RemotePorts,
                info.LocalAddresses,
                info.RemoteAddresses,
                info.Program,
                info.EditableRule != null);
        }
    }
}
