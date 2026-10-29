using IIGO.Data;
using IIGO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IIGO.Api
{
    // The fallback policy requires a signed-in user; the API uses keys instead, checked by RequireApiKey
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    [RequireApiKey]
    public class IISApiController(ILogger<IISApiController> logger, IISService IISService) : ControllerBase
    {
        [HttpGet("sites")]
        public ActionResult<List<IISSiteInfo>> GetIISSites()
        {
            try
            {
                return IISService.GetSites().Select(site => new IISSiteInfo
                {
                    Id = site.Id,
                    Name = site.Name,
                    State = site.State.ToString(),
                    ApplicationPool = site.Applications.Count > 0 ? site.Applications[0].ApplicationPoolName : "",
                    PhysicalPath = site.Applications.Count > 0 && site.Applications[0].VirtualDirectories.Count > 0
                        ? site.Applications[0].VirtualDirectories[0].PhysicalPath
                        : "",
                    Bindings = [.. site.Bindings.Select(b => new IISBindingInfo { Protocol = b.Protocol, BindingInformation = b.BindingInformation })]
                }).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading IIS sites");
                return Problem(statusCode: 500, detail: "Error loading IIS sites");
            }
        }
    }
}
