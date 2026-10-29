using IIGO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace IIGO.Api
{
    /// <summary>
    /// Server-wide IP address rules (system.webServer/security/ipSecurity in applicationHost.config).
    /// Ported from mws-utilities/IISManager, with the same routes and request bodies.
    /// </summary>
    // The fallback policy requires a signed-in user; the API uses keys instead, checked by RequireApiKey
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    [RequireApiKey]
    public class IPSecurityRulesController(ILogger<IPSecurityRulesController> logger, IISService IISService) : ControllerBase
    {
        /// <summary>Returns IP address → "True" (allowed) or "False" (denied).</summary>
        [HttpGet]
        public ActionResult<Dictionary<string, string>> GetIPSecurityRules()
        {
            try
            {
                return IISService.GetIPRules();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading IP security rules");
                return Problem(statusCode: 500, detail: "Error loading IP security rules");
            }
        }

        /// <summary>Adds a rule (does nothing if the address already has one) and returns all rules.</summary>
        [HttpPost]
        public ActionResult<Dictionary<string, string>> AddIPSecurityRule([FromBody] IPAddressModel ip)
        {
            try
            {
                IISService.AddIPRule(ip.IPAddress, ip.Block);
                logger.LogInformation("API key {KeyName} added {RuleType} rule for {IPAddress}", KeyName, ip.Block ? "deny" : "allow", ip.IPAddress);
                return IISService.GetIPRules();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding IP security rule for {IPAddress}", ip.IPAddress);
                return Problem(statusCode: 500, detail: "Error adding IP security rule");
            }
        }

        /// <summary>Removes the rule for an address and returns all rules. Only IPAddress is used from the body.</summary>
        [HttpDelete]
        public ActionResult<Dictionary<string, string>> DeleteIPSecurityRule([FromBody] IPAddressModel ip)
        {
            try
            {
                IISService.DeleteIPRule(ip.IPAddress);
                logger.LogInformation("API key {KeyName} deleted the rule for {IPAddress}", KeyName, ip.IPAddress);
                return IISService.GetIPRules();
            }
            catch (InvalidOperationException)
            {
                // IISWrapper throws this when no rule matches the address
                return Problem(statusCode: 404, detail: $"No IP security rule exists for {ip.IPAddress}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting IP security rule for {IPAddress}", ip.IPAddress);
                return Problem(statusCode: 500, detail: "Error deleting IP security rule");
            }
        }

        string KeyName => HttpContext.Items[RequireApiKeyAttribute.KeyNameItem] as string ?? "(unknown)";

        public class IPAddressModel : IValidatableObject
        {
            [Required]
            public string IPAddress { get; set; } = "";

            // true adds a deny rule, false an allow rule (same default as the old IISManager API)
            public bool Block { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (!System.Net.IPAddress.TryParse(IPAddress, out _))
                    yield return new ValidationResult($"'{IPAddress}' is not a valid IPv4 or IPv6 address.", [nameof(IPAddress)]);
            }
        }
    }
}
