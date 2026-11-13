using IIGO.Data;
using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Web.Administration;
using System.ComponentModel.DataAnnotations;

namespace IIGO.Api
{
    /// <summary>
    /// IIS websites: the same actions as the Websites and website details pages.
    /// </summary>
    [Route("api/websites")]
    public class WebsitesController(ILogger<WebsitesController> logger, IISService IISService) : ApiControllerBase(logger)
    {
        [HttpGet]
        public ActionResult<List<IISSiteInfo>> GetWebsites()
        {
            try
            {
                return IISService.GetSites().Select(ToSiteInfo).ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading websites");
            }
        }

        [HttpGet("{id:long}")]
        public ActionResult<WebsiteDetail> GetWebsite(long id)
        {
            try
            {
                var site = FindSite(id);
                return site == null ? NotFoundProblem($"Website {id} was not found") : ToDetail(site);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"loading website {id}");
            }
        }

        /// <summary>Certificates that can be used on https bindings.</summary>
        [HttpGet("certificates")]
        public ActionResult<List<CertificateInfo>> GetCertificates()
        {
            try
            {
                return IISService.GetCertificates();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading certificates");
            }
        }

        /// <summary>
        /// Creates a site with one http binding. The application pool is created if it doesn't exist.
        /// Add https bindings afterwards so a certificate is attached.
        /// </summary>
        [HttpPost]
        public ActionResult<WebsiteDetail> CreateWebsite([FromBody] CreateWebsiteRequest request)
        {
            var name = request.Name.Trim();
            var appPool = string.IsNullOrWhiteSpace(request.ApplicationPool) ? name : request.ApplicationPool.Trim();
            try
            {
                if (IISService.GetSites().Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return ConflictProblem($"A website named {name} already exists");

                IISService.AddWebsite(name, appPool, request.PhysicalPath.Trim(), request.BindingInformation, "http");
                Logger.LogWarning("API key {KeyName} created site {SiteName}", KeyName, name);

                var site = IISService.GetSites().FirstOrDefault(x => x.Name == name);
                return site == null
                    ? StatusCode(StatusCodes.Status201Created)
                    : CreatedAtAction(nameof(GetWebsite), new { id = site.Id }, ToDetail(site));
            }
            catch (Exception ex)
            {
                return Failed(ex, $"creating website {name}");
            }
        }

        [HttpDelete("{id:long}")]
        public IActionResult DeleteWebsite(long id)
        {
            var site = FindSite(id);
            if (site == null)
                return NotFoundProblem($"Website {id} was not found");

            try
            {
                IISService.DeleteWebsite(id);
                Logger.LogWarning("API key {KeyName} deleted site {SiteName} ({SiteId})", KeyName, site.Name, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failed(ex, $"deleting website {id}");
            }
        }

        [HttpPost("{id:long}/start")]
        public IActionResult StartWebsite(long id) => ChangeState(id, "started", IISService.StartSite);

        [HttpPost("{id:long}/stop")]
        public IActionResult StopWebsite(long id) => ChangeState(id, "stopped", IISService.StopSite);

        [HttpPost("{id:long}/restart")]
        public IActionResult RestartWebsite(long id) => ChangeState(id, "restarted", IISService.RestartSite);

        private IActionResult ChangeState(long id, string done, Action<long> action)
        {
            var site = FindSite(id);
            if (site == null)
                return NotFoundProblem($"Website {id} was not found");

            try
            {
                action(id);
                Logger.LogInformation("API key {KeyName} {Action} site {SiteName} ({SiteId})", KeyName, done, site.Name, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failed(ex, $"changing the state of website {id}");
            }
        }

        /// <summary>Sets the root application's physical path and connect-as user.</summary>
        [HttpPut("{id:long}/path")]
        public ActionResult<WebsiteDetail> UpdatePath(long id, [FromBody] PathRequest request) =>
            EditSite(id, "updated the physical path of", () =>
                IISService.UpdateSitePath(id, request.PhysicalPath.Trim(), NullIfBlank(request.UserName), NullIfBlank(request.UserName) == null ? null : request.Password));

        [HttpPost("{id:long}/bindings")]
        public ActionResult<WebsiteDetail> AddBinding(long id, [FromBody] BindingRequest request)
        {
            var (store, thumbprint, error) = ResolveCertificate(request, usesCentralStore: false);
            if (error != null)
                return error;

            return EditSite(id, $"added binding {request.Protocol} {request.BindingInformation} to", () =>
                IISService.AddBinding(id, request.Protocol, request.BindingInformation, thumbprint, store, request.RequireSni));
        }

        /// <summary>Replaces the binding identified by OriginalProtocol and OriginalBindingInformation.</summary>
        [HttpPut("{id:long}/bindings")]
        public ActionResult<WebsiteDetail> UpdateBinding(long id, [FromBody] UpdateBindingRequest request)
        {
            var site = FindSite(id);
            if (site == null)
                return NotFoundProblem($"Website {id} was not found");

            var existing = site.Bindings.FirstOrDefault(x => x.Protocol == request.OriginalProtocol && string.Equals(x.BindingInformation, request.OriginalBindingInformation, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                return NotFoundProblem($"Binding {request.OriginalProtocol} {request.OriginalBindingInformation} was not found");

            // Centralized certificate store bindings keep their certificate source; no certificate is needed
            var usesCentralStore = existing.Protocol == "https" && existing.SslFlags.HasFlag(SslFlags.CentralCertStore);
            var (store, thumbprint, error) = ResolveCertificate(request, usesCentralStore);
            if (error != null)
                return error;

            return EditSite(id, $"changed binding {request.OriginalProtocol} {request.OriginalBindingInformation} to {request.Protocol} {request.BindingInformation} on", () =>
                IISService.UpdateBinding(id, request.OriginalProtocol, request.OriginalBindingInformation, request.Protocol, request.BindingInformation, thumbprint, store, request.RequireSni));
        }

        [HttpDelete("{id:long}/bindings")]
        public ActionResult<WebsiteDetail> DeleteBinding(long id, [FromQuery, Required] string protocol, [FromQuery, Required] string bindingInformation) =>
            EditSite(id, $"deleted binding {protocol} {bindingInformation} from", () => IISService.DeleteBinding(id, protocol, bindingInformation));

        [HttpPost("{id:long}/applications")]
        public ActionResult<WebsiteDetail> AddApplication(long id, [FromBody] AddApplicationRequest request) =>
            EditSite(id, $"added application {request.Alias} to", () =>
                IISService.AddApplication(id, request.Alias, request.PhysicalPath.Trim(), request.ApplicationPool, NullIfBlank(request.UserName), NullIfBlank(request.UserName) == null ? null : request.Password, request.PreloadEnabled));

        /// <summary>Changes the application at <paramref name="path"/>, e.g. "/sales".</summary>
        [HttpPut("{id:long}/applications")]
        public ActionResult<WebsiteDetail> UpdateApplication(long id, [FromQuery, Required] string path, [FromBody] ApplicationRequest request) =>
            EditSite(id, $"changed application {path} on", () =>
                IISService.UpdateApplication(id, path, request.PhysicalPath.Trim(), request.ApplicationPool, NullIfBlank(request.UserName), NullIfBlank(request.UserName) == null ? null : request.Password, request.PreloadEnabled));

        /// <summary>Removes the application at <paramref name="path"/>. Its files are not deleted.</summary>
        [HttpDelete("{id:long}/applications")]
        public ActionResult<WebsiteDetail> DeleteApplication(long id, [FromQuery, Required] string path) =>
            EditSite(id, $"deleted application {path} from", () => IISService.DeleteApplication(id, path));

        /// <summary>Sets the protocols the root application accepts, e.g. "http" or "http,net.tcp".</summary>
        [HttpPut("{id:long}/enabled-protocols")]
        public ActionResult<WebsiteDetail> UpdateEnabledProtocols(long id, [FromBody] EnabledProtocolsSettings request) =>
            EditSite(id, "changed the enabled protocols of", () => IISService.UpdateSite(id, request.Apply));

        /// <summary>Sets bandwidth, connection and URL segment limits. Turn a limit off with LimitBandwidth or LimitConnections.</summary>
        [HttpPut("{id:long}/limits")]
        public ActionResult<WebsiteDetail> UpdateLimits(long id, [FromBody] SiteLimitsSettings request) =>
            EditSite(id, "changed the limits of", () => IISService.UpdateSite(id, request.Apply));

        [HttpPut("{id:long}/failed-request-tracing")]
        public ActionResult<WebsiteDetail> UpdateFailedRequestTracing(long id, [FromBody] FailedRequestTracingSettings request) =>
            EditSite(id, "changed failed request tracing on", () => IISService.UpdateSite(id, request.Apply));

        /// <summary>Sets HTTP Strict Transport Security. Returns 409 when this server's IIS doesn't support HSTS.</summary>
        [HttpPut("{id:long}/hsts")]
        public ActionResult<WebsiteDetail> UpdateHsts(long id, [FromBody] HstsSettings request) =>
            EditSite(id, "changed HSTS on", () => IISService.UpdateSite(id, request.Apply));

        /// <summary>
        /// Adds a virtual directory at VirtualPath, e.g. "/images". It goes under the application that contains the path.
        /// </summary>
        [HttpPost("{id:long}/virtual-directories")]
        public ActionResult<WebsiteDetail> AddVirtualDirectory(long id, [FromBody] AddVirtualDirectoryRequest request) =>
            EditSite(id, $"added virtual directory {request.VirtualPath} to", () =>
                IISService.AddVirtualDirectory(id, request.VirtualPath, request.PhysicalPath.Trim(), NullIfBlank(request.UserName), NullIfBlank(request.UserName) == null ? null : request.Password));

        /// <summary>Changes the virtual directory at <paramref name="path"/>, its path from the site root, e.g. "/sales/images".</summary>
        [HttpPut("{id:long}/virtual-directories")]
        public ActionResult<WebsiteDetail> UpdateVirtualDirectory(long id, [FromQuery, Required] string path, [FromBody] PathRequest request) =>
            EditSite(id, $"changed virtual directory {path} on", () =>
                IISService.UpdateVirtualDirectory(id, path, request.PhysicalPath.Trim(), NullIfBlank(request.UserName), NullIfBlank(request.UserName) == null ? null : request.Password));

        /// <summary>Removes the virtual directory at <paramref name="path"/>. Its files are not deleted.</summary>
        [HttpDelete("{id:long}/virtual-directories")]
        public ActionResult<WebsiteDetail> DeleteVirtualDirectory(long id, [FromQuery, Required] string path) =>
            EditSite(id, $"deleted virtual directory {path} from", () => IISService.DeleteVirtualDirectory(id, path));

        /// <summary>
        /// Runs a change to a site and returns the site as it is afterwards
        /// </summary>
        private ActionResult<WebsiteDetail> EditSite(long id, string description, Action change)
        {
            var site = FindSite(id);
            if (site == null)
                return NotFoundProblem($"Website {id} was not found");

            try
            {
                change();
                Logger.LogInformation("API key {KeyName} {Change} site {SiteName} ({SiteId})", KeyName, description, site.Name, id);
                var updated = FindSite(id);
                return updated == null ? NoContent() : ToDetail(updated);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"changing website {id}");
            }
        }

        /// <summary>
        /// Finds the store for an https binding's certificate. The store is optional in the request; the certificate is
        /// looked up by thumbprint among those IIS can bind to.
        /// </summary>
        private (string? Store, string? Thumbprint, ActionResult? Error) ResolveCertificate(BindingRequest request, bool usesCentralStore)
        {
            if (request.Protocol != "https" || usesCentralStore)
                return (null, null, null);

            if (string.IsNullOrWhiteSpace(request.CertificateThumbprint))
                return (null, null, Problem(statusCode: StatusCodes.Status400BadRequest, detail: "CertificateThumbprint is required for https bindings"));

            var thumbprint = request.CertificateThumbprint.Replace(" ", "").Trim();
            var certificate = IISService.GetCertificates().FirstOrDefault(x =>
                x.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(request.CertificateStore) || x.StoreName.Equals(request.CertificateStore.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (certificate == null)
                return (null, null, Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"No certificate with a private key and thumbprint {thumbprint} was found{(string.IsNullOrWhiteSpace(request.CertificateStore) ? "" : $" in the {request.CertificateStore} store")}"));

            return (certificate.StoreName, certificate.Thumbprint, null);
        }

        // Read from a fresh copy of the config, so changes just made show up
        private Site? FindSite(long id) => IISService.GetSites().FirstOrDefault(x => x.Id == id);

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        internal static IISSiteInfo ToSiteInfo(Site site) => new()
        {
            Id = site.Id,
            Name = site.Name,
            State = site.State.ToString(),
            ApplicationPool = site.Applications.Count > 0 ? site.Applications[0].ApplicationPoolName : "",
            PhysicalPath = site.Applications.Count > 0 && site.Applications[0].VirtualDirectories.Count > 0
                ? site.Applications[0].VirtualDirectories[0].PhysicalPath
                : "",
            Bindings = [.. site.Bindings.Select(b => new IISBindingInfo { Protocol = b.Protocol, BindingInformation = b.BindingInformation })]
        };

        private static WebsiteDetail ToDetail(Site site) => new(
            site.Id,
            site.Name,
            site.State.ToString(),
            site.ServerAutoStart,
            [.. site.Bindings.Select(ToBindingDetail)],
            [.. site.Applications.Select(ToApplicationDetail)],
            [.. IISService.GetVirtualDirectories(site).Select(x => new VirtualDirectoryDetail(x.Path, x.Application.Path, x.Directory.PhysicalPath, string.IsNullOrEmpty(x.Directory.UserName) ? null : x.Directory.UserName))],
            EnabledProtocolsSettings.From(site).EnabledProtocols,
            SiteLimitsSettings.From(site),
            FailedRequestTracingSettings.From(site),
            HstsSettings.From(site));

        private static BindingDetail ToBindingDetail(Binding binding)
        {
            bool https = binding.Protocol == "https";
            bool web = https || binding.Protocol == "http";
            return new BindingDetail(
                binding.Protocol,
                binding.BindingInformation,
                web ? binding.Host : "",
                https && binding.SslFlags.HasFlag(SslFlags.Sni),
                https && binding.SslFlags.HasFlag(SslFlags.CentralCertStore),
                https && binding.CertificateHash is { Length: > 0 } hash ? Convert.ToHexString(hash) : null,
                https ? binding.CertificateStoreName : null);
        }

        private static ApplicationDetail ToApplicationDetail(Application app)
        {
            var dir = app.VirtualDirectories.FirstOrDefault(x => x.Path == "/") ?? app.VirtualDirectories.FirstOrDefault();
            return new ApplicationDetail(
                app.Path,
                dir?.PhysicalPath ?? "",
                app.ApplicationPoolName,
                app.GetAttributeValue("preloadEnabled") is true,
                string.IsNullOrEmpty(dir?.UserName) ? null : dir.UserName);
        }

        // Hsts is null when this server's IIS doesn't support HSTS
        public record WebsiteDetail(
            long Id,
            string Name,
            string State,
            bool ServerAutoStart,
            List<BindingDetail> Bindings,
            List<ApplicationDetail> Applications,
            List<VirtualDirectoryDetail> VirtualDirectories,
            string EnabledProtocols,
            SiteLimitsSettings Limits,
            FailedRequestTracingSettings FailedRequestTracing,
            HstsSettings? Hsts);

        public record BindingDetail(string Protocol, string BindingInformation, string Host, bool RequireSni, bool CentralCertStore, string? CertificateThumbprint, string? CertificateStore);

        // ConnectAsUser is null for pass-through authentication. Passwords are never returned.
        public record ApplicationDetail(string Path, string PhysicalPath, string ApplicationPool, bool PreloadEnabled, string? ConnectAsUser);

        // Path is from the site root, e.g. "/sales/images". Application is the path of the application it belongs to.
        public record VirtualDirectoryDetail(string Path, string Application, string PhysicalPath, string? ConnectAsUser);

        public class CreateWebsiteRequest : IValidatableObject
        {
            [Required]
            public string Name { get; set; } = "";

            // Only http. An https binding needs a certificate, so it's added afterwards through the bindings endpoint.
            public string Protocol { get; set; } = "http";

            // Defaults to the site name, like the Add Website page. Created if it doesn't exist.
            public string? ApplicationPool { get; set; }

            [Required]
            public string PhysicalPath { get; set; } = "";

            public string IPAddress { get; set; } = "*";

            [Range(1, 65535)]
            public int Port { get; set; } = 80;

            [RegularExpression(BindingRequest.HostNamePattern, ErrorMessage = "Enter a valid host name, or leave it blank.")]
            public string HostName { get; set; } = "";

            internal string BindingInformation => $"{IPAddress.Trim()}:{Port}:{HostName.Trim()}";

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Protocol != "http")
                    yield return new ValidationResult("Create the site with an http binding, then add the https binding with POST /api/websites/{id}/bindings so a certificate is attached.", [nameof(Protocol)]);
            }
        }

        public class PathRequest
        {
            [Required]
            public string PhysicalPath { get; set; } = "";

            // Blank for pass-through authentication
            public string? UserName { get; set; }

            // Blank keeps the saved password when UserName is unchanged
            public string? Password { get; set; }
        }

        public class BindingRequest : IValidatableObject
        {
            internal const string HostNamePattern = @"^(\*\.)?([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$";

            public string Protocol { get; set; } = "http";

            public string IPAddress { get; set; } = "*";

            [Range(1, 65535)]
            public int Port { get; set; } = 80;

            [RegularExpression(HostNamePattern, ErrorMessage = "Enter a valid host name, or leave it blank.")]
            public string HostName { get; set; } = "";

            // Server Name Indication (https only)
            public bool RequireSni { get; set; }

            // Required for https, except on bindings that use the centralized certificate store
            public string? CertificateThumbprint { get; set; }

            // "My" or "WebHosting". Optional; found from the thumbprint when blank.
            public string? CertificateStore { get; set; }

            internal string BindingInformation => $"{IPAddress.Trim()}:{Port}:{HostName.Trim()}";

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Protocol != "http" && Protocol != "https")
                    yield return new ValidationResult("Protocol must be http or https.", [nameof(Protocol)]);
                if (Protocol == "https" && RequireSni && HostName.Trim() == "")
                    yield return new ValidationResult("A host name is required when Server Name Indication is on.", [nameof(HostName)]);
            }
        }

        public class UpdateBindingRequest : BindingRequest
        {
            [Required]
            public string OriginalProtocol { get; set; } = "";

            [Required]
            public string OriginalBindingInformation { get; set; } = "";
        }

        public class ApplicationRequest
        {
            [Required]
            public string PhysicalPath { get; set; } = "";

            [Required]
            public string ApplicationPool { get; set; } = "";

            public bool PreloadEnabled { get; set; }

            // Blank for pass-through authentication
            public string? UserName { get; set; }

            // Blank keeps the saved password when UserName is unchanged
            public string? Password { get; set; }
        }

        public class AddVirtualDirectoryRequest : PathRequest
        {
            // e.g. "images" or "/sales/images"
            [Required]
            public string VirtualPath { get; set; } = "";
        }

        public class AddApplicationRequest : ApplicationRequest
        {
            // e.g. "sales" for /sales
            [Required]
            public string Alias { get; set; } = "";
        }
    }
}
